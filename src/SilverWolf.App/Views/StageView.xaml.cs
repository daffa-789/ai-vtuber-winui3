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
    /// <b>Sejarah nilai ini, jangan diulang:</b> pernah 1.85 (warisan web) —
    /// hanya kepala+bahu. 0.88 — kepala & sayap terpotong. 0.48 — kekecilan.
    ///
    /// <b>0.70 dipakai sejak 2026-10-09 sore.</b> Alasannya aritmetika, bukan
    /// selera: hasil ukur luar (PIL pada tangkapan Master) menunjukkan karakter
    /// mengisi <b>524 px dari panel 672 px</b> pada 0.78. Artinya hanya tersisa
    /// **88 px render untuk dua margin** — mustahil memberi margin ~80 px per
    /// sisi sambil tetap menggeser karakter ke kiri seperti yang diminta Master.
    /// Salah satu sisi pasti terpotong.
    ///
    /// Dengan 0.70 lebar turun ke ~496 px render, menyisakan ~145 px untuk dua
    /// margin, sehingga permintaan "geser ke kiri, semua badan + sayap kelihatan"
    /// bisa dipenuhi sekaligus.
    ///
    /// Skala render (641 px) ke panel nyata (672 px) = 1.048.
    /// </summary>
    private const float Perbesaran = 0.70f;

    /// <summary>
    /// Geser horizontal dalam satuan ternormalisasi (1.0 = setengah lebar
    /// panel). Negatif = ke kiri.
    ///
    /// Master meminta karakter digeser ~80 px ke kiri dari posisi terakhir
    /// (yang terukur margin kiri 147 px / kanan 1 px — sayap kanan terpotong).
    ///
    /// Konversi yang dipakai:
    ///   1 satuan geser = 425 px render = 446 px panel
    ///   80 px panel   = 80 / 446 = 0.179 satuan
    ///   posisi lama -0.14 (sebelum 0.78) -> nilai baru di sekitar -0.32
    ///
    /// Karena perbesarannya sekaligus diturunkan ke 0.70 (lihat di atas), geser
    /// yang setara "80 px" menjadi lebih kecil: model yang lebih kecil bergerak
    /// px yang sama dengan satuan yang lebih besar. -0.20 menghasilkan
    /// pergeseran yang sama secara visual tanpa mendorong sisi kiri keluar.
    /// </summary>
    private const float GeserX = -0.20f;

    /// <summary>
    /// Jangkar vertikal dalam satuan ternormalisasi (1.0 = tepi atas panel).
    /// Positif = naik.
    ///
    /// Master meminta badan "dinaikkan ke atas". Pada 0.48 pusatnya di 42%
    /// tinggi bingkai tetapi menyisakan 352 px kosong di bawah. 0.30 dipakai:
    /// mengangkat model sehingga kepalanya tidak terlalu jauh dari atas
    /// sementara kaki mendekati dasar panel, mengisi ruang lebih merata.
    /// </summary>
    private const float JangkarY = 0.30f;

    /// <summary>Indeks gerakan idle di grup <c>isyarat</c>: 2 = "siklus".</summary>
    private const int IdleGerakan = 2;

    /// <summary>Titik wajah untuk ikut kursor — VITE_AVATAR_HEAD_Y = 0.46.</summary>
    private const float TitikWajahY = 0.46f;

    private DispatcherQueueTimer? _timerRender;
    private CompanionViewModel? _vm;
    private int _panggung = -1;
    private string _ekspresiTerakhir = string.Empty;
    private int _percobaan;
    private bool _aktif = true;
    private bool _berhenti;

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
        if (_berhenti) return;
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
        _timerRender.Tick += (_, _) =>
        {
            if (!_berhenti && _aktif && _panggung > 0) Live2DNative.Gambar(_panggung);
        };
        TerapkanEkspresi();
        if (_aktif) _timerRender.Start();
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
            if (!_berhenti && _panggung < 0)
            {
                CobaBuatPanggung();
            }
        };
        timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Berhenti();

        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmBerubah;
            _vm = null;
        }
    }

    /// <summary>
    /// Hentikan timer/retry dan invalidasikan ID panggung sebelum shutdown native.
    /// Tidak membebaskan objek native sendiri. Urutan ini adalah mitigasi teardown;
    /// penyebab dialog abort() dan keberhasilan penutupan masih perlu uji runtime.
    /// </summary>
    public void Berhenti()
    {
        try
        {
            _berhenti = true;
            _timerRender?.Stop();
            _timerRender = null;

            if (_panggung >= 0)
            {
                _panggung = -1;
            }

            _ekspresiTerakhir = string.Empty;
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("StageView.Berhenti", galat);
        }
    }

    /// <summary>
    /// Hentikan atau lanjutkan render sesuai fokus jendela.
    ///
    /// <para>
    /// <b>Kenapa ini penting di mesin ini:</b> GPU-nya Intel Iris Xe terintegrasi,
    /// jadi VRAM berbagi RAM dengan model bahasa 5,12 GB. Render 30 fps yang
    /// tetap berjalan saat jendela di belakang membakar CPU/GPU tanpa ada yang
    /// melihatnya — sekaligus menambah tekanan memori yang justru menjadi akar
    /// kematian Mode B/C (lihat docs/PROYEK.md §8.1). Menjeda saat tidak fokus
    /// adalah penghematan terbesar yang paling murah.
    /// </para>
    ///
    /// <para>
    /// Aman dipanggil kapan saja: kalau panggung belum jadi, timer belum ada dan
    /// panggilan ini tidak melakukan apa-apa (pembuatan panggung tetap berjalan
    /// lewat jalur <c>OnLoaded</c>).
    /// </para>
    /// </summary>
    public void UbahFokus(bool aktif)
    {
        _aktif = aktif;
        if (_berhenti) return;
        try
        {
            if (_timerRender is null || _panggung < 0)
            {
                return;
            }

            if (aktif)
            {
                if (!_timerRender.IsRunning)
                {
                    _timerRender.Start();
                }
            }
            else
            {
                if (_timerRender.IsRunning)
                {
                    _timerRender.Stop();
                }
            }
        }
        catch (Exception galat)
        {
            // Timer adalah async void di permukaan WinUI; exception di sini
            // tidak boleh menjatuhkan proses (pelajaran §7.19).
            CrashLog.Tulis("StageView.UbahFokus", galat);
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
            TerapkanEkspresi();
        }
    }

    private void TerapkanEkspresi()
    {
        if (_berhenti || _panggung <= 0 || _vm is null) return;
        var ekspresi = _vm.Expression;
        if (string.IsNullOrWhiteSpace(ekspresi) || ekspresi == _ekspresiTerakhir) return;
        var hasil = Live2DNative.AturEkspresi(_panggung, ekspresi);
        if (hasil == 0) _ekspresiTerakhir = ekspresi;
        CrashLog.Tahap($"live2d: ekspresi {ekspresi} -> {hasil}");
    }

    private void OnVmBerubah(object? sender, PropertyChangedEventArgs e)
    {
        if (_panggung <= 0 || _vm is null)
        {
            return;
        }

        if (e.PropertyName == nameof(CompanionViewModel.Expression))
        {
            // Wajah diaktifkan kembali; label teks ekspresi tetap dihapus.
            TerapkanEkspresi();
        }
        else if (e.PropertyName == nameof(CompanionViewModel.MirrorTrack))
        {
            // Mirror baru benar-benar bekerja setelah M8 selesai; di sini hanya
            // diteruskan supaya tidak perlu diubah lagi nanti.
            Live2DNative.AturTampak(_panggung, Perbesaran, GeserX, JangkarY);
        }
        else if (e.PropertyName == nameof(CompanionViewModel.LevelSuara))
        {
            // LipSync. Nilainya RMS audio nyata yang dihitung PcmPlayer dari
            // berkas WAV yang sedang diputar, lalu dipetakan ke posisi
            // pemutaran — bukan perkiraan dari waktu berjalan.
            //
            // Dikirim apa adanya: peredaman dan pembatas laju dikerjakan di
            // sisi native (Perbarui), supaya mulut tetap mulus walaupun
            // notifikasi properti datang tidak beraturan.
            Live2DNative.AturMulut(_panggung, _vm.LevelSuara);
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
    ///
    /// <para>
    /// <b>Kenapa Y dibalik tandanya.</b> Dua sistem koordinat di sini berlawanan
    /// arah sumbu Y:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Layar (WinUI):</b> Y bertambah ke BAWAH. Kursor di
    /// atas kepala menghasilkan <c>titik.Y - wajahY</c> yang <b>negatif</b>.</description></item>
    /// <item><description><b>Cubism:</b> <c>ParamAngleY</c> positif = kepala
    /// menoleh ke ATAS. Framework mendaftarkannya lewat
    /// <c>LookParameterData(ParamAngleY, 0.0f, 30.0f)</c> di
    /// <c>Live2DStage.cpp</c> — basis 0, puncak di +30 untuk masukan positif.</description></item>
    /// </list>
    /// <para>
    /// Tanpa pembalikan, kursor di atas kepala memberi Y negatif sehingga kepala
    /// justru menunduk, dan sebaliknya. Itulah gejala "arahnya kebalik" yang
    /// dilaporkan Master. Jadi: <c>y = -(titik.Y - wajahY) / rentangY</c>.
    /// Sumbu X tidak dibalik — kiri/kanan kedua sistem sama arahnya.
    /// </para>
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

        // Dibagi MINUS: layar Y ke bawah, Cubism ParamAngleY ke atas.
        // Lihat penjelasan di doc comment — ini yang membetulkan arah pandang.
        var y = Jepit(-(titik.Y - wajahY) / rentangY);

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
    /// Membalik <c>CompanionViewModel.MirrorTrack</c> (disimpan ke
    /// <c>LocalApplicationData\SilverWolf\</c>).
    ///
    /// <para>
    /// <b>Sudah tidak dipanggil siapa pun.</b> Tombol MIRROR di
    /// <c>StageView.xaml</c> dibuang atas permintaan Master (9 Okt 2026), jadi
    /// penangan ini tinggal warisan. Sengaja tidak dihapus: logikanya masih
    /// benar, dan mengembalikan tombolnya cukup menambah satu <c>Button</c>
    /// dengan <c>Click="OnMirrorDiklik"</c> — tidak perlu menulis ulang C#.
    /// </para>
    /// </summary>
    private void OnMirrorDiklik(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompanionViewModel vm)
        {
            vm.MirrorTrack = !vm.MirrorTrack;
        }
    }
}
