using SilverWolf.Core.Domain;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji mesin cemburu. Yang diperiksa bukan "apakah ada kelasnya", tetapi:
/// apakah nama karakter benar dikenali, apakah salah kenal bisa dicegah,
/// dan apakah nadanya benar-benar melunak — tiga hal yang menentukan apakah
/// fitur ini terasa hidup atau justru mengganggu.
/// </summary>
public class CemburuTests
{
    // ── Pengenalan nama ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Aku suka Kafka, dia keren banget.", "Kafka")]
    [InlineData("Himeko itu kapten yang hebat ya.", "Himeko")]
    [InlineData("Tadi aku mabar sama March 7th.", "March 7th")]
    [InlineData("Black Swan muncul di mimpiku.", "Black Swan")]
    [InlineData("Ruan Mei itu ilmuwan, bukan?", "Ruan Mei")]
    [InlineData("Fu Xuan bisa lihat masa depan.", "Fu Xuan")]
    [InlineData("Stelle itu Trailblazer kan?", "Stelle")]
    public void Sebutan_MengenaliNamaKarakter(string ucapan, string diharapkan)
    {
        Assert.Contains(diharapkan, Cemburu.Sebutan(ucapan));
    }

    [Fact]
    public void Sebutan_DuaKarakterUrutSesuaiKemunculan()
    {
        var hasil = Cemburu.Sebutan("Kafka dan Himeko sama-sama keren.");

        Assert.Equal(["Kafka", "Himeko"], hasil);
    }

    [Fact]
    public void Sebutan_TidakDuplikatNamaYangSamaDuaKali()
    {
        var hasil = Cemburu.Sebutan("Kafka itu keren. Kafka juga baik.");

        Assert.Equal(["Kafka"], hasil);
    }

    [Fact]
    public void Sebutan_DibatasiDuaNamaWalauMenyebutLebihBanyak()
    {
        // Prompt tidak boleh kebanjiran nama; dua sudah cukup untuk memicu nada.
        var hasil = Cemburu.Sebutan("Kafka, Himeko, dan March 7th semuanya keren.");

        Assert.Equal(2, hasil.Count);
    }

    [Fact]
    public void Sebutan_FireflyTidakTermasukKarakterCemburu()
    {
        // persona.md: Firefly satu-satunya yang dibicarakan dengan nada serius.
        // Cemburu soal Firefly akan mematahkan tulisan itu.
        Assert.False(Cemburu.AdaSebutan("Kasihan banget ya Firefly, hidupnya berat."));
        Assert.Empty(Cemburu.Sebutan("Aku suka Firefly."));
    }

    [Fact]
    public void Sebutan_ScrewllumDanHertaTidakTermasuk()
    {
        // Keduanya lawan/boss yang dia hormati, bukan saingan asmara.
        Assert.False(Cemburu.AdaSebutan("Screwllum itu lawan yang jago, aku hormati."));
        Assert.False(Cemburu.AdaSebutan("Herta pernah balikin seranganku."));
    }

    // ── Batas kata (mencegah salah kenal) ───────────────────────────────────

    [Fact]
    public void Sebutan_TidakSalahKenalKataBiasa()
    {
        // "march" adalah kata Inggris biasa (berbaris / Maret). Ini salah
        // kenal yang paling mudah terjadi, jadi diuji langsung.
        Assert.False(Cemburu.AdaSebutan("Kita berbaris march sepanjang jalan."));
        Assert.False(Cemburu.AdaSebutan("Bulan march itu musim semi."));

        // Kalimat biasa tanpa nama karakter apa pun.
        Assert.False(Cemburu.AdaSebutan("Aku sedang membaca buku di kamar."));
    }

    [Theory]
    [InlineData("March 7th itu lucu ya.")]
    [InlineData("Aku suka March 7")]
    [InlineData("March tujuh tuh ramah.")]
    [InlineData("march7th keren")]
    public void Sebutan_MarchKetujuhDikenaliDalamBerbagaiBentuk(string ucapan)
    {
        // "March" sendiri adalah kata biasa, jadi bentuk lengkapnya yang
        // diakui — bukan katanya sendirian.
        Assert.Equal(["March 7th"], Cemburu.Sebutan(ucapan));
    }

