using SilverWolf.Core.Domain;
using SilverWolf.Core.Domain.Kizuna;

namespace SilverWolf.Core.Tests;

public class DomainTests
{
    // ── Mood ────────────────────────────────────────────────────────────────

    [Fact]
    public void MoodAwal_SesuaiKonstantaAplikasi()
    {
        Assert.Equal(0.6, Mood.Awal.Valensi, 6);
        Assert.Equal(0.8, Mood.Awal.Energi, 6);
        Assert.Equal(0.9, Mood.Awal.Afinitas, 6);
        Assert.Equal(0, Mood.Awal.Pertukaran);
    }

    [Fact]
    public void PerbaruiMood_SenyumMenghasilkanRumusYangSama()
    {
        var mood = Mood.Perbarui(Mood.Awal, "senyum");

        // valensi = 0.6*0.7 + 0.3*0.5 + 0.25*0.3
        Assert.Equal(0.645, mood.Valensi, 6);
        // energi  = 0.8*0.8 + 0.8*0.2 + 0
        Assert.Equal(0.8, mood.Energi, 6);
        // afinitas = 0.9 + 0.02
        Assert.Equal(0.92, mood.Afinitas, 6);
        Assert.Equal(1, mood.Pertukaran);
        Assert.Equal("tag terakhir: senyum", mood.Alasan);
    }

    [Fact]
    public void PerbaruiMood_GodaMenambahEnergi()
    {
        var mood = Mood.Perbarui(Mood.Awal, "goda");

        Assert.Equal(0.9, mood.Energi, 6);
    }

    [Fact]
    public void PerbaruiMood_SedihMenurunkanValensi()
    {
        var mood = Mood.Perbarui(Mood.Awal, "sedih");

        Assert.Equal(0.54, mood.Valensi, 6);
    }

    [Fact]
    public void PerbaruiMood_TagTidakDikenalDeltaNol()
    {
        // NILAI_TAG[tag] ?? 0 — tag di luar daftar memberi delta 0, BUKAN
        // delta "netral" (0.05). Ini beda halus yang mudah tertukar.
        var mood = Mood.Perbarui(Mood.Awal, "tersipu");

        Assert.Equal(0.57, mood.Valensi, 6);
        Assert.NotEqual(Mood.Perbarui(Mood.Awal, "netral").Valensi, mood.Valensi);
    }

    [Fact]
    public void PerbaruiMood_TanpaTagPakaiNetral()
    {
        var mood = Mood.Perbarui(Mood.Awal, null);

        Assert.Equal("tag terakhir: tidak ada", mood.Alasan);
    }

    [Fact]
    public void PerbaruiMood_MoodNullPakaiNilaiAwal()
    {
        var mood = Mood.Perbarui(null, "netral");

        Assert.Equal(1, mood.Pertukaran);
    }

    [Theory]
    [InlineData(0.5, 0.7, "sangat bahagia")]
    [InlineData(0.3, 0.7, "santai, sayang banget")]
    [InlineData(-0.3, 0.7, "cemberut")]
    [InlineData(0.1, 0.7, "menemani")]
    public void Suasana_MemilihKalimatBerdasarkanAmbang(double valensi, double energi, string potongan)
    {
        var teks = Mood.Suasana(new Mood { Valensi = valensi, Energi = energi });

        Assert.Contains(potongan, teks, StringComparison.Ordinal);
    }

    [Fact]
    public void Suasana_MoodNullKembaliKosong()
    {
        Assert.Equal(string.Empty, Mood.Suasana(null));
    }

    // ── PersonaComposer ─────────────────────────────────────────────────────

    private const string PersonaContoh =
        "# Silver Wolf\n" +
        "## Aturan keras\nJangan pernah keluar dari karakter.\nJangan mengaku sebagai AI.\n" +
        "## Siapa dia\nHacker jenius.\n" +
        "## Cara dia bicara\nManis dan usil.\n" +
        "## Yang dia suka\nGame.\n" +
        "## Contoh nada\n[senyum] Iya sayang.\n";

    [Fact]
    public void RingkasPersona_LokalMemakaiIntisari()
    {
        var hasil = PersonaComposer.RingkasPersona(PersonaContoh, 4500, lokal: true);

        Assert.True(hasil.Terpotong);
        Assert.Contains("# Silver Wolf — Pacar Tercinta", hasil.Teks, StringComparison.Ordinal);
        Assert.Contains("## Aturan keras", hasil.Teks, StringComparison.Ordinal);
        Assert.Contains("## Siapa kamu", hasil.Teks, StringComparison.Ordinal);
        Assert.Contains("## Cara dia bicara", hasil.Teks, StringComparison.Ordinal);
        Assert.DoesNotContain("## Siapa dia", hasil.Teks, StringComparison.Ordinal);
        Assert.Empty(hasil.BagianHilang);
    }

