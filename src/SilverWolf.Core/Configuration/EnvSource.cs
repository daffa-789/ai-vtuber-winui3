using System.Globalization;

namespace SilverWolf.Core.Configuration;

/// <summary>
/// Port <c>buatEnvSource</c> + <c>nilai/angka/angkaFloat/bool_/daftar</c> dari
/// <c>apps/server-node/src/config.js</c>.
///
/// Urutan prioritas sama persis dengan aplikasi lama: variabel lingkungan proses
/// menang atas berkas <c>.env</c>, dan nilai proses yang kosong/putih dianggap
/// tidak ada sehingga jatuh ke berkas.
/// </summary>
public sealed class EnvSource
{
    /// <summary>Nilai dari berkas <c>.env</c>.</summary>
    public Dictionary<string, string> File { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Nilai dari lingkungan proses.</summary>
    public Dictionary<string, string> Environ { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Peringatan yang dikumpulkan saat konversi gagal. Aplikasi lama mencetak
    /// semuanya ke stderr setelah server hidup; di sini dikumpulkan agar bisa
    /// ditampilkan di UI nanti.
    /// </summary>
    public List<string> Warnings { get; } = [];

    public static EnvSource FromFile(string? isiEnv) => new() { File = EnvFile.Parse(isiEnv) };

    /// <summary>Port <c>nilai(env, kunci, bawaan)</c>.</summary>
    public string Value(string kunci, string bawaan = "")
    {
        if (Environ.TryGetValue(kunci, out var dariEnv) && !string.IsNullOrWhiteSpace(dariEnv))
        {
            return EnvFile.StripQuotes(dariEnv);
        }

        return File.TryGetValue(kunci, out var dariBerkas) ? dariBerkas : bawaan;
    }

    /// <summary>Port <c>angka(env, kunci, bawaan)</c>.</summary>
    public int Int(string kunci, int bawaan)
    {
        var mentah = Value(kunci, bawaan.ToString(CultureInfo.InvariantCulture)).Trim();
        if (mentah.Length == 0)
        {
            return bawaan;
        }

        if (!double.TryParse(mentah, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            || !double.IsFinite(n)
            || n != Math.Floor(n))
        {
            Warnings.Add($"{kunci}=\"{mentah}\" bukan bilangan bulat; dipakai bawaan {bawaan}");
            return bawaan;
        }

        return (int)n;
    }

    /// <summary>Port <c>angkaFloat(env, kunci, bawaan)</c>.</summary>
    public double Float(string kunci, double bawaan)
    {
        var mentah = Value(kunci, bawaan.ToString(CultureInfo.InvariantCulture)).Trim();
        if (mentah.Length == 0)
        {
            return bawaan;
        }

        if (!double.TryParse(mentah, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            || !double.IsFinite(n))
        {
            Warnings.Add($"{kunci}=\"{mentah}\" bukan bilangan; dipakai bawaan {bawaan}");
            return bawaan;
        }

        return n;
    }

    private static readonly HashSet<string> Benar = new(StringComparer.Ordinal) { "true", "1", "ya", "on" };
    private static readonly HashSet<string> Salah = new(StringComparer.Ordinal) { "false", "0", "tidak", "off" };

    /// <summary>Port <c>bool_(env, kunci, bawaan)</c>.</summary>
    public bool Bool(string kunci, bool bawaan)
    {
        var mentah = Value(kunci, bawaan ? "true" : "false").Trim().ToLowerInvariant();
        if (Benar.Contains(mentah))
        {
            return true;
        }

        if (Salah.Contains(mentah))
        {
            return false;
        }

        Warnings.Add($"{kunci}=\"{mentah}\" bukan boolean; dipakai bawaan {bawaan}");
        return bawaan;
    }

    /// <summary>Port <c>daftar(env, kunci, bawaan)</c>.</summary>
    public List<string> List(string kunci, string bawaan) =>
        [.. Value(kunci, bawaan)
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length != 0)];
}