    [Fact]
    public void Sebutan_TidakKenaDiDalamKataLain()
    {
        // "asta" ada di dalam "pasta", "robin" ada di dalam "robbing".
        Assert.False(Cemburu.AdaSebutan("Aku mau makan pasta malam ini."));
        Assert.False(Cemburu.AdaSebutan("Kode ini kafkaesque banget ya."));
    }

    [Fact]
    public void Sebutan_TagEmosiTidakMengacaukanPencocokan()
    {
        var hasil = Cemburu.Sebutan("[goda] Kafka ya? Hmph.");

        Assert.Equal(["Kafka"], hasil);
    }

    [Fact]
    public void Sebutan_KapitalKecilBesarTidakBerpengaruh()
    {
        Assert.True(Cemburu.AdaSebutan("KAFKA itu keren."));
        Assert.True(Cemburu.AdaSebutan("kafka itu keren."));
        Assert.True(Cemburu.AdaSebutan("KaFkA itu keren."));
    }

    [Fact]
    public void Sebutan_TeksKosongAtauNullAman()
    {
        Assert.Empty(Cemburu.Sebutan(null));
        Assert.Empty(Cemburu.Sebutan(""));
        Assert.Empty(Cemburu.Sebutan("   "));
    }

    [Fact]
    public void Sebutan_UcapanTanpaNamaKarakterKosong()
    {
        Assert.Empty(Cemburu.Sebutan("Aku capek kerja hari ini, mau istirahat."));
    }

    // ── Prompt ──────────────────────────────────────────────────────────────

