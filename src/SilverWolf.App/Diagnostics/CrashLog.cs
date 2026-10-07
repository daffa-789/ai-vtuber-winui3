using System.Text;

namespace SilverWolf.App.Diagnostics;

/// <summary>
/// Pencatat kegagalan proses ke berkas.
///
/// Kenapa ini ada: WinUI 3 pada aplikasi unpackaged bisa berhenti dengan
/// <c>STATUS_FAIL_FAST_EXCEPTION</c> (0xC0000602) <b>tanpa</b> pesan apa pun —
/// tidak ada keluaran stderr, tidak ada entri "Application Error" di Event Log,
/// dan tidak ada laporan Windows Error Reporting. Debugger Visual Studio hanya
/// menampilkan "A fatal exception occurred" tanpa nama tipe atau stack.
///
/// Tanpa pencatat seperti ini, kegagalan seperti itu praktis tidak bisa
/// didiagnosis. Berkasnya sengaja ditaruh di sebelah exe supaya mudah ditemukan
/// dan ikut terhapus saat folder build dibersihkan.
/// </summary>
public static class CrashLog
{
    private static readonly object Kunci = new();
    private static bool _sudahDipasang;

    public static string JalurBerkas => Path.Combine(AppContext.BaseDirectory, "crash.log");

    /// <summary>
    /// Pasang penangkap untuk ketiga sumber exception yang tidak tertangani.
    /// Aman dipanggil berkali-kali.
    /// </summary>
    public static void Pasang()
    {
        if (_sudahDipasang)
        {
            return;
        }

        _sudahDipasang = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Tulis("AppDomain.UnhandledException", e.ExceptionObject as Exception,
                $"IsTerminating={e.IsTerminating}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Tulis("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        // Kegagalan yang tidak bisa dipulihkan: jangan diam-diam.
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            // Hanya dicatat bila benar-benar tidak ada penangkap lain; first-chance
            // terlalu berisik untuk ditulis semuanya.
            _ = e;
        };
    }

    /// <summary>Catat satu penanda tahap — berguna untuk melacak sampai mana startup berjalan.</summary>
    public static void Tahap(string nama) => Tulis($"TAHAP: {nama}", null, null);

    public static void Tulis(string sumber, Exception? error, string? catatan = null)
    {
        var teks = new StringBuilder()
            .AppendLine("──────────────────────────────────────────────")
            .AppendLine($"waktu   : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}")
            .AppendLine($"sumber  : {sumber}");

        if (catatan is not null)
        {
            teks.AppendLine($"catatan : {catatan}");
        }

        if (error is not null)
        {
            teks.AppendLine($"tipe    : {error.GetType().FullName}");
            teks.AppendLine($"pesan   : {error.Message}");
            if (error is AggregateException agg && agg.InnerExceptions.Count > 0)
            {
                teks.AppendLine($"inner[{agg.InnerExceptions.Count}]:");
                foreach (var inner in agg.InnerExceptions)
                {
                    teks.AppendLine($"  - {inner.GetType().FullName}: {inner.Message}");
                }
            }

            teks.AppendLine("stack   :");
            teks.AppendLine(error.ToString());
        }

        teks.AppendLine();

        try
        {
            lock (Kunci)
            {
                File.AppendAllText(JalurBerkas, teks.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Pencatat tidak boleh ikut menjatuhkan proses.
        }
    }
}
