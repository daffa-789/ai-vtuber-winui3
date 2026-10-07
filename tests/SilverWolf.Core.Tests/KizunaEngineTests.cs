using SilverWolf.Core.Domain.Kizuna;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji mesin ikatan. Angka-angka di sini adalah <b>hasil hitungan tangan</b>
/// dari rumus <c>BondDynamics</c> + <c>PointCalculator</c>, bukan hasil
/// pengamatan longgar — jadi kalau ada yang melenceng, berarti port-nya salah.
/// </summary>
public class KizunaEngineTests
{
    private static readonly long Awal = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private sealed class Jam
    {
        public long Now { get; private set; }

        public Jam(long awal) => Now = awal;

        public void Maju(TimeSpan delta) => Now += (long)delta.TotalMilliseconds;
    }

    private static KizunaEngine Buat(IKizunaStorage storage, Jam jam, KizunaConfig? config = null) =>
        new(config ?? KizunaConfig.CreateSilverWolf(), storage, "master", "silverwolf_kizuna_v1", () => jam.Now);

    [Fact]
    public async Task PesanPertama_MenambahEmpatPoin()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();

        var hasil = await engine.RecordMessageAsync("halo");

        Assert.Equal(4, hasil.PointsAdded, 6);
        Assert.Equal(4, hasil.TotalPoints, 6);
        Assert.False(hasil.LeveledUp);