    [Fact]
    public void BuatPrompt_MenyebutNamaYangDitemukan()
    {
        var prompt = Cemburu.BuatPrompt(["Kafka"]);

        Assert.Contains("Kafka", prompt, StringComparison.Ordinal);
        Assert.Contains("PACAR", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuatPrompt_TanpaNamaTetapBisaDipakai()
    {
        var prompt = Cemburu.BuatPrompt([]);

        Assert.Contains("karakter cewek lain", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuatPrompt_MelarangMarahDanMelarang()
    {
        var prompt = Cemburu.BuatPrompt(["Kafka"]);

        // Rambu-rambu ini yang mencegah cemburu berubah jadi tidak nyaman.
        Assert.Contains("DILARANG", prompt, StringComparison.Ordinal);
        Assert.Contains("Melarang Master", prompt, StringComparison.Ordinal);
        Assert.Contains("tidak setia", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuatPrompt_MelunakSetelahBeberapaGiliranBerturut()
    {
        var awal = Cemburu.BuatPrompt(["Kafka"], jumlahBerturut: 0);
        var lanjut = Cemburu.BuatPrompt(["Kafka"], jumlahBerturut: Cemburu.BatasBerturut);

        Assert.DoesNotContain("LEDEKAN RINGAN", awal, StringComparison.Ordinal);
        Assert.Contains("LEDEKAN RINGAN", lanjut, StringComparison.Ordinal);
        Assert.Contains("Jangan diulang", lanjut, StringComparison.Ordinal);
    }

    [Fact]
    public void BuatPrompt_SelaluMintaTagEmosi()
    {
        var prompt = Cemburu.BuatPrompt(["Kafka"]);

        Assert.Contains("[goda]", prompt, StringComparison.Ordinal);
        Assert.Contains("tag emosi", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuatPrompt_MengenaliKarakterYangDikagumi()
    {
        var prompt = Cemburu.BuatPrompt(["Kafka"]);

        // Dia boleh mengakui dulu kalau karakternya memang dia hormati.
        Assert.Contains("kagumi", prompt, StringComparison.Ordinal);
    }

    // ── Pelunakan nada ──────────────────────────────────────────────────────

    [Fact]
    public void Intensitas_TurunSeiringGiliranBerturut()
    {
        Assert.Equal(1.0, Cemburu.Intensitas(0), 3);
        Assert.True(Cemburu.Intensitas(1) < Cemburu.Intensitas(0));
        Assert.True(Cemburu.Intensitas(2) < Cemburu.Intensitas(1));
    }

    [Fact]
    public void Intensitas_TidakPernahNolAtauNegatif()
    {
        // Walau berturut-turut lama, nadanya tidak boleh mati total.
        foreach (var n in new[] { 0, 1, 2, 3, 5, 10, 100 })
        {
            Assert.InRange(Cemburu.Intensitas(n), 0.2, 1.0);
        }
    }

    [Fact]
    public void Intensitas_NilaiNegatifDiperlakukanSebagaiAwal()
    {
        Assert.Equal(1.0, Cemburu.Intensitas(-5), 3);
    }

    [Fact]
    public void LabelNada_MenggambarkanTingkatDenganBenar()
    {
        Assert.Contains("penuh", Cemburu.LabelNada(0), StringComparison.Ordinal);
        Assert.Contains("melunak", Cemburu.LabelNada(1), StringComparison.Ordinal);
        Assert.Contains("ringan", Cemburu.LabelNada(3), StringComparison.Ordinal);
    }

    // ── Kalimat cadangan ────────────────────────────────────────────────────

    [Fact]
    public void KalimatCadangan_SelaluBerisiTagEmosiYangSah()
    {
        // Cadangan yang tag-nya tidak dikenal akan mematikan ekspresi wajah.
        for (var n = 0; n < 8; n++)
        {
            var kalimat = Cemburu.KalimatCadangan("Kafka", n);
            var tag = kalimat[1..kalimat.IndexOf(']')];

            Assert.True(EmotionTags.Dikenali(tag), $"tag tidak dikenal pada giliran {n}: {tag}");
        }
    }

    [Fact]
    public void KalimatCadangan_BergantiSehinggaTidakTerasaRekaman()
    {
        // Rotasi hanya berlaku SEBELUM ambang BatasBerturut — setelah itu
        // semuanya melunak ke satu kalimat yang sama, dan itu memang disengaja.
        var unik = Enumerable.Range(0, Cemburu.BatasBerturut)
            .Select(n => Cemburu.KalimatCadangan("Kafka", n))
            .Distinct()
            .Count();

        Assert.Equal(Cemburu.BatasBerturut, unik);
    }

    [Fact]
    public void KalimatCadangan_MelunakSetelahBatasBerturut()
    {
        var lanjut = Cemburu.KalimatCadangan("Kafka", Cemburu.BatasBerturut);

        Assert.Contains("Bukan cemburu ya", lanjut, StringComparison.Ordinal);

        // Nama karakter tidak boleh disebut lagi setelah melunak.
        Assert.DoesNotContain("Kafka", lanjut, StringComparison.Ordinal);
    }

    [Fact]
    public void KalimatCadangan_TidakPernahMemuatPenandaMarkdown()
    {
        // Keluhan Master sebelumnya: TTS membaca "*" sebagai "bintang".
        for (var n = 0; n < 8; n++)
        {
            var kalimat = Cemburu.KalimatCadangan("Kafka", n);

            Assert.DoesNotContain("*", kalimat, StringComparison.Ordinal);
            Assert.DoesNotContain("`", kalimat, StringComparison.Ordinal);
            Assert.DoesNotContain("_", kalimat, StringComparison.Ordinal);
        }
    }

    // ── Ringkasan log ───────────────────────────────────────────────────────

    [Fact]
    public void Ringkas_MenjelaskanKeadaanKeLog()
    {
        Assert.Contains("tidak ada sebutan", Cemburu.Ringkas([]), StringComparison.Ordinal);

        var ada = Cemburu.Ringkas(["Kafka"], 0);
        Assert.Contains("Kafka", ada, StringComparison.Ordinal);
        Assert.Contains("giliran 0", ada, StringComparison.Ordinal);
    }

    // ── Daftar karakter ─────────────────────────────────────────────────────

    [Fact]
    public void Daftar_TidakMemuatKarakterYangJustruDihormati()
    {
        var daftar = Cemburu.Daftar.Select(x => x.ToLowerInvariant()).ToList();

        Assert.DoesNotContain("firefly", daftar);
        Assert.DoesNotContain("screwllum", daftar);
        Assert.DoesNotContain("herta", daftar);
    }

    [Fact]
    public void Daftar_SemuaNamaHurufKecil()
    {
        // Pencocokan memakai OrdinalIgnoreCase, tapi tabel yang bersih
        // menghindari salah baca saat ditinjau manusia.
        foreach (var nama in Cemburu.Daftar)
        {
            Assert.Equal(nama, nama.ToLowerInvariant());
        }
    }
}
