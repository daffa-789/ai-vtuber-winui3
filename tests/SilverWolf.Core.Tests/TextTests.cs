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

    // ── TeksUcapan ──────────────────────────────────────────────────────────
    // Keluhan Master: "kaya ada * dia malah mengatakan bintang". LLM menulis
    // penekanan Markdown, TTS membacakan tanda bintangnya.

    [Fact]
    public void Bersihkan_MembuangTandaBintangTunggal()
    {
        Assert.Equal("Jadi kamu kangen Kafka?",
            TeksUcapan.Bersihkan("Jadi kamu kangen *Kafka*?"));
    }

    [Fact]
    public void Bersihkan_MembuangTandaBintangGanda()
    {
        Assert.Equal("aku cemburu", TeksUcapan.Bersihkan("aku **cemburu**"));
    }

    [Fact]
    public void Bersihkan_MempertahankanIsiKata()
    {
        // Yang dibuang HANYA penandanya — isinya tidak boleh hilang.
        Assert.Equal("partner", TeksUcapan.Bersihkan("*partner*"));
    }

    [Fact]
    public void Bersihkan_MembuangBacktick()
    {
        Assert.Equal("firewall", TeksUcapan.Bersihkan("`firewall`"));
    }

    [Fact]
    public void Bersihkan_MembuangGarisBawahMiring()
    {
        Assert.Equal("penting", TeksUcapan.Bersihkan("_penting_"));
    }

    [Fact]
    public void Bersihkan_MembuangJudulDanButirDaftar()
    {
        Assert.Equal("Daftar\nsatu\ndua",
            TeksUcapan.Bersihkan("## Daftar\n- satu\n- dua"));
    }

    [Fact]
    public void Bersihkan_MembuangNomorDaftar()
    {
        Assert.Equal("pertama\nkedua",
            TeksUcapan.Bersihkan("1. pertama\n2. kedua"));
    }

    [Fact]
    public void Bersihkan_MengubahTautanJadiJudulnya()
    {
        Assert.Equal("lihat wiki resminya",
            TeksUcapan.Bersihkan("lihat [wiki resminya](https://example.com)"));
    }

    [Fact]
    public void Bersihkan_EmDashJadiKoma()
    {
        // TTS kadang membaca em dash sebagai "strip" atau diam terlalu lama;
        // koma memberi jeda yang benar tanpa suara aneh.
        Assert.Equal("aku tunggu, tapi kamu tak datang",
            TeksUcapan.Bersihkan("aku tunggu — tapi kamu tak datang"));
    }

    [Fact]
    public void Bersihkan_MembuangSimbolHias()
    {
        Assert.Equal("satu dua", TeksUcapan.Bersihkan("• satu ▪ dua"));
    }

    [Fact]
    public void Bersihkan_MempertahankanTandaBacaIntonasi()
    {
        // Justru tanda baca ini yang memberi intonasi TTS — jangan dihilangkan.
        const string asli = "Hmph! Kamu kangen Kafka, bukan aku? Aku cemburu...";
        Assert.Equal(asli, TeksUcapan.Bersihkan(asli));
    }

    [Fact]
    public void Bersihkan_MenanganiBintangTidakBerpasangan()
    {
        // Balasan streaming bisa terpotong di tengah penanda.
        Assert.Equal("aku cemburu tahu", TeksUcapan.Bersihkan("aku **cemburu* tahu"));
    }

    [Fact]
    public void Bersihkan_MembersihkanGabunganSemuaPenanda()
    {
        var hasil = TeksUcapan.Bersihkan(
            "## Jawaban\nJadi kamu kangen *Kafka*, bukan **aku**? Lihat `catatan` — ini penting. •");
        Assert.DoesNotContain('*', hasil);
        Assert.DoesNotContain('`', hasil);
        Assert.DoesNotContain('#', hasil);
        Assert.DoesNotContain('—', hasil);
        Assert.Contains("Kafka", hasil);
        Assert.Contains("aku", hasil);
    }

    [Fact]
    public void Bersihkan_SpasiSebelumTandaBacaDirapikan()
    {
        Assert.Equal("halo, sayang.", TeksUcapan.Bersihkan("halo , sayang ."));
    }

    [Fact]
    public void Bersihkan_TeksKosongTetapKosong()
    {
        Assert.Equal(string.Empty, TeksUcapan.Bersihkan(null));
        Assert.Equal(string.Empty, TeksUcapan.Bersihkan("   "));
    }

    [Fact]
    public void BersihkanPerKalimat_MemotongLaluMembersihkan()
    {
        var hasil = TeksUcapan.BersihkanPerKalimat(
            "[goda] Jadi kamu kangen *Kafka*? Hmph, aku **cemburu** tahu.");

        Assert.Equal(["Jadi kamu kangen Kafka?", "Hmph, aku cemburu tahu."], hasil);
    }

    [Fact]
    public void AdaMarkdown_MendeteksiPenanda()
    {
        Assert.True(TeksUcapan.AdaMarkdown("*tebal*"));
        Assert.True(TeksUcapan.AdaMarkdown("ada — ini"));
        Assert.False(TeksUcapan.AdaMarkdown("teks biasa, tanpa penanda!"));
    }

    // ── Intonasi ──────────────────────────────────────────────────────────
    // Tabel tempo adalah satu-satunya sumber intonasi. Kalau angkanya bergeser
    // tanpa disengaja, suara Silver Wolf berubah karakter — uji ini mengunci
    // urutannya: gembira paling cepat, lelah paling lambat.

    [Theory]
    [InlineData("semangat", 0.80)]
    [InlineData("kaget", 0.83)]
    [InlineData("goda", 0.86)]
    [InlineData("senyum", 0.88)]
    [InlineData("netral", 0.90)]
    [InlineData("bingung", 0.99)]
    [InlineData("sebal", 1.02)]
    [InlineData("sedih", 1.10)]
    [InlineData("lelah", 1.15)]
    public void Tempo_SesuaiTabel(string emosi, double diharapkan)
    {
        Assert.Equal(diharapkan, Intonasi.Tempo(emosi), 3);
    }

    [Theory]
    [InlineData("semangat")]
    [InlineData("SEMANGAT")]
    [InlineData("  senyum  ")]
    public void Tempo_NormalisasiHurufBesarDanSpasi(string emosi)
    {
        Assert.True(Intonasi.Dikenali(emosi));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("emosi-yang-tidak-ada")]
    public void Tempo_EmosiTidakDikenalPakaiBawaan(string? emosi)
    {
        Assert.False(Intonasi.Dikenali(emosi));
        Assert.Equal(Intonasi.TempoBawaan, Intonasi.Tempo(emosi), 3);
    }

    [Fact]
    public void Tempo_SelaluDalamBatasAman()
    {
        // Di luar rentang ini Piper terdengar terburu-buru atau tidur.
        foreach (var emosi in new[]
                 {
                     "semangat", "kaget", "goda", "senyum", "netral",
                     "bingung", "sebal", "sedih", "lelah",
                 })
        {
            var nilai = Intonasi.Tempo(emosi);
            Assert.InRange(nilai, Intonasi.TempoMinimum, Intonasi.TempoMaksimum);
        }
    }

    [Fact]
    public void BeriJeda_KalimatPanjangDapatJedaTambahan()
    {
        var teks = "Aku sudah bilang berkali-kali kalau aku tidak suka menunggu. "
                 + "Kamu selalu saja mengulanginya!";

        var hasil = Intonasi.BeriJeda(teks, "sedih");

        // Kalimat panjang (> 60 karakter) selalu dapat jeda sesudah titik.
        Assert.NotEqual(teks, hasil);
        Assert.Contains("menunggu.  ", hasil);
    }

    [Fact]
    public void BeriJeda_KalimatPendekTidakDiubah()
    {
        // Ambang 60 karakter: kalimat pendek tidak perlu jeda buatan.
        var teks = "Aku bosan.";
        Assert.Equal(teks, Intonasi.BeriJeda(teks, "lelah"));
    }

    [Fact]
    public void BeriJeda_JedaTitikBerlakuUntukSemuaEmosi()
    {
        // Jeda sesudah titik bukan soal tempo — kalimat panjang terdengar
        // mengalir rata kalau tidak diberi napas, apa pun emosinya.
        // Perhatikan: jeda disisipkan SESUDAH tanda baca yang diikuti spasi,
        // jadi tanda di akhir kalimat tidak tersentuh.
        var teks = "Kamu tahu tidak, aku sudah menunggu di sini sejak lama sekali. "
                 + "Ayo main sekarang! Lagi!";

        var hasil = Intonasi.BeriJeda(teks, "semangat");

        Assert.Contains(".  ", hasil);
        Assert.Contains("!  Lagi!", hasil);
    }

    [Fact]
    public void BeriJeda_KomaDiberiJedaHanyaSaatTempoLambat()
    {
        var teks = "Dengar, aku tidak sedang bercanda denganmu soal hal ini, oke?";

        Assert.Contains(",  ", Intonasi.BeriJeda(teks, "sedih"));
        Assert.DoesNotContain(",  ", Intonasi.BeriJeda(teks, "semangat"));
    }

    [Fact]
    public void BeriJeda_MempertahankanIsiTeks()
    {
        var teks = "Aku tahu kamu menyembunyikan sesuatu dariku, dan aku tidak suka itu. "
                 + "Katakan yang sebenarnya sekarang!";

        var hasil = Intonasi.BeriJeda(teks, "sebal");

        // Hanya spasi yang boleh berubah — tidak ada kata yang hilang.
        Assert.Equal(
            teks.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            hasil.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void BeriJeda_TeksKosongTetapKosong()
    {
        Assert.Equal(string.Empty, Intonasi.BeriJeda(null, "sedih"));
        Assert.Equal(string.Empty, Intonasi.BeriJeda("", "sedih"));
    }
}
