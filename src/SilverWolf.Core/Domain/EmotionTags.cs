namespace SilverWolf.Core.Domain;

/// <summary>
/// Daftar tag emosi yang diakui sistem — port <c>EMOTION_TAGS</c> dari
/// <c>apps/server-node/src/character.js</c>.
///
/// Nilai stringnya sengaja tetap Indonesia: masuk ke prompt sistem, menjadi
/// bagian dari perilaku LLM, dan dipakai UI sebagai nama ekspresi Live2D.
/// </summary>
public static class EmotionTags
{
    public static readonly string[] Semua =
    [
        "netral", "senyum", "semangat", "kaget", "bingung", "lelah", "goda", "sebal", "sedih",
    ];

    private static readonly HashSet<string> Dikenal = new(Semua, StringComparer.OrdinalIgnoreCase);

    public static bool Dikenali(string? tag) => tag is not null && Dikenal.Contains(tag);

    /// <summary>String siap pakai untuk prompt: <c>[netral], [senyum], ...</c></summary>
    public static string UntukPrompt() => string.Join(", ", Semua.Select(t => $"[{t}]"));
}
