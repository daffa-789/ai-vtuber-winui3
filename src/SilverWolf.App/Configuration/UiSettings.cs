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

    /// <summary>
    /// Kamus mentah. Nilai disimpan sebagai <see cref="JsonElement"/> karena satu
    /// berkas kini menampung dua jenis nilai (bool lama seperti
    /// <c>silverwolf_mirror_track</c>, dan teks baru seperti
    /// <c>silverwolf_model_path</c>).
    ///
    /// <b>Kenapa aman untuk berkas lama:</b> berkas yang ditulis versi sebelumnya
    /// berbentuk <c>{"kunci":true}</c>, dan itu terbaca apa adanya sebagai
    /// <see cref="JsonValueKind.True"/> — jadi preferensi yang sudah tersimpan
    /// tidak hilang saat memperbarui aplikasi.
    /// </summary>
    private static Dictionary<string, JsonElement> Kamus()
    {
        if (!File.Exists(Berkas))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var teks = File.ReadAllText(Berkas);
        if (string.IsNullOrWhiteSpace(teks))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(teks)
               ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }

    public static bool Baca(string kunci, bool bawaan = false)
    {
        try
        {
            if (!Kamus().TryGetValue(kunci, out var nilai))
            {
                return bawaan;
            }

            return nilai.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(nilai.GetString(), out var b) ? b : bawaan,
                JsonValueKind.Number => nilai.TryGetInt32(out var n) ? n != 0 : bawaan,
                _ => bawaan,
            };
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("UiSettings.Baca", galat, $"kunci={kunci}");
            return bawaan;
        }
    }

    public static void Tulis(string kunci, bool nilai) =>
        TulisMentah(kunci, JsonSerializer.SerializeToElement(nilai));

    /// <summary>
    /// Baca preferensi teks. Mengembalikan <paramref name="bawaan"/> bila kunci
    /// tidak ada, atau bila nilainya bukan teks (mis. berkas lama dari versi
    /// yang belum mengenal kunci ini).
    /// </summary>
    public static string BacaTeks(string kunci, string bawaan = "")
    {
        try
        {
            if (!Kamus().TryGetValue(kunci, out var nilai) || nilai.ValueKind != JsonValueKind.String)
            {
                return bawaan;
            }

            var teks = nilai.GetString();
            return string.IsNullOrWhiteSpace(teks) ? bawaan : teks;
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("UiSettings.BacaTeks", galat, $"kunci={kunci}");
            return bawaan;
        }
    }

    public static void TulisTeks(string kunci, string nilai) =>
        TulisMentah(kunci, JsonSerializer.SerializeToElement(nilai));

    /// <summary>
    /// Tulis satu kunci tanpa menimpa kunci lain — berkas dibaca ulang dulu,
    /// baru ditulis penuh. Menulis seluruh berkas (bukan menambal sebagian)
    /// menjaga berkas tetap JSON valid sekalipun proses mati di tengah.
    /// </summary>
    private static void TulisMentah(string kunci, JsonElement nilai)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            var kamus = Kamus();
            kamus[kunci] = nilai;
            File.WriteAllText(Berkas, JsonSerializer.Serialize(kamus));
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("UiSettings.TulisMentah", galat, $"kunci={kunci}");
        }
    }
}
