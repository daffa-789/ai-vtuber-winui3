using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SilverWolf.App.Configuration;
using SilverWolf.App.Diagnostics;
using SilverWolf.App.Native;
using SilverWolf.App.ViewModels;

namespace SilverWolf.App.Views;

/// <summary>Panel kiri — port <c>komponen/Panggung.jsx</c>.</summary>
public sealed partial class StageView : UserControl
{
    /// <summary>
    /// Perbesaran relatif terhadap "pas tinggi": 1.0 berarti tinggi kanvas model
    /// tepat memenuhi tinggi panel. Nilainya BUKAN skala mentah Cubism — ruang
    /// kerja Cubism ternormalisasi, jadi 1.0 berarti tinggi tampil 2.0 satuan
    /// pada rentang pandang yang juga 2.0 satuan.
    ///
    /// <b>Nilai ini pernah 1.85 dan itu terlalu besar.</b> Warisan aplikasi web
    /// (<c>VITE_AVATAR_ZOOM=1.85</c>) tidak bisa dipindahkan apa adanya: di web
    /// zoom itu mengalikan skala "pas panel" pada PIXI dengan jangkar 0.92,
    /// sedangkan di sini <c>SetHeight(2.0 * perbesaran)</c> langsung membuat
    /// model setinggi itu. Akibatnya pada 1.85 hanya kepala dan bahu yang
    /// terlihat — kepala tampak raksasa dan badan terpotong.
    ///
    /// 0.88 dipilih dari pengukuran, bukan tebakan: pada nilai ini seluruh
    /// karakter (termasuk ujung sayap) masuk ke panel, sementara tingginya
    /// masih memenuhi sekitar dua pertiga panel.
    /// </summary>
    private const float Perbesaran = 0.88f;

    /// <summary>
    /// Geser horizontal dalam satuan ternormalisasi (1.0 = setengah lebar
    /// panel). Negatif = ke kiri.
    ///
    /// Kenapa tidak 0: seni model ini tidak simetris — sayap mekaniknya jauh
    /// lebih panjang ke kanan daripada ke kiri. Dengan geser 0, ujung sayap
    /// kanan terpotong tepi panel (terukur 90 piksel menyentuh tepi pada
    /// perbesaran 1.0). Menggeser sedikit ke kiri memindahkan potongan itu ke
    /// sisi yang memang lega.
    /// </summary>
    private const float GeserX = -0.10f;

    /// <summary>
    /// Jangkar vertikal dalam satuan ternormalisasi (1.0 = tepi atas panel).
    /// Positif = naik. 0.10 mengangkat model sedikit supaya tidak menempel
    /// dasar panel.
    /// </summary>
    private const float JangkarY = 0.10f;

    /// <summary>Indeks gerakan idle di grup <c>isyarat</c>: 2 = "siklus".</summary>
    private const int IdleGerakan = 2;

    /// <summary>Titik wajah untuk ikut kursor — VITE_AVATAR_HEAD_Y = 0.46.</summary>
    private const float TitikWajahY = 0.46f;

    private DispatcherQueueTimer? _timerRender;
    private CompanionViewModel? _vm;
    private int _panggung = -1;
    private string _ekspresiTerakhir = string.Empty;
    private int _percobaan;

