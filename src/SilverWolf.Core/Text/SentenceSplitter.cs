namespace SilverWolf.Core.Text;

/// <summary>
/// Port <c>potongKalimat</c> dari <c>packages/pipelines-audio/src/kalimat.js</c>.
///
/// Dipakai untuk memecah balasan LLM menjadi kalimat yang bisa disintesis
/// berurutan (pipeline suara M11). Aturan "<b>bukan akhir kalimat</b>"
/// (angka seperti "3.14" atau "v1." yang diikuti angka) harus dipertahankan,
/// kalau tidak setiap versi dan desimal akan memotong kalimat.
/// </summary>
public static class SentenceSplitter
{
    /// <summary>Port <c>PENUTUP</c>: . ! ? … 。 ！ ？</summary>
    private const string Penutup = ".!?…。！？";

    /// <summary>Port <c>EKOR</c>: " ' ” ’ ) ] } »</summary>
    private const string Ekor = "\"'”’)]}»";

    /// <summary>Hasil pemotongan: kalimat lengkap + sisa yang belum lengkap.</summary>
    public readonly record struct HasilPotong(List<string> Kalimat, string Sisa);

    /// <summary>Port <c>buangTag</c>: bersihkan tag emosi lalu padatkan spasi ganda.</summary>
    public static string BuangTag(string? teks)
    {
        // .replace(/[ \t]{2,}/g, " ") — run 2+ spasi/tab dipadatkan jadi satu spasi.
        return Padatkan(EmotionParser.CleanEmotionTags(teks));
    }

    /// <summary>Port <c>potongKalimat(teks, minimal = 12)</c>.</summary>
    public static HasilPotong PotongKalimat(string? teks, int minimal = 12) =>
        PotongBersih(BuangTag(teks), minimal);

    private static HasilPotong PotongBersih(string teks, int minimal)
    {
        var kalimat = new List<string>();
        var awal = 0;

        for (var i = 0; i < teks.Length; i++)
        {
            var c = teks[i];
            if (!Penutup.Contains(c))
            {
                continue;
            }

            if (BukanAkhirKalimat(teks, i))
            {
                continue;
            }

            var j = i + 1;
            while (j < teks.Length && Ekor.Contains(teks[j]))
            {
                j++;
            }

            if (j < teks.Length && !char.IsWhiteSpace(teks[j]))
            {
                continue;
            }

            var potongan = teks[awal..j].Trim();
            if (potongan.Length >= minimal || potongan.Any(char.IsWhiteSpace))
            {
                kalimat.Add(potongan);
                awal = j;
            }
        }

        return new HasilPotong(kalimat, teks[awal..].TrimStart());
    }

    /// <summary>Port <c>bukanAkhirKalimat(teks, i)</c>.</summary>
    private static bool BukanAkhirKalimat(string teks, int i)
    {
        if (i - 1 < 0 || teks[i - 1] < '0' || teks[i - 1] > '9')
        {
            return false;
        }

        if (i + 1 < teks.Length && teks[i + 1] >= '0' && teks[i + 1] <= '9')
        {
            return true;
        }

        if (i - 2 < 0)
        {
            return true;
        }

        var duaSebelum = teks[i - 2];
        return duaSebelum == ' ' || duaSebelum == '\n' || duaSebelum == '(';
    }

    /// <summary>Port <c>sisaKalimat</c>.</summary>
    public static string SisaKalimat(string? teks) => (teks ?? string.Empty).Trim();

    private static string Padatkan(string teks)
    {
        var hasil = new System.Text.StringBuilder(teks.Length);
        var spasi = false;
        foreach (var c in teks)
        {
            if (c == ' ' || c == '\t')
            {
                if (spasi)
                {
                    continue;
                }

                spasi = true;
                hasil.Append(' ');
            }
            else
            {
                spasi = false;
                hasil.Append(c);
            }
        }

        return hasil.ToString();
    }
}
