using System.Globalization;

namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Penyusun teks konteks ikatan untuk prompt — port <c>BondContextBuilder</c>
/// dari <c>@aituber-onair/kizuna</c> (<c>dist/context/BondContextBuilder.js</c>).
///
/// Aplikasi memakai <c>defaultLanguage = "en"</c>, jadi hasilnya selalu kalimat
/// bahasa Inggris yang disisipkan sebagai baris "Raw Context" di prompt sistem.
/// Template <c>ja</c> ikut dipertahankan supaya port tidak menyempitkan pustaka.
/// </summary>
public static class BondContextBuilder
{
    private const int MaksEmosi = 3;

    public static string Build(BondRawSnapshot snapshot, string language = "en")
    {
        var emosi = snapshot.FavoriteEmotions.Take(MaksEmosi).ToList();
        return language == "ja"
            ? BuildJa(snapshot, emosi)
            : BuildEn(snapshot, emosi);
    }

    private static string BuildEn(BondRawSnapshot s, List<(string Emotion, int Count)> emosi)
    {
        var warmth = s.Warmth.ToString("0.00", CultureInfo.InvariantCulture);
        var aktifScar = s.Scars
            .Where(x => x.HealedAt is null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var pencapaian = s.Achievements.OrderByDescending(x => x.EarnedAt).FirstOrDefault();
        var daftarEmosi = string.Join(", ", emosi.Select(e => e.Emotion));

        var bagian = new List<string>
        {
            $"Bond with {s.DisplayName}: {s.Stage} (level {s.Level}, {FormatAngka(s.Points)} points).",
            $"Trend: {FormatTren(s.Trend, "en")}; current atmosphere: {FormatAtmosfer(s.Atmosphere, "en")} (warmth {warmth}).",
            $"Continuity: {s.Streak} buckets.",
        };

        if (aktifScar is not null)
        {
            bagian.Add($"Recent scar: {aktifScar.Summary}; repair still needs a sustained calm pattern.");
        }
        else if (pencapaian is not null)
        {
            bagian.Add($"Recent milestone: {pencapaian.Title}.");
        }

        if (daftarEmosi.Length != 0)
        {
            bagian.Add($"Favorite emotions: {daftarEmosi}.");
        }

        bagian.Add("Respond in a way that fits this bond depth and atmosphere without inducing guilt.");
        return string.Join(' ', bagian.Where(x => x.Length != 0));
    }

    private static string BuildJa(BondRawSnapshot s, List<(string Emotion, int Count)> emosi)
    {
        var warmth = s.Warmth.ToString("0.00", CultureInfo.InvariantCulture);
        var aktifScar = s.Scars
            .Where(x => x.HealedAt is null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var pencapaian = s.Achievements.OrderByDescending(x => x.EarnedAt).FirstOrDefault();
        var daftarEmosi = string.Join(", ", emosi.Select(e => e.Emotion));

        var bagian = new List<string>
        {
            $"{s.DisplayName}との絆: {s.Stage}（レベル{s.Level}、{FormatAngka(s.Points)}ポイント）。",
            $"関係の流れ: {FormatTren(s.Trend, "ja")}。現在の空気: {FormatAtmosfer(s.Atmosphere, "ja")}（温かさ{warmth}）。",
            $"継続: {s.Streak}バケット。",
        };

        if (aktifScar is not null)
        {
            bagian.Add($"最近の傷: {aktifScar.Summary}。仲直りには穏やかな接触の積み重ねが必要です。");
        }
        else if (pencapaian is not null)
        {
            bagian.Add($"最近の節目: {pencapaian.Title}。");
        }

        if (daftarEmosi.Length != 0)
        {
            bagian.Add($"好みの感情: {daftarEmosi}。");
        }

        bagian.Add("この関係性の深さと現在の空気に合わせ、罪悪感を促さずに応答してください。");
        return string.Join(' ', bagian.Where(x => x.Length != 0));
    }

    /// <summary>
    /// JS mencetak angka dengan <c>String(number)</c>, jadi 4 ditulis "4" bukan
    /// "4.0". Format ini meniru itu agar prompt identik.
    /// </summary>
    private static string FormatAngka(double nilai) =>
        nilai == Math.Floor(nilai) && Math.Abs(nilai) < 1e15
            ? ((long)nilai).ToString(CultureInfo.InvariantCulture)
            : nilai.ToString("0.############", CultureInfo.InvariantCulture);

    private static string FormatTren(string tren, string bahasa) => bahasa == "ja"
        ? tren switch
        {
            "rising" => "育っている",
            "steady" => "安定している",
            "falling" => "悪化している",
            _ => "修復している",
        }
        : tren switch
        {
            "rising" => "rising",
            "steady" => "steady",
            "falling" => "falling",
            _ => "repairing",
        };

    private static string FormatAtmosfer(string atmosfer, string bahasa) => bahasa == "ja"
        ? atmosfer switch
        {
            "warm" => "温かい",
            "neutral" => "落ち着いている",
            "cool" => "冷えている",
            _ => "かなり冷えている",
        }
        : atmosfer switch
        {
            "warm" => "warm",
            "neutral" => "neutral",
            "cool" => "cool",
            _ => "cold",
        };
}
