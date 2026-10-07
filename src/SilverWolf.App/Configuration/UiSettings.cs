using System.Text.Json;
using SilverWolf.App.Diagnostics;

namespace SilverWolf.App.Configuration;

/// <summary>
/// Preferensi tampilan yang bertahan lintas sesi.
///
/// Kenapa tidak memakai <c>ApplicationData.Current.LocalSettings</c>, padanan
/// <c>localStorage</c> yang direncanakan di
/// <c>docs/migrasi/01-peta-fitur.md</c> §5:
///
///   Aplikasi ini <b>unpackaged</b> (<c>WindowsPackageType=None</c>), dan
///   <c>ApplicationData.Current</c> melempar
///   <c>System.InvalidOperationException: Operation is not valid due to the
///   current state of the object.</c> saat dipanggil dari aplikasi tanpa
///   identitas paket.
///
/// Terverifikasi langsung 2026-10-07: <c>crash.log</c> mencatat kegagalan itu
/// pada sumber <c>BacaMirror</c>. Startup selamat hanya karena
/// <c>CompanionViewModel</c> membungkusnya dengan try/catch — tetapi
/// preferensinya tidak pernah tersimpan.
///
/// Karena itu preferensi disimpan sebagai berkas JSON biasa di
/// <c>LocalApplicationData\SilverWolf\</c>.
/// </summary>
public static class UiSettings
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SilverWolf");

    private static readonly string Berkas = Path.Combine(Folder, "ui-settings.json");

    public static bool Baca(string kunci, bool bawaan = false)
    {
        try
        {
            if (!File.Exists(Berkas))
            {
                return bawaan;
            }

            var teks = File.ReadAllText(Berkas);
            if (string.IsNullOrWhiteSpace(teks))
            {
                return bawaan;
            }

            var kamus = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(teks);
            if (kamus is null || !kamus.TryGetValue(kunci, out var nilai))
            {
                return bawaan;
            }

            return nilai.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(nilai.GetString(), out var b) ? b : bawaan,
                _ => bawaan,
            };
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("UiSettings.Baca", galat, $"kunci={kunci}");
            return bawaan;
        }
    }

    public static void Tulis(string kunci, bool nilai)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            var kamus = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (File.Exists(Berkas))
            {
                var teks = File.ReadAllText(Berkas);
                if (!string.IsNullOrWhiteSpace(teks))
                {
                    kamus = JsonSerializer.Deserialize<Dictionary<string, bool>>(teks) ?? kamus;
                }
            }

            kamus[kunci] = nilai;
            File.WriteAllText(Berkas, JsonSerializer.Serialize(kamus));
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("UiSettings.Tulis", galat, $"kunci={kunci}");
        }
    }
}