        var snap = engine.GetSnapshot();
        Assert.Equal("stranger", snap.Stage);
        Assert.Equal("Stranger", snap.StageName);
        Assert.Equal("Hacker Waspada", snap.StageLabel);
        Assert.Equal(1, snap.Level);
        Assert.Equal(100, snap.NextPoints);
        Assert.Equal(4, snap.Progress);
        Assert.Equal(1, snap.Warmth, 6);
        Assert.Equal("warm", snap.Atmosphere);
        Assert.Equal("rising", snap.Trend);
        Assert.Equal(1, snap.Streak);
    }

    [Fact]
    public async Task PesanKeduaDiHariYangSama_MengecilKarenaPengulangan()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();

        await engine.RecordMessageAsync("satu");
        jam.Maju(TimeSpan.FromSeconds(1));
        var hasil = await engine.RecordMessageAsync("dua");

        // 4 * 0.85^1 * 1 (streak masih 1 → tanpa bonus konsistensi)
        Assert.Equal(3.4, hasil.PointsAdded, 6);
        Assert.Equal(7.4, hasil.TotalPoints, 6);
    }

    [Fact]
    public async Task SentuhanPertama_MenambahDelapanPoin()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();

        var (hasil, snap) = await engine.RecordTouchAsync();

        Assert.Equal(8, hasil.PointsAdded, 6);
        Assert.Equal(8, snap.Points, 6);
    }

    [Fact]
    public async Task HariBerikutnya_StreakNaikDanBonusKonsistensiMuncul()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();

        await engine.RecordMessageAsync("hari satu");
        jam.Maju(TimeSpan.FromDays(1));
        var hasil = await engine.RecordMessageAsync("hari dua");

        // bucket baru → pengulangan nol, streak 2 → 1 + (2-1)*0.03 = 1.03
        Assert.Equal(4.12, hasil.PointsAdded, 6);
        Assert.Equal(2, engine.GetSnapshot().Streak);
    }

    [Fact]
    public async Task Kehangatan_MeluruhMenujuLantai()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();
        await engine.RecordMessageAsync("halo");

        jam.Maju(TimeSpan.FromDays(14));
        // floor 0.35 + (1 - 0.35) * 2^-1
        Assert.Equal(0.675, engine.GetSnapshot().Warmth, 3);

        jam.Maju(TimeSpan.FromDays(14));
        Assert.Equal(0.5125, engine.GetSnapshot().Warmth, 3);
    }

    [Theory]
    [InlineData(0, "warm")]     // kehangatan 1.00
    [InlineData(14, "neutral")] // 0.675  — ambang "warm" adalah 0.75
    [InlineData(28, "neutral")] // 0.5125
    [InlineData(42, "cool")]    // 0.43125
    public async Task Atmosfer_MengikutiKehangatan(int hari, string expected)
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();
        await engine.RecordMessageAsync("halo");

        jam.Maju(TimeSpan.FromDays(hari));

        Assert.Equal(expected, engine.GetSnapshot().Atmosphere);
    }

    [Theory]
    [InlineData(2500, "lover", 5)]
    [InlineData(1500, "companion", 4)]
    [InlineData(500, "regular", 3)]
    [InlineData(150, "acquaintance", 2)]
    [InlineData(50, "stranger", 1)]
    public void Tahap_DitentukanMurniOlehPoin(double poin, string stage, int level)
    {
        Assert.Equal(stage, BondStages.DariPoin(poin));
        Assert.Equal(level, BondStages.Untuk(stage).Level);
    }

    [Fact]
    public async Task KonteksIkatan_BerisiBarisYangDiharapkan()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);
        await engine.InitializeAsync();
        await engine.RecordMessageAsync("halo");

        var ctx = engine.GetBondContext();

        Assert.Contains("## Hubungan & Ikatan (Kizuna Level 1 - Stranger", ctx, StringComparison.Ordinal);
        Assert.Contains("- Total Poin Ikatan: 4 XP (Kemajuan ke level berikutnya: 4%)", ctx, StringComparison.Ordinal);
        Assert.Contains("- Kehangatan Hubungan: 100%", ctx, StringComparison.Ordinal);
        Assert.Contains("- Raw Context: Bond with master: stranger (level 1, 4 points).", ctx, StringComparison.Ordinal);
        Assert.Contains("without inducing guilt.", ctx, StringComparison.Ordinal);
    }

    [Fact]
    public void BelumInisialisasi_MemakaiSnapshotAwal()
    {
        var jam = new Jam(Awal);
        var engine = Buat(new InMemoryKizunaStorage(), jam);

        var snap = engine.GetSnapshot();

        Assert.Equal("stranger", snap.Stage);
        Assert.Equal(1, snap.Level);
        Assert.Equal(0, snap.Points, 6);
        Assert.Equal(100, snap.NextPoints);
        Assert.Equal(1, snap.Warmth, 6);
        Assert.Equal("warm", snap.Atmosphere);
        Assert.Equal("rising", snap.Trend);
        Assert.Equal(string.Empty, engine.GetBondContext());
    }

    [Fact]
    public async Task Persistensi_PoinKembaliUtuhSetelahDimuatUlang()
    {
        var storage = new InMemoryKizunaStorage();
        var jam = new Jam(Awal);

        var pertama = Buat(storage, jam);
        await pertama.InitializeAsync();
        await pertama.RecordMessageAsync("satu");
        jam.Maju(TimeSpan.FromDays(1));
        await pertama.RecordMessageAsync("dua");

        var kedua = Buat(storage, jam);
        await kedua.InitializeAsync();

        Assert.Equal(pertama.GetSnapshot().Points, kedua.GetSnapshot().Points, 6);
        Assert.Equal(2, kedua.GetSnapshot().Streak);
    }

    [Fact]
    public async Task Pembersihan_MenghapusPenggunaLewatMasaRetensi()
    {
        var jam = new Jam(Awal);
        var config = KizunaConfig.CreateSilverWolf();
        config.Storage.DataRetentionDays = 90;

        var engine = Buat(new InMemoryKizunaStorage(), jam, config);
        await engine.InitializeAsync();
        await engine.RecordMessageAsync("halo");
        Assert.Equal(4, engine.GetSnapshot().Points, 6);

        jam.Maju(TimeSpan.FromDays(91));
        engine.PerformCleanup();

        Assert.Equal(0, engine.GetSnapshot().Points, 6);
    }

    [Fact]
    public async Task PesanPanjang_DipotongTigaRatusKarakter()
    {
        var jam = new Jam(Awal);
        var storage = new InMemoryKizunaStorage();
        var engine = Buat(storage, jam);
        await engine.InitializeAsync();

        await engine.RecordMessageAsync(new string('a', 500));

        var envelope = await storage.LoadAsync("silverwolf_kizuna_v1");
        var rekam = Assert.Single(Assert.Single(envelope!.Users).Value.Stats.InteractionHistory);
        Assert.Equal(300, rekam.Message!.Length);
    }

    [Fact]
    public async Task PoinTidakPernahTurunDiBawahNol()
    {
        var jam = new Jam(Awal);
        var config = KizunaConfig.CreateSilverWolf();
        config.BasePoints["message"] = -100; // jalur negatif: negativityBias 3 memperbesar kerugian

        var engine = Buat(new InMemoryKizunaStorage(), jam, config);
        await engine.InitializeAsync();

        var hasil = await engine.RecordMessageAsync("kasar");

        Assert.Equal(0, hasil.TotalPoints, 6);
        Assert.Equal("falling", engine.GetSnapshot().Trend);
    }
}
