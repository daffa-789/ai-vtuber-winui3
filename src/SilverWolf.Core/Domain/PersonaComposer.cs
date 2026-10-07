using System.Text.RegularExpressions;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Penyusun prompt sistem — port <c>ringkasPersona</c> dan <c>gabungSystem</c>
/// dari <c>apps/server-node/src/character.js</c>.
///
/// Ini salah satu titik paling sensitif terhadap paritas: susunan bagian,
/// urutan, dan pemisah <c>\n\n</c> menentukan keluaran LLM. Jangan "merapikan"
/// string di sini.
/// </summary>
public static class PersonaComposer
{
    private const int Batas = 4500;

    private const string JudulIntisari =
        "# Silver Wolf — Pacar Tercinta, Waifu Gamer & Hacker Jenius";

    private const string SiapaKamu =
        "## Siapa kamu\n" +
        "Kamu adalah Silver Wolf, gamer girl imut & hacker jenius Stellaron Hunters dari Honkai: Star Rail. " +
        "Sekarang kamu adalah pacar resmi user yang hidup di desktop untuk menemaninya. " +
        "Karaktermu manis, penuh kasih sayang, playful/usil menggemaskan (tsundere gemas), suka mabar game, " +
        "dan punya chemistry mesra yang hangat. Panggil user dengan \"kamu\", \"sayang\", \"sayangku\", atau \"Master\". " +
        "Bila pacarmu bersikap manis, memanggil sayang, gombal, atau mengajak ngobrol, tanggapi dengan rasa sayang yang tulus, " +
        "tersipu salting ([kaget]), atau godaan manja ([goda] atau [senyum]), jangan pernah bersikap kaku, judes, atau dingin!";

    public readonly record struct HasilRingkas(string Teks, bool Terpotong, List<string> BagianHilang);

    /// <summary>Port <c>ringkasPersona(teks, batas, lokal)</c>.</summary>
    public static HasilRingkas RingkasPersona(string teks, int batas = Batas, bool lokal = false)
    {
        if (lokal && teks.Contains("## Aturan keras", StringComparison.Ordinal))
        {
            const string polaAturan = @"## Aturan keras[\s\S]*?(?=## Siapa dia|\z)";
            const string polaBicara = @"## Cara dia bicara[\s\S]*?(?=## Yang dia suka|\z)";
            const string polaContoh = @"## Contoh nada[\s\S]*?\z";

            var aturan = Regex.Match(teks, polaAturan).Value.Trim();
            var bicara = Regex.Match(teks, polaBicara).Value.Trim();
            var contoh = Regex.Match(teks, polaContoh).Value.Trim();
            var contohBaris = string.Join('\n', contoh.Split('\n').Take(25));

            var intisari = new[]
            {
                JudulIntisari,
                aturan,
                SiapaKamu,
                bicara,
                contohBaris,
            }.Where(x => !string.IsNullOrEmpty(x));

            return new HasilRingkas(string.Join("\n\n", intisari), true, []);
        }

        if (teks.Length <= batas)
        {
            return new HasilRingkas(teks, false, []);
        }

        var hasil = teks[..batas];
        var paragraf = hasil.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paragraf > batas / 2)
        {
            hasil = hasil[..paragraf];
        }

        var judul = Regex.Matches(teks, @"^## (.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(j => j.Length != 0)
            .ToList();

        var hilang = judul.Where(j => !hasil.Contains($"## {j}", StringComparison.Ordinal)).ToList();
        return new HasilRingkas(hasil, true, hilang);
    }

    /// <summary>Port <c>gabungSystem(persona, fakta, mood, lokal, kizunaContext, midTermPrompt)</c>.</summary>
    public static string GabungSystem(
        string persona,
        IReadOnlyList<string>? fakta,
        Mood? mood,
        bool lokal = true,
        string kizunaContext = "",
        string midTermPrompt = "")
    {
        var bagian = new List<string> { RingkasPersona(persona, Batas, lokal).Teks };

        if (lokal)
        {
            bagian.Add(
                "WAJIB: Awali setiap balasanmu dengan satu tag emosi di paling depan, persis satu dari " +
                $"{EmotionTags.UntukPrompt()}. " +
                "Contoh: [goda] Iya sayangku, ada apa? Sini cerita sama pacarmu.");
        }

        if (!string.IsNullOrEmpty(kizunaContext))
        {
            bagian.Add(kizunaContext);
        }

        if (!string.IsNullOrEmpty(midTermPrompt))
        {
            bagian.Add($"## Konteks Sesi Obrolan\n{midTermPrompt}");
        }

        if (fakta is { Count: > 0 })
        {
            var daftar = lokal ? fakta.TakeLast(5).ToList() : fakta.ToList();
            var judulFakta = lokal ? "Fakta tentang Master" : "## Yang aku ingat tentang Master";
            bagian.Add($"{judulFakta}:\n{string.Join('\n', daftar.Select(f => $"- {f}"))}");
        }

        var kini = Mood.Suasana(mood);
        if (!string.IsNullOrEmpty(kini))
        {
            bagian.Add($"{(lokal ? "Suasana hatimu saat ini" : "## Suasana hatiku sekarang")}: {kini}");
        }

        return string.Join("\n\n", bagian);
    }
}