    [Fact]
    public void RingkasPersona_TanpaAturanKerasDipotongDiBatas()
    {
        var panjang = new string('a', 600) + "\n\n" + new string('b', 600);
        var hasil = PersonaComposer.RingkasPersona(panjang, 4500, lokal: true);

        Assert.False(hasil.Terpotong);
        Assert.Equal(panjang, hasil.Teks);
    }

    [Fact]
    public void RingkasPersona_MelaporkanBagianYangHilang()
    {
        var teks = "## Satu\n" + new string('a', 100) + "\n## Dua\n" + new string('b', 100);
        var hasil = PersonaComposer.RingkasPersona(teks, 50, lokal: false);

        Assert.True(hasil.Terpotong);
        Assert.Contains("Dua", hasil.BagianHilang);
    }

    [Fact]
    public void GabungSystem_LokalMemaksaTagEmosiDanMembatasiFakta()
    {
        var fakta = Enumerable.Range(1, 7).Select(i => $"Fakta {i}").ToList();

        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, fakta, new Mood { Valensi = 0.6, Energi = 0.8 }, lokal: true);

        Assert.Contains("WAJIB: Awali setiap balasanmu", sistem, StringComparison.Ordinal);
        Assert.Contains("Fakta tentang Master:", sistem, StringComparison.Ordinal);
        Assert.Contains("- Fakta 7", sistem, StringComparison.Ordinal);
        Assert.DoesNotContain("- Fakta 2", sistem, StringComparison.Ordinal); // hanya 5 terakhir
        Assert.Contains("Suasana hatimu saat ini", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_NonLokalMemakaiJudulBerbedaDanSemuaFakta()
    {
        var fakta = Enumerable.Range(1, 7).Select(i => $"Fakta {i}").ToList();

        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, fakta, null, lokal: false);

        Assert.DoesNotContain("WAJIB:", sistem, StringComparison.Ordinal);
        Assert.Contains("## Yang aku ingat tentang Master:", sistem, StringComparison.Ordinal);
        Assert.Contains("- Fakta 1", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_MenyisipkanKonteksKizunaDanMidTerm()
    {
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, [], null, lokal: true,
            kizunaContext: "## Hubungan & Ikatan (Kizuna Level 1)",
            midTermPrompt: "Sedang membicarakan game.");

        Assert.Contains("## Hubungan & Ikatan", sistem, StringComparison.Ordinal);
        Assert.Contains("## Konteks Sesi Obrolan", sistem, StringComparison.Ordinal);
    }

    // ── Cemburu ─────────────────────────────────────────────────────────────

    [Fact]
    public void GabungSystem_AturanCemburuSelaluAdaDiModeLokal()
    {
        // Aturan ini HARUS disuntik dari kode, bukan hanya ditulis di
        // persona.md: persona dipotong pada 4500 karakter sedangkan bagian
        // "Hubungan asmara" ada di ~11.500, jadi salinan di berkas persona
        // tidak pernah sampai ke model pada mode lokal.
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, [], null, lokal: true);

        Assert.Contains("SOAL CEMBURU", sistem, StringComparison.Ordinal);
        Assert.Contains("PACAR, bukan asisten", sistem, StringComparison.Ordinal);
        Assert.Contains("Firefly", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_AturanCemburuTetapAdaWalauPersonaTerpotong()
    {
        // Persona sengaja dibuat panjang supaya RingkasPersona memotongnya.
        var personaPanjang = string.Join("\n\n",
            "## Siapa kamu\nAku Silver Wolf.",
            new string('x', 6000));

        var sistem = PersonaComposer.GabungSystem(
            personaPanjang, [], null, lokal: true);

        Assert.Contains("SOAL CEMBURU", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_KonteksCemburuDisisipkanSaatAda()
    {
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, [], null, lokal: true,
            konteksCemburu: "[KONDISI: Master sedang membicarakan Kafka kepadamu]");

        Assert.Contains("Master sedang membicarakan Kafka", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_TanpaKonteksCemburuTidakAdaBlokKondisi()
    {
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, [], null, lokal: true);

        Assert.DoesNotContain("[KONDISI:", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_KonteksCemburuTidakMenghapusAturanSuara()
    {
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, [], null, lokal: true,
            konteksCemburu: "[KONDISI: Master sedang membicarakan Kafka kepadamu]");

        Assert.Contains("ATURAN SUARA", sistem, StringComparison.Ordinal);
        Assert.Contains("SOAL CEMBURU", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public void GabungSystem_MenyisipkanKonteksAlatTepatSebelumFakta()
    {
        var sistem = PersonaComposer.GabungSystem(
            PersonaContoh, ["Suka kopi hitam"], null, lokal: true,
            konteksAlat: "## Jam & tanggal mesin (alat: waktu)\n- Hari ini: Jumat, 9 Oktober 2026");

        Assert.Contains("## Jam & tanggal mesin", sistem, StringComparison.Ordinal);
        Assert.Contains("Jumat, 9 Oktober 2026", sistem, StringComparison.Ordinal);

        // Alat harus terbaca berurutan dengan fakta tentang Master.
        Assert.True(
            sistem.IndexOf("## Jam & tanggal mesin", StringComparison.Ordinal)
            < sistem.IndexOf("- Suka kopi hitam", StringComparison.Ordinal));
    }

    [Fact]
    public void GabungSystem_TanpaAlatTidakMenambahApaPun()
    {
        var sistem = PersonaComposer.GabungSystem(PersonaContoh, [], null, lokal: true);

        Assert.DoesNotContain("alat:", sistem, StringComparison.Ordinal);
    }

    // ── ChatHistory ─────────────────────────────────────────────────────────

    [Fact]
    public void RapikanRiwayat_MenormalisasiPeran()
    {
        var hasil = ChatHistory.Rapikan(
        [
            new ChatMessage("model", "halo"),
            new ChatMessage("user", "hai"),
            new ChatMessage("aneh", "hei"),
        ]);

        Assert.Equal(["assistant", "user", "user"], hasil.Select(m => m.Role));
    }

    [Fact]
    public void RapikanRiwayat_MembuangPesanKosong()
    {
        var hasil = ChatHistory.Rapikan(
        [
            new ChatMessage("user", "   "),
            new ChatMessage("user", "isi"),
        ]);

        Assert.Single(hasil);
    }

    [Fact]
    public void RapikanRiwayat_MemakaiBagianBilaKontenKosong()
    {
        var hasil = ChatHistory.Rapikan([new ChatMessage { Role = "user", Parts = ["satu", "dua"] }]);

        Assert.Equal("satu dua", Assert.Single(hasil).Content);
    }

    [Fact]
    public void RapikanRiwayat_MemotongKontenTerlaluPanjang()
    {
        var hasil = ChatHistory.Rapikan([new ChatMessage("user", new string('x', 100))], 64, 10);

        Assert.Equal(10, Assert.Single(hasil).Content.Length);
    }

    [Fact]
    public void RapikanRiwayat_MenyimpanHanyaPesanTerakhir()
    {
        var mentah = Enumerable.Range(1, 100).Select(i => new ChatMessage("user", $"p{i}")).ToList();

        var hasil = ChatHistory.Rapikan(mentah, 5, 8192);

        Assert.Equal(5, hasil.Count);
        Assert.Equal("p100", hasil[^1].Content);
    }

    [Fact]
    public void RapikanRiwayat_MasukanNullKembaliKosong()
    {
        Assert.Empty(ChatHistory.Rapikan(null));
    }

    // ── ProactiveDirector ───────────────────────────────────────────────────

    [Theory]
    [InlineData(7, 30, "pagi")]
    [InlineData(12, 0, "siang")]
    [InlineData(16, 45, "sore")]
    [InlineData(21, 5, "malam")]
    [InlineData(2, 15, "larut_malam")]
    public void WaktuLokal_MemilihSesi(int jam, int menit, string expected)
    {
        var waktu = ProactiveDirector.DapatkanWaktuLokal(new DateTime(2026, 1, 1, jam, menit, 0));

        Assert.Equal(expected, waktu.Sesi);
    }

    [Fact]
    public void PromptProaktif_MenyebutDurasiHeningDanSesi()
    {
        var teks = ProactiveDirector.BuatPromptProaktif(null, 65, new DateTime(2026, 1, 1, 7, 30, 0));

        Assert.Contains("[SITUASI PROAKTIF OTONOM NEURO-SAMA]", teks, StringComparison.Ordinal);
        Assert.Contains("terdiam selama 65 detik", teks, StringComparison.Ordinal);
        Assert.Contains("pukul 7:30 (pagi)", teks, StringComparison.Ordinal);
        Assert.Contains("Status Ikatan Kizuna: Level 1 (Teman).", teks, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptProaktif_LevelTinggiMemakaiNadaPacar()
    {
        var snapshot = new BondSnapshot { Level = 4, Stage = "companion", StageLabel = "Gamer Girlfriend" };
        var teks = ProactiveDirector.BuatPromptProaktif(snapshot, 65);

        Assert.Contains("Karena kamu adalah pacarnya", teks, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptSentuhan_LevelRendahLebihKaku()
    {
        var teks = ProactiveDirector.BuatPromptSentuhan(null);

        Assert.Contains("[INTERAKSI HEADPAT / ELUS KEPALA]", teks, StringComparison.Ordinal);
        Assert.Contains("Kaget dan sedikit canggung", teks, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptSentuhan_LevelTinggiMemakaiTersipu()
    {
        var snapshot = new BondSnapshot { Level = 5, Stage = "lover", StageLabel = "Pacar Tercinta (Bucin Tsundere)" };
        var teks = ProactiveDirector.BuatPromptSentuhan(snapshot);

        Assert.Contains("DARI PACAR", teks, StringComparison.Ordinal);
        Assert.Contains("tersipu malu (salting)", teks, StringComparison.Ordinal);
    }
}
