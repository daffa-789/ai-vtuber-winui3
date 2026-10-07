namespace SilverWolf.Core.Configuration;

/// <summary>
/// Parser berkas <c>.env</c> — port setia dari <c>apps/server-node/src/config.js</c>
/// (<c>bacaEnv</c>, <c>potongKomentar</c>, <c>bersihLocal</c>).
///
/// Sengaja tidak memakai pustaka .env pihak ketiga: perilaku potong-komentar di
/// aplikasi lama punya aturan yang tidak umum (komentar hanya dihitung bila
/// didahului spasi/tab, dan kutip di awal menang atas tanda '#'), dan aturan itu
/// ikut menentukan nilai konfigurasi yang akhirnya masuk ke prompt LLM.
/// </summary>
public static class EnvFile
{
    /// <summary>Port <c>bacaEnv(isi)</c>.</summary>
    public static Dictionary<string, string> Parse(string? content)
    {
        var hasil = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(content))
        {
            return hasil;
        }

        foreach (var barisMentah in content.Split(['\n'], StringSplitOptions.None))
        {
            var baris = barisMentah.Trim('\r', ' ', '\t');
            if (baris.Length == 0 || baris.StartsWith('#') || !baris.Contains('='))
            {
                continue;
            }

            var idx = baris.IndexOf('=');
            var kunci = baris[..idx].Trim();
            var nilai = baris[(idx + 1)..];
            if (kunci.Length != 0)
            {
                hasil[kunci] = StripComment(nilai);
            }
        }

        return hasil;
    }

    /// <summary>Port <c>potongKomentar(nilai)</c>.</summary>
    public static string StripComment(string? nilai)
    {
        var mentah = (nilai ?? string.Empty).Trim();
        if (mentah.Length >= 2 && (mentah[0] == '"' || mentah[0] == '\''))
        {
            var kutip = mentah[0];
            var akhir = mentah.IndexOf(kutip, 1);
            return akhir > 0 ? mentah[1..akhir] : mentah[1..];
        }

        for (var i = 1; i < mentah.Length; i++)
        {
            if (mentah[i] == '#' && (mentah[i - 1] == ' ' || mentah[i - 1] == '\t'))
            {
                return mentah[..i].TrimEnd();
            }
        }

        return mentah;
    }

    /// <summary>
    /// Port <c>bersihLocal(nilai)</c> — hanya dipakai untuk nilai yang datang dari
    /// variabel lingkungan proses, bukan dari berkas.
    /// </summary>
    public static string StripQuotes(string? nilai)
    {
        var v = (nilai ?? string.Empty).Trim();
        if (v.Length >= 2 && v[0] == v[^1] && (v[0] == '"' || v[0] == '\''))
        {
            return v[1..^1];
        }

        return v;
    }
}
