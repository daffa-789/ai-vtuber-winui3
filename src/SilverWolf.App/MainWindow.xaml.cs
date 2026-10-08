using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SilverWolf.App.Configuration;
using SilverWolf.App.Diagnostics;
using SilverWolf.App.Native;
using SilverWolf.App.ViewModels;

namespace SilverWolf.App
{
    /// <summary>
    /// Jendela utama. Pengganti BrowserWindow Electron.
    ///
    /// Ukuran 1180x760 dan minimum 780x560 tidak bisa diset lewat XAML — Window MinWidth
    /// tidak ada di WinUI 3. Harus lewat AppWindow (lihat InitializeWindowing()).
    ///
    /// Penanda tahap <c>CrashLog</c> di bawah adalah alat diagnosis utama
    /// (lihat docs/PROYEK.md §7.6): tanpa mereka, kegagalan fail-fast tidak
    /// meninggalkan jejak apa pun. Jangan dibuang saat menyusun ulang berkas ini.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            CrashLog.Tahap("MainWindow: sebelum InitializeComponent()");

            // Dibuat sebelum XAML dimuat supaya pengikatan langsung punya sumber.
            // Konstruktor ini tidak memulai runtime — itu dilakukan di Loaded,
            // agar Activate() tidak tertahan oleh pemuatan berkas dan kizuna.
            ViewModel = new CompanionViewModel();

            InitializeComponent();

            CrashLog.Tahap("MainWindow: InitializeComponent() selesai");

            Shell.DataContext = ViewModel;

            // Window di WinUI 3 tidak punya kejadian Loaded — yang punya adalah
            // elemen akar isinya. Karena itu dipasang ke Shell, bukan ke this.
            Shell.Loaded += OnLoaded;
            Closed += OnClosed;

            // Jeda render Live2D saat jendela tidak fokus. Di GPU terintegrasi
            // (VRAM = RAM), loop 30 fps yang jalan di latar membakar CPU/GPU
            // dan menambah tekanan memori yang tidak terlihat siapa pun.
            Activated += OnActivated;

            InitializeWindowing();

            CrashLog.Tahap("MainWindow: InitializeWindowing() selesai");
        }

        /// <summary>Satu-satunya sumber keadaan antarmuka (port store.js).</summary>
        public CompanionViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CrashLog.Tahap($"aset: {AssetLocator.Ringkasan} (akar {AssetLocator.Akar})");

                var antrean = DispatcherQueue.GetForCurrentThread();
                await ViewModel.InisialisasiAsync(antrean);

                CrashLog.Tahap("MainWindow: runtime siap");
            }
            catch (Exception galat)
            {
                CrashLog.Tulis("MainWindow.OnLoaded", galat);
            }
        }

        /// <summary>
        /// Jeda/lanjutkan render Live2D saat fokus jendela berubah.
        /// <paramref name="args"/> memuat <c>WindowActivationState</c>: Deactivated
        /// berarti jendela kehilangan fokus (atau diminimize) — hentikan loop.
        /// </summary>
        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            var aktif = args.WindowActivationState
                != WindowActivationState.Deactivated;

            Stage.UbahFokus(aktif);
        }

        private async void OnClosed(object sender, WindowEventArgs e)
        {
            // Lepaskan dulu handler aktivasi supaya tidak ada `UbahFokus` yang
            // menyala di tengah pembongkaran.
            Activated -= OnActivated;

            // Mitigasi teardown: stop render/retry sebelum menutup layanan.
            // Ini belum membuktikan penyebab ataupun penyelesaian dialog abort().
            Stage.Berhenti();
            try
            {
                await ViewModel.DisposeAsync();
            }
            catch (Exception galat)
            {
                CrashLog.Tulis("MainWindow.OnClosed", galat);
            }
            finally
            {
                Live2DNative.Hentikan();
            }
        }

        private void InitializeWindowing()
        {
            CrashLog.Tahap("windowing: ambil HWND");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            CrashLog.Tahap($"windowing: HWND = 0x{hwnd:X}");

            CrashLog.Tahap("windowing: GetWindowIdFromWindow");
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            CrashLog.Tahap($"windowing: WindowId = {windowId.Value}");

            CrashLog.Tahap("windowing: AppWindow.GetFromWindowId");
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            CrashLog.Tahap($"windowing: AppWindow diperoleh (judul '{appWindow.Title}')");

            // Electron memakai width/height dalam DIP, sedangkan AppWindow.Resize
            // memakai PIKsel FISIK. Tanpa dikali skala, di layar 125% jendelanya
            // 20% lebih kecil daripada aplikasi lama dan panggung Live2D-nya ikut
            // mengecil. Lihat docs/PROYEK.md §8.3.
            var skala = WindowsDpi.Skala(hwnd);

            // Tetapi 1180x760 DIP TIDAK selalu muat: di layar 1920x1080 dengan
            // skala 125% jendelanya menjadi 1475x950 piksel, dan kalau Windows
            // menaruhnya agak ke bawah, bagian bawahnya — yaitu kotak input
            // pesan — jatuh keluar layar dan tidak bisa diklik sama sekali.
            // Jadi ukurannya dijepit ke area kerja yang benar-benar tersedia.
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var kerja = area.WorkArea;
            var batasLebar = (int)(kerja.Width * 0.95);
            var batasTinggi = (int)(kerja.Height * 0.95);

            var lebar = Math.Min(WindowsDpi.Piksel(1180, skala), batasLebar);
            var tinggi = Math.Min(WindowsDpi.Piksel(760, skala), batasTinggi);
            var minLebar = Math.Min(WindowsDpi.Piksel(780, skala), lebar);
            var minTinggi = Math.Min(WindowsDpi.Piksel(560, skala), tinggi);

            CrashLog.Tahap($"windowing: area kerja {kerja.Width}x{kerja.Height}, skala {skala:F3}");

            // Electron: width 1180, height 760
            CrashLog.Tahap($"windowing: Resize({lebar},{tinggi})");
            appWindow.Resize(new Windows.Graphics.SizeInt32(lebar, tinggi));
            CrashLog.Tahap("windowing: Resize selesai");

            // Electron: minWidth 780, minHeight 560
            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                CrashLog.Tahap($"windowing: set PreferredMinimum {minLebar}x{minTinggi}");
                presenter.PreferredMinimumWidth = minLebar;
                presenter.PreferredMinimumHeight = minTinggi;
                CrashLog.Tahap("windowing: PreferredMinimum selesai");
            }
            else
            {
                CrashLog.Tahap($"windowing: presenter bukan OverlappedPresenter ({appWindow.Presenter?.GetType().Name})");
            }

            // TODO(M12): tray icon, hotkey Ctrl+Shift+S (RegisterHotKey via P/Invoke),
            //            close-to-tray (appWindow.Closing + e.Cancel + presenter.Hide()).
        }
    }
}
