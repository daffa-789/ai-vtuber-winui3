using SilverWolf.Core.Text;

namespace SilverWolf.Core.Tests;

public class TextTests
{
    // ── EmotionParser ───────────────────────────────────────────────────────

    [Fact]
    public void EkstrakEmosi_TagApaSajaDiterima()
    {
        var hasil = EmotionParser.ExtractEmotion("[GODA] Halo sayang");

        Assert.Equal("goda", hasil.Emotion);
        Assert.Equal("Halo sayang", hasil.CleanText);
    }

    [Fact]
    public void EkstrakEmosi_TanpaTagTidakMemangkasTeks()
    {
        // Perilaku pustaka asli: { cleanText: text } — tanpa trim().
        var hasil = EmotionParser.ExtractEmotion("  Halo  ");

        Assert.Null(hasil.Emotion);
        Assert.Equal("  Halo  ", hasil.CleanText);
    }

    [Fact]
    public void EkstrakEmosi_TagDiTengahTetapTerbaca()
    {
        var hasil = EmotionParser.ExtractEmotion("Halo [senyum] sayang");

        Assert.Equal("senyum", hasil.Emotion);
    }

    [Fact]
    public void BersihkanTag_SelaluMemangkas()
    {
        Assert.Equal("Halo", EmotionParser.CleanEmotionTags("[kaget] Halo"));
        Assert.Equal("Halo", EmotionParser.CleanEmotionTags("  Halo  "));
    }

    [Fact]
    public void TambahTag_SesuaiFormat()
    {
        Assert.Equal("[goda] Halo", EmotionParser.AddEmotionTag("goda", "Halo"));
    }

    // ── TagSkipper ──────────────────────────────────────────────────────────

    [Fact]
    public void LewatiTag_TeksDiawaliSpasi()
    {
        Assert.Equal(10, TagSkipper.LewatiTag("  [senyum] Halo"));
    }

    [Fact]
    public void LewatiTag_TanpaTagKembaliNol()
    {
        Assert.Equal(0, TagSkipper.LewatiTag("Halo sayang"));
    }

    [Fact]
    public void LewatiTag_TagBelumLengkapKembaliMinusSatu()
    {
        Assert.Equal(-1, TagSkipper.LewatiTag("[senyum"));
    }

    [Fact]
    public void LewatiTag_TeksKosongKembaliMinusSatu()
    {
        Assert.Equal(-1, TagSkipper.LewatiTag(string.Empty));
    }

    // ── SentenceSplitter ────────────────────────────────────────────────────

    [Fact]
    public void PotongKalimat_MemisahDuaKalimatDanMenyisakanYangBelumLengkap()
    {
        var hasil = SentenceSplitter.PotongKalimat("Halo Master. Apa kabar? Baik.");

        Assert.Equal(["Halo Master.", "Apa kabar?"], hasil.Kalimat);
        Assert.Equal("Baik.", hasil.Sisa);
    }

    [Fact]
    public void PotongKalimat_DesimalTidakMemotongKalimat()
    {
        var hasil = SentenceSplitter.PotongKalimat("Versi 3.14 sudah keluar.");

        Assert.Equal(["Versi 3.14 sudah keluar."], hasil.Kalimat);
        Assert.Equal(string.Empty, hasil.Sisa);
    }

    [Fact]
    public void PotongKalimat_NomorVersiDiikutiAngkaTidakMemotong()
    {
        var hasil = SentenceSplitter.PotongKalimat("Rilis v1.2 dirilis hari ini.");

        // "v1." diikuti angka → bukan akhir kalimat.
        Assert.Equal(["Rilis v1.2 dirilis hari ini."], hasil.Kalimat);
    }

    [Fact]
    public void PotongKalimat_MenghapusTagEmosi()
    {
        var hasil = SentenceSplitter.PotongKalimat("[senyum] Halo Master. Apa kabar?");

        Assert.Equal(["Halo Master.", "Apa kabar?"], hasil.Kalimat);
    }

    [Fact]
    public void PotongKalimat_MemadatkanSpasiGanda()
    {
        var hasil = SentenceSplitter.PotongKalimat("Halo    Master.     Apa kabar?");

        Assert.Equal(["Halo Master.", "Apa kabar?"], hasil.Kalimat);
    }

    [Fact]
    public void PotongKalimat_TandaBacaAsiaDiakui()
    {
        var hasil = SentenceSplitter.PotongKalimat("Halo Master。 Apa kabar！");

        Assert.Equal(["Halo Master。", "Apa kabar！"], hasil.Kalimat);
    }

    [Fact]
    public void BuangTag_MemadatkanTabDanSpasi()
    {
        Assert.Equal("Halo Master", SentenceSplitter.BuangTag("[netral] Halo \t\tMaster"));
    }
}
