using System.Text.RegularExpressions;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Membersihkan seluruh audio sementara milik TTS saat aplikasi ditutup.
///
/// <para>
/// <b>Kenapa perlu.</b> Cache <c>%TEMP%\silverwolf-tts\</c>, berkas
/// <c>sw-tts-*.wav</c> / <c>sw-gabung-*.wav</c>, dan folder kerja Python
/// <c>sw-pekerja-*\</c> hanya bertambah dan tidak pernah dibuang sendiri —
/// pada mesin ini sudah terkumpul puluhan WAV dan belasan folder pekerja
/// yatim. Pembersihan ini mengembalikannya pada setiap penutupan.
/// </para>
///
/// <para>
/// <b>Urutan itu wajib.</b> Folder <c>sw-pekerja-*\</c> dipegang oleh proses
/// Python yang menetap. Menghapusnya selagi prosesnya hidup akan gagal
/// senyap (berkas terkunci). Pemanggil harus memastikan pekerja sudah mati
/// lebih dulu — lihat <c>TtsWorker.Matikan()</c> dan pemanggilannya di
/// <c>CompanionViewModel.DisposeAsync</c>.
/// </para>
///
/// <para>
/// Idempoten dan tidak pernah melempar: ini jalur penutupan, jadi kegagalan
/// apa pun harus ditelan, bukan menjatuhkan proses.
/// </para>
/// </summary>
public static class TempAudio
{
    /// <summary>Hanya berkas dengan pola ini yang disentuh.</summary>
    private static readonly Regex Pola = new(
        @"^(sw-tts-|sw-gabung-).*\.wav$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Hapus seluruh audio sementara TTS. <paramref name="log"/> opsional.
    /// </summary>
    public static void Bersihkan(Action<string>? log = null) =>
        Bersihkan(Path.GetTempPath(), log);

    /// <summary>
    /// Varian dengan akar temporer yang bisa disuntik agar bisa diuji tanpa
    /// menyentuh <c>%TEMP%</c> sungguhan.
    /// </summary>
    internal static void Bersihkan(string temp, Action<string>? log)
    {
        if (string.IsNullOrWhiteSpace(temp))
        {
            return;
        }

        // 1. Folder cache TTS.
        HapusFolder(Path.Combine(temp, "silverwolf-tts"), log);

        // 2. Berkas sementara lepas + folder kerja Python yatim.
        try
        {
            foreach (var jalur in Directory.EnumerateFileSystemEntries(temp))
            {
                var nama = Path.GetFileName(jalur);
                if (nama.StartsWith("sw-pekerja-", StringComparison.OrdinalIgnoreCase))
                {
                    HapusFolder(jalur, log);
                }
                else if (Pola.IsMatch(nama))
                {
                    HapusBerkas(jalur, log);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void HapusFolder(string jalur, Action<string>? log)
    {
        try
        {
            if (Directory.Exists(jalur))
            {
                Directory.Delete(jalur, recursive: true);
            }
        }
        catch (IOException)
        {
            // Masih dipegang proses lain; biarkan.
        }
        catch (UnauthorizedAccessException)
        {
            // Tidak punya izin; biarkan.
        }
    }

    private static void HapusBerkas(string jalur, Action<string>? log)
    {
        try
        {
            File.Delete(jalur);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
