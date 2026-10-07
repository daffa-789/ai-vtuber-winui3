using System.Globalization;
using System.Text.Json;
using SilverWolf.Core.Domain.Kizuna;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji paritas terhadap <b>data golden</b> yang diambil dari aplikasi Electron
/// asli pada 2026-10-07, sebelum asetnya dipindahkan ke proyek ini.
///
/// Berkas <c>TestData/golden/kizuna-setelah-3-interaksi.json</c> adalah berkas
/// yang benar-benar ditulis <c>@aituber-onair/kizuna</c> ke
/// <c>silver_wolf_memory/kizuna/silverwolf_kizuna_v1.json</c> setelah tiga
/// interaksi lewat server Node. Isinya bukan hitungan tangan — ini artefak asli.
///
/// Urutan interaksi yang direkam server lama, dan sebabnya:
///   1. POST /api/chat          → satu interaksi  kind=message  (+4)
///   2. POST /api/kizuna/touch  → interaksi touch              (+6,8)
///   3.                            …lalu pesan balasan, karena chatTouch()
///                                 memanggil persist() → recordMessage()  (+2,89)
///
/// Poin ketiga itu mudah terlewat: <c>/api/kizuna/touch</c> menghasilkan
/// <b>dua</b> interaksi, bukan satu. Uji ini mengunci perilaku tersebut supaya
/// tidak hilang saat refactor.
/// </summary>
public class KizunaGoldenTests
{
    private sealed class Jam
    {
        public long Now { get; set; }

        public Jam(long awal) => Now = awal;
    }

