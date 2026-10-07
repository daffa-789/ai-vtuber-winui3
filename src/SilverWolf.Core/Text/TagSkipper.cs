using System.Text.RegularExpressions;

namespace SilverWolf.Core.Text;

/// <summary>
/// Port <c>lewatiTag</c> dari
/// <c>apps/stage-tamagotchi/renderer/src/ucapan.js</c>.
///
/// Dipakai saat teks masih mengalir: menentukan sampai mana awal balasan yang
/// belum boleh diucapkan karena masih berupa tag emosi.
/// Mengembalikan -1 bila tag belum lengkap, 0 bila teks tidak diawali tag.
/// </summary>
public static partial class TagSkipper
{
    [GeneratedRegex(@"^[\s`'""]*")]
    private static partial Regex AwalanRegex();

    public static int LewatiTag(string? teks)
    {
        if (string.IsNullOrEmpty(teks))
        {
            return -1;
        }

        var depan = AwalanRegex().Replace(teks, string.Empty);
        if (depan.Length == 0)
        {
            return -1;
        }

        if (depan[0] != '[')
        {
            return 0;
        }

        var tutup = depan.IndexOf(']');
        if (tutup < 0)
        {
            return -1;
        }

        return teks.Length - depan.Length + tutup + 1;
    }

    /// <summary>Port <c>uraikanEmosi</c>.</summary>
    public static EmotionParser.HasilEmosi UraikanEmosi(string? teks) => EmotionParser.ExtractEmotion(teks);

    /// <summary>Port <c>bersihkanEmosi</c>.</summary>
    public static string BersihkanEmosi(string? teks) => EmotionParser.CleanEmotionTags(teks);
}
