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

    /// <summary>
    /// Ambang rotasi. Satu start bisa menulis ~35 KB, dan aplikasi yang hidup
    /// berjam-jam bisa menembus ratusan KB. Tanpa batas, crash.log tumbuh
    /// selamanya dan ikut terbawa ke setiap penyalinan bukti.
    /// </summary>
    private const long BatasBita = 2_000_000;

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

        // Penanda keluar proses — dibuat khusus untuk §8.1 (aplikasi mati senyap).
        //
        // Masalahnya: proses pernah berakhir dengan **exit code 0** tanpa entri
        // exception, tanpa `DisposeAsync`, dan tanpa pernah menjalankan
        // `Live2DNative.Hentikan()`. Dua kemungkinan yang sangat berbeda:
        //   1. ada yang memanggil `Environment.Exit` / `Main` kembali normal
        //      -> `ProcessExit` MENYALA, dan tulisan ini ada di crash.log;
        //   2. proses dihentikan dari luar (`TerminateProcess`, atau driver yang
        //      memanggil ExitProcess sendiri) -> `ProcessExit` TIDAK menyala,
        //      dan baris terakhir crash.log tetap penanda tahap sebelumnya.
        // Jadi ada-tidaknya baris "PROSES KELUAR" di ujung log memisahkan
        // keduanya tanpa perlu debugger.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Tulis("PROSES KELUAR", null,
                $"ProcessExit menyala; HasShutdownStarted={Environment.HasShutdownStarted}; "
                + $"StackTrace:{(Environment.StackTrace ?? "(kosong)").Replace(Environment.NewLine, " | ")}");
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
                PutarBilaBesar();
                File.AppendAllText(JalurBerkas, teks.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Pencatat tidak boleh ikut menjatuhkan proses.
        }
    }

    /// <summary>
    /// Kalau berkas sudah melewati <see cref="BatasBita"/>, ganti nama ke
    /// <c>crash.log.1</c> (menimpa yang lama) lalu mulai berkas baru.
    /// Satu tungku rotasi: hanya bukti terakhir yang disimpan, selaras dengan
    /// tujuan berkas ini (menangkap kematian terakhir, bukan riwayat penuh).
    /// </summary>
    private static void PutarBilaBesar()
    {
        try
        {
            var info = new FileInfo(JalurBerkas);
            if (!info.Exists || info.Length < BatasBita)
            {
                return;
            }

            var lama = JalurBerkas + ".1";
            if (File.Exists(lama))
            {
                File.Delete(lama);
            }

            File.Move(JalurBerkas, lama);
        }
        catch (Exception)
        {
            // Rotasi gagal (berkas terkunci dsb.) tidak boleh menghentikan
            // penulisan log — lebih baik berkas besar daripada log hilang.
        }
    }
}
