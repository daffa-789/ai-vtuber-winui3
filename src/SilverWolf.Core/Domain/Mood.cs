namespace SilverWolf.Core.Domain;

/// <summary>
/// Suasana hati karakter — port <c>MOOD_AWAL</c>, <c>NILAI_TAG</c>,
/// <c>perbaruiMood</c>, dan <c>suasana</c> dari
/// <c>apps/server-node/src/character.js</c>.
///
/// Semua rumus di sini menentukan kalimat suasana yang disisipkan ke prompt
/// sistem, jadi angkanya tidak boleh "dibulatkan" atau disederhanakan.
/// </summary>
public sealed class Mood
{
    public double Valensi { get; set; }
    public double Energi { get; set; }
    public double Afinitas { get; set; }
    public int Pertukaran { get; set; }
    public string? Alasan { get; set; }

    public static Mood Awal => new()
    {
        Valensi = 0.6,
        Energi = 0.8,
        Afinitas = 0.9,
        Pertukaran = 0,
    };

    private static readonly Dictionary<string, double> NilaiTag = new(StringComparer.Ordinal)
    {
        ["senyum"] = 0.25,
        ["semangat"] = 0.35,
        ["goda"] = 0.3,
        ["netral"] = 0.05,
        ["bingung"] = 0,
        ["kaget"] = 0.1,
        ["lelah"] = -0.05,
        ["sedih"] = -0.1,
        ["sebal"] = -0.05,
    };

    private const double BaselineEnergi = 0.8;

    private static double Jepit(double n, double min, double maks) => Math.Min(maks, Math.Max(min, n));

    /// <summary>Port <c>perbaruiMood(lama, tag)</c>.</summary>
    public static Mood Perbarui(Mood? lama, string? tag)
    {
        var dasar = lama ?? Awal;
        var delta = NilaiTag.TryGetValue(tag ?? "netral", out var d) ? d : 0;
        var naikEnergi = tag is "semangat" or "goda" ? 0.1 : 0.0;

        return new Mood
        {
            Valensi = Jepit((dasar.Valensi * 0.7) + (0.3 * 0.5) + (delta * 0.3), -0.4, 1),
            // Energi tidak mengering ke 0, selalu kembali ke baseline yang hidup
            Energi = Jepit((dasar.Energi * 0.8) + (BaselineEnergi * 0.2) + naikEnergi, 0.4, 1),
            Afinitas = Jepit(dasar.Afinitas + 0.02, 0.4, 1),
            Pertukaran = dasar.Pertukaran + 1,
            Alasan = $"tag terakhir: {tag ?? "tidak ada"}",
        };
    }

    /// <summary>Port <c>suasana(mood)</c>. Mengembalikan string kosong bila mood null.</summary>
    public static string Suasana(Mood? mood)
    {
        if (mood is null)
        {
            return string.Empty;
        }

        if (mood.Valensi > 0.4 && mood.Energi > 0.6)
        {
            return "Kamu lagi dalam mood sangat bahagia dan ceria, suka bermanja dan menggoda pacarmu dengan manis dan usil.";
        }

        if (mood.Valensi > 0.2)
        {
            return "Kamu lagi santai, sayang banget sama pacarmu, dan senang ngobrol atau mabar bareng.";
        }

        if (mood.Valensi < -0.2)
        {
            return "Kamu lagi agak cemberut menggemaskan (tsundere manja), pengen diperhatiin dan disayang sama pacarmu.";
        }

        return "Kamu santai, manis, penuh perhatian, dan senang menemani pacarmu.";
    }
}
