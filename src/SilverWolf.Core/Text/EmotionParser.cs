using System.Text.RegularExpressions;

namespace SilverWolf.Core.Text;

/// <summary>
/// Port <c>EmotionParser</c> dari <c>@aituber-onair/voice</c>
/// (<c>dist/esm/utils/emotionParser.js</c>).
///
/// Dua detail yang mudah salah dan harus dijaga persis:
/// 1. Bila tidak ada tag, <c>ExtractEmotion</c> mengembalikan teks <b>tanpa</b>
///    dipangkas (<c>{ cleanText: text }</c>), sedangkan <c>CleanEmotionTags</c>
///    selalu memangkas.
/// 2. Regex tag-nya <c>/\[([a-z]+)\]/i</c> — huruf saja, case-insensitive, dan
///    <c>\s*</c> sesudahnya ikut terhapus saat pembersihan.
/// </summary>
public static partial class EmotionParser
{
    /// <summary>Port <c>EMOTION_TAG_REGEX</c>.</summary>
    [GeneratedRegex(@"\[([a-z]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    /// <summary>Port <c>EMOTION_TAG_CLEANUP_REGEX</c>.</summary>
    [GeneratedRegex(@"\[[a-z]+\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex CleanupRegex();

    /// <summary>Hasil <c>extractEmotion</c>: <c>Emotion</c> null bila tidak ada tag.</summary>
    public readonly record struct HasilEmosi(string? Emotion, string CleanText);

    public static HasilEmosi ExtractEmotion(string? teks)
    {
        if (string.IsNullOrEmpty(teks))
        {
            return new HasilEmosi(null, teks ?? string.Empty);
        }

        var match = TagRegex().Match(teks);
        if (!match.Success)
        {
            return new HasilEmosi(null, teks);
        }

        return new HasilEmosi(
            match.Groups[1].Value.ToLowerInvariant(),
            CleanupRegex().Replace(teks, string.Empty).Trim());
    }

    /// <summary>Port <c>cleanEmotionTags</c>.</summary>
    public static string CleanEmotionTags(string? teks) =>
        string.IsNullOrEmpty(teks) ? string.Empty : CleanupRegex().Replace(teks, string.Empty).Trim();

    /// <summary>Port <c>addEmotionTag</c>.</summary>
    public static string AddEmotionTag(string emosi, string teks) => $"[{emosi}] {teks}";

    /// <summary>
    /// Port <c>emotionToTalkStyle</c>. Tidak dipakai aplikasi lama, tetapi
    /// disertakan agar permukaan API pustaka aslinya tetap terwakili.
    /// </summary>
    public static string EmotionToTalkStyle(string? emosi, string fallback = "neutral") =>
        (emosi ?? "neutral").ToLowerInvariant() switch
        {
            "angry" => "angry",
            "happy" => "happy",
            "sad" => "sad",
            "surprised" => "surprised",
            "relaxed" => "talk",
            _ => fallback,
        };
}
