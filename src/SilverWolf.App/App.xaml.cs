using Microsoft.UI.Xaml;
using SilverWolf.App.Diagnostics;
using SilverWolf.Services.Configuration;

namespace SilverWolf.App
{
    /// <summary>
    /// Titik masuk aplikasi. Pengganti Electron main process
    /// (apps/stage-tamagotchi/src/main/index.js).
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            // Dipasang paling awal supaya kegagalan saat startup pun tercatat.
            CrashLog.Pasang();
            CrashLog.Tahap("App() mulai");

            UnhandledException += (_, e) =>
            {
                CrashLog.Tulis("Application.UnhandledException", e.Exception,
                    $"Handled={e.Handled}");
                e.Handled = false;
            };

            try
            {
                InitializeComponent();
                CrashLog.Tahap("InitializeComponent() selesai");
            }
            catch (Exception error)
            {
                CrashLog.Tulis("InitializeComponent()", error);
                throw;
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            CrashLog.Tahap("OnLaunched mulai");

            // Diagnostik jalur: akar yang ditemukan dan kelengkapan aset.
            // Aset dipindahkan dari `AI Vtuber Web/` ke sini pada 2026-10-07,
            // jadi pemeriksaan ini memastikan penemuan akar benar-benar bekerja.
            try
            {
                var akar = AppPaths.TentukanAkar();
                CrashLog.Tahap($"akar = {akar}");

                foreach (var (nama, jalur, ada) in AppPaths.Periksa(akar))
                {
                    CrashLog.Tahap($"  {(ada ? "OK    " : "HILANG")} {nama,-22} {jalur}");
                }
            }
            catch (Exception error)
            {
                CrashLog.Tulis("PeriksaAset", error);
            }

            try
            {
                // TODO(M12): AppInstance.FindOrRegisterForKey("SilverWolf.SingleInstance.v1")
                //            + RedirectActivationToAsync — pengganti app.requestSingleInstanceLock().
                _window = new MainWindow();
                CrashLog.Tahap("MainWindow dibuat");

                _window.Activate();
                CrashLog.Tahap("MainWindow diaktifkan");
            }
            catch (Exception error)
            {
                CrashLog.Tulis("OnLaunched", error);
                throw;
            }
        }
    }
}