    private static string JalurGolden =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "golden", "kizuna-setelah-3-interaksi.json");

    [Fact]
    public async Task ReplayTigaInteraksi_MenghasilkanKeadaanYangSamaDenganAplikasiLama()
    {
        Assert.True(File.Exists(JalurGolden), $"Data golden tidak ditemukan: {JalurGolden}");

        using var dok = JsonDocument.Parse(await File.ReadAllTextAsync(JalurGolden));
        var emas = dok.RootElement.GetProperty("users").GetProperty("master");
        var rekaman = emas.GetProperty("stats").GetProperty("interactionHistory")
            .EnumerateArray().ToList();

        Assert.Equal(3, rekaman.Count);

        static long KeMs(JsonElement rekam) => DateTimeOffset
            .Parse(rekam.GetProperty("timestamp").GetString()!, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            .ToUnixTimeMilliseconds();

        var waktu = rekaman.Select(KeMs).ToList();
        var poinHarapan = rekaman.Select(r => r.GetProperty("points").GetDouble()).ToList();

        // Nilai yang harus direproduksi persis.
        Assert.Equal(4, poinHarapan[0], 6);
        Assert.Equal(6.8, poinHarapan[1], 6);
        Assert.Equal(2.89, poinHarapan[2], 6);

        var jam = new Jam(waktu[0]);
        var storage = new InMemoryKizunaStorage();
        var engine = new KizunaEngine(
            KizunaConfig.CreateSilverWolf(), storage, "master", "silverwolf_kizuna_v1", () => jam.Now);

        await engine.InitializeAsync();

        // 1) pesan pengguna
        var pesan1 = await engine.RecordMessageAsync("halo Silver Wolf");
        Assert.Equal(poinHarapan[0], pesan1.PointsAdded, 6);

        // 2) sentuhan
        jam.Now = waktu[1];
        var (sentuh, _) = await engine.RecordTouchAsync();
        Assert.Equal(poinHarapan[1], sentuh.PointsAdded, 6);

        // 3) pesan balasan yang ditulis persist() setelah sentuhan
        jam.Now = waktu[2];
        var pesan2 = await engine.RecordMessageAsync("[senyum] Sistem inti sudah hidup, Master.");
        Assert.Equal(poinHarapan[2], pesan2.PointsAdded, 6);

        // ── Bandingkan keadaan akhir dengan berkas golden ──────────────────
        var totalEmas = emas.GetProperty("points").GetDouble();
        Assert.Equal(totalEmas, pesan2.TotalPoints, 6);
        Assert.Equal(13.69, pesan2.TotalPoints, 6);

        Assert.Equal(emas.GetProperty("level").GetInt32(), engine.GetSnapshot().Level);
        Assert.Equal("guest", emas.GetProperty("role").GetString());
        Assert.Equal(
            emas.GetProperty("stats").GetProperty("totalInteractions").GetInt32(),
            rekaman.Count);

        var snap = engine.GetSnapshot();
        Assert.Equal("stranger", snap.Stage);
        Assert.Equal(14, snap.Progress);
        Assert.Equal("warm", snap.Atmosphere);
        Assert.Equal("rising", snap.Trend);
        Assert.Equal(1, snap.Streak);

        // ── Dinamika internal ─────────────────────────────────────────────
        var dinamikaEmas = emas.GetProperty("stats").GetProperty("dynamics");
        var kontinuitasEmas = emas.GetProperty("stats").GetProperty("continuity");

        var bucketEmas = dinamikaEmas.GetProperty("positiveBucketCounts")
            .EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());

        var muatan = await storage.LoadAsync("silverwolf_kizuna_v1");
        var user = muatan!.Users["master"];
        var dinamika = user.Stats.Dynamics!;

        Assert.Equal(bucketEmas, dinamika.PositiveBucketCounts);
        Assert.Equal("stranger", dinamika.CurrentStage);
        Assert.Equal("rising", dinamika.Trend);
        Assert.False(dinamika.ConflictChilled);
        Assert.Empty(dinamika.OffenseTimestamps);
        Assert.Empty(dinamika.GraveBucketKeys);
        Assert.Equal(0, dinamika.PositiveInteractionsSinceScar);
        Assert.Empty(dinamika.PositiveBucketKeysSinceScar);

        Assert.Equal(
            kontinuitasEmas.GetProperty("lastBucketKey").GetString(),
            user.Stats.Continuity.LastBucketKey);
        Assert.Equal(
            kontinuitasEmas.GetProperty("lastBucketIndex").GetInt64(),
            user.Stats.Continuity.LastBucketIndex);
        Assert.Equal(kontinuitasEmas.GetProperty("streak").GetInt32(), user.Stats.Continuity.Streak);
        Assert.Equal(
            kontinuitasEmas.GetProperty("totalActiveBuckets").GetInt32(),
            user.Stats.Continuity.TotalActiveBuckets);

        // Jejak interaksi: jenis, poin, dan valence-nya harus sama.
        Assert.Equal(
            rekaman.Select(r => r.GetProperty("kind").GetString()).ToList(),
            user.Stats.InteractionHistory.Select(r => (string?)r.Kind).ToList());
        Assert.Equal(
            rekaman.Select(r => r.GetProperty("valence").GetString()).ToList(),
            user.Stats.InteractionHistory.Select(r => (string?)r.Valence).ToList());
        Assert.All(user.Stats.InteractionHistory, r => Assert.Empty(r.AppliedRules));
    }

    [Fact]
    public void BerkasGolden_MemakaiPeranGuestBukanOwner()
    {
        // Aplikasi lama tidak pernah mengirim isOwner, jadi pengguna "master"
        // berperan guest. Akibatnya aturan retensi 90 hari benar-benar berlaku
        // padanya, dan bonus owner tidak pernah aktif.
        using var dok = JsonDocument.Parse(File.ReadAllText(JalurGolden));
        var emas = dok.RootElement.GetProperty("users").GetProperty("master");

        Assert.Equal("guest", emas.GetProperty("role").GetString());
        Assert.Empty(emas.GetProperty("achievements").EnumerateArray());
        Assert.Empty(emas.GetProperty("scars").EnumerateArray());
        Assert.Empty(emas.GetProperty("triggeredThresholds").EnumerateArray());
    }

    [Fact]
    public void BerkasGolden_TidakMenyimpanEmosiKarenaInteraksiTidakMengirimnya()
    {
        using var dok = JsonDocument.Parse(File.ReadAllText(JalurGolden));
        var emas = dok.RootElement.GetProperty("users").GetProperty("master");

        Assert.Empty(emas.GetProperty("stats").GetProperty("favoriteEmotions").EnumerateObject());
        Assert.All(
            emas.GetProperty("stats").GetProperty("interactionHistory").EnumerateArray(),
            r => Assert.False(r.TryGetProperty("emotion", out _)));
    }
}
