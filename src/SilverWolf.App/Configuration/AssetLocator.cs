using SilverWolf.Services.Configuration;

namespace SilverWolf.App.Configuration;

/// <summary>
/// Penunjuk aset untuk keperluan antarmuka.
///
/// Ini <b>bukan</b> penemu akar. Penemuan akar sudah diselesaikan di
/// <c>AppPaths.TentukanAkar()</c> beserta penanda <c>SilverWolf.sln</c> yang
/// diperbaiki dengan susah payah — menduplikasinya di sini hanya membuka
/// kembali bug "akar salah terdeteksi" (lihat <c>docs/PROYEK.md</c> §14).
///
/// Kelas ini hanya meneruskan jalur turunan dan melaporkan ada/tidaknya aset,
/// supaya M9 bisa menampilkan keadaan yang jujur tanpa mencoba menebak jalur.
/// </summary>
public static class AssetLocator
{
    /// <summary>Akar yang dipakai runtime. Tidak pernah melempar — jatuh ke direktori kerja.</summary>
    public static string Akar { get; } = AppPaths.TentukanAkar();

    public static string Live2D => AppPaths.Live2D(Akar);

    public static string Font => AppPaths.Font(Akar);

    public static string Ikon => AppPaths.Ikon(Akar);

    public static string Piper => AppPaths.ModelSuaraPiper(Akar);

    public static string Memori => AppPaths.Memori(Akar);

    public static bool AdaLive2D => Directory.Exists(Live2D);

    public static bool AdaFont => Directory.Exists(Font);

    public static bool AdaIkon => Directory.Exists(Ikon);

    /// <summary>
    /// Baris diagnostik singkat. Dipakai tampilan dan <c>crash.log</c> supaya
    /// kekurangan aset langsung terlihat tanpa membuka berkas log.
    /// </summary>
    public static string Ringkasan =>
        $"live2d={(AdaLive2D ? "ada" : "tiada")} font={(AdaFont ? "ada" : "tiada")} ikon={(AdaIkon ? "ada" : "tiada")}";
}