    public StageView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        PanggungPanel.SizeChanged += OnPanelUbahUkuran;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_panggung >= 0)
        {
            return;
        }

        try
        {
            var hasil = Live2DNative.Mulai();
            if (hasil != 0)
            {
                CrashLog.Tahap($"live2d: tidak aktif ({Live2DNative.Alasan})");
                return;
            }

            if (!AssetLocator.AdaLive2D)
            {
                CrashLog.Tahap($"live2d: model tidak ada di {AssetLocator.Live2D}");
                return;
            }

            _percobaan = 0;
            CobaBuatPanggung();
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("StageView.OnLoaded", galat);
        }
    }

    /// <summary>
    /// SwapChainPanel belum punya "peer" native begitu Loaded menyala —
    /// QueryInterface ke ISwapChainPanelNative di titik itu mengembalikan
    /// E_NOINTERFACE, walau penunjuknya sudah objek SwapChainPanel yang sah.
    /// Karena itu dicoba ulang beberapa kali sampai panel benar-benar hidup.
    /// </summary>
    private void CobaBuatPanggung()
    {
        _percobaan++;

        var penunjuk = Live2DNative.AmbilPenunjukPanel(PanggungPanel);
        if (penunjuk == IntPtr.Zero)
        {
            CrashLog.Tahap("live2d: penunjuk panel tidak diperoleh");
            JadwalkanUlang();
            return;
        }

        var id = Live2DNative.BuatPanggung(penunjuk, AssetLocator.Live2D, "silverwolf.model3.json");
        if (id <= 0)
        {
            CrashLog.Tahap($"live2d: percobaan {_percobaan} gagal (kode {id})");
            JadwalkanUlang();
            return;
        }

        _panggung = id;
        CrashLog.Tahap($"live2d: panggung {_panggung} aktif pada percobaan {_percobaan}");

        Live2DNative.AturTampak(_panggung, Perbesaran, GeserX, JangkarY);
        UbahUkuranPanel();

        // Gerakan idle berulang. Tanpa ini model tampak diam membeku walaupun
        // renderer berjalan 30 fps — bingkai tetap digambar, isinya tidak
        // berubah. Grup dan indeks diambil dari silverwolf.model3.json:
        //   Motions.isyarat = [berubah-1, berubah-2, siklus, tidur]
        // Indeks 2 = "siklus", gerakan memutar yang memang untuk keadaan diam.
        var hasilIdle = Live2DNative.MainkanIdle(_panggung, "isyarat", IdleGerakan);
        CrashLog.Tahap($"live2d: idle grup=isyarat indeks={IdleGerakan} -> {hasilIdle}");

        Placeholder.Visibility = Visibility.Collapsed;

        // ~30 fps cukup untuk Live2D dan jauh lebih hemat daripada
        // CompositionTarget.Rendering yang menembak tiap bingkai UI.
        var antrean = DispatcherQueue.GetForCurrentThread();
        _timerRender = antrean.CreateTimer();
        _timerRender.Interval = TimeSpan.FromMilliseconds(33);
        _timerRender.Tick += (_, _) => Live2DNative.Gambar(_panggung);
        _timerRender.Start();
    }

    private void JadwalkanUlang()
    {
        if (_percobaan >= 20)
        {
            CrashLog.Tahap("live2d: menyerah setelah 20 percobaan — SwapChainPanel tidak pernah siap");
            return;
        }

        var antrean = DispatcherQueue.GetForCurrentThread();
        var timer = antrean.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(150);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_panggung < 0)
            {
                CobaBuatPanggung();
            }
        };
        timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timerRender?.Stop();
        _timerRender = null;

        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmBerubah;
            _vm = null;
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmBerubah;
        }

        _vm = args.NewValue as CompanionViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmBerubah;
        }
    }

    private void OnVmBerubah(object? sender, PropertyChangedEventArgs e)
    {
        if (_panggung <= 0 || _vm is null)
        {
            return;
        }

        if (e.PropertyName == nameof(CompanionViewModel.Expression))
        {
            var ekspresi = _vm.Expression;
            if (string.IsNullOrWhiteSpace(ekspresi) || ekspresi == _ekspresiTerakhir)
            {
                return;
            }

            _ekspresiTerakhir = ekspresi;
            Live2DNative.AturEkspresi(_panggung, ekspresi);
        }
        else if (e.PropertyName == nameof(CompanionViewModel.MirrorTrack))
        {
            // Mirror baru benar-benar bekerja setelah M8 selesai; di sini hanya
            // diteruskan supaya tidak perlu diubah lagi nanti.
            Live2DNative.AturTampak(_panggung, Perbesaran, GeserX, JangkarY);
        }
    }

    private void OnPanelUbahUkuran(object sender, SizeChangedEventArgs e) => UbahUkuranPanel();

    private void UbahUkuranPanel()
    {
        if (_panggung <= 0)
        {
            return;
        }

        // Swap chain bekerja dalam piksel FISIK, sedangkan ActualWidth/Height
        // dalam DIP. Tanpa dikali skala rasterisasi, di layar 125% buffer
        // gambarnya lebih kecil daripada panelnya sehingga hasilnya buram
        // karena direntangkan (DXGI_SCALING_STRETCH).
        var skala = PanggungPanel.XamlRoot?.RasterizationScale ?? 1.0;
        var lebar = (int)Math.Max(1, Math.Round(PanggungPanel.ActualWidth * skala));
        var tinggi = (int)Math.Max(1, Math.Round(PanggungPanel.ActualHeight * skala));

        // Dicatat karena inilah satu-satunya cara memastikan buffer benar-benar
        // seukuran piksel fisik panel. Kalau `skala` salah (mis. XamlRoot belum
        // siap sehingga jatuh ke 1.0 padahal layar 125%), buffer jadi lebih
        // kecil daripada panelnya lalu direntangkan DXGI_SCALING_STRETCH —
        // hasilnya tampak berbutir/blok, dan tidak ada galat apa pun.
        CrashLog.Tahap($"live2d: panel {PanggungPanel.ActualWidth:F1}x{PanggungPanel.ActualHeight:F1} DIP, "
                       + $"skala={skala:F3}, buffer={lebar}x{tinggi}");

        Live2DNative.UbahUkuran(_panggung, lebar, tinggi);
    }

    /// <summary>
    /// Ikut kursor — port <c>aktifkanIkutiKursor()</c>. Posisi kursor
    /// dinormalisasi terhadap titik wajah lalu dijepit ke rentang -1..1,
    /// persis seperti aplikasi web.
    /// </summary>
    private void OnPanggungPointerBergerak(object sender, PointerRoutedEventArgs e)
    {
        if (_panggung <= 0 || PanggungPanel.ActualWidth <= 0 || PanggungPanel.ActualHeight <= 0)
        {
            return;
        }

        var titik = e.GetCurrentPoint(PanggungPanel).Position;
        var lebar = PanggungPanel.ActualWidth;
        var tinggi = PanggungPanel.ActualHeight;

        var wajahX = lebar * 0.5;
        var wajahY = tinggi * TitikWajahY;

        var rentangX = Math.Max(160.0, wajahX * 0.85);
        var rentangY = Math.Max(160.0, wajahY * 0.85);

        var x = Jepit((titik.X - wajahX) / rentangX);
        var y = Jepit((titik.Y - wajahY) / rentangY);

        Live2DNative.AturPandang(_panggung, (float)x, (float)y);
    }

    private void OnPanggungPointerKeluar(object sender, PointerRoutedEventArgs e)
    {
        if (_panggung > 0)
        {
            Live2DNative.AturPandang(_panggung, 0f, 0f);
        }
    }

    private static double Jepit(double nilai) => Math.Clamp(nilai, -1.0, 1.0);

    /// <summary>
    /// Mirror disimpan oleh <c>CompanionViewModel.MirrorTrack</c> (ke
    /// <c>LocalApplicationData\SilverWolf\</c>). Tombol ini hanya membaliknya.
    /// </summary>
    private void OnMirrorDiklik(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompanionViewModel vm)
        {
            vm.MirrorTrack = !vm.MirrorTrack;
        }
    }
}
