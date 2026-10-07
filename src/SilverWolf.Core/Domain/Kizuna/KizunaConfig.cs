namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>Satu tahap ikatan: id + ambang poin minimum.</summary>
public sealed class BondStage
{
    public string Id { get; set; } = "stranger";

    public int MinPoints { get; set; }
}

/// <summary>
/// Metadata tahap yang dilihat pengguna — port <c>BOND_STAGES_INFO</c> dari
/// <c>apps/server-node/src/kizuna.js</c>. Label dan tone-nya berbahasa Indonesia
/// dan dipakai apa adanya oleh HUD serta prompt.
/// </summary>
public sealed class BondStageInfo
{
    public int Level { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int MinPoints { get; set; }

    public int NextPoints { get; set; }

    public string Tone { get; set; } = string.Empty;
}

public static class BondStages
{
    public static readonly Dictionary<string, BondStageInfo> Info = new(StringComparer.Ordinal)
    {
        ["stranger"] = new()
        {
            Level = 1, Name = "Stranger", Label = "Hacker Waspada",
            MinPoints = 0, NextPoints = 100,
            Tone = "Skeptis, dingin, menganggap kamu pengguna biasa atau rival hacker.",
        },
        ["acquaintance"] = new()
        {
            Level = 2, Name = "Acquaintance", Label = "Teman Mabar",
            MinPoints = 100, NextPoints = 400,
            Tone = "Santai, suka mengajak mabar game, mulai memanggil Master dengan nada ramah.",
        },
        ["regular"] = new()
        {
            Level = 3, Name = "Regular", Label = "Partner Hacking",
            MinPoints = 400, NextPoints = 1000,
            Tone = "Akrab dan percaya, berbagi strategi game, cerita tentang misi Stellaron Hunters.",
        },
        ["companion"] = new()
        {
            Level = 4, Name = "Companion", Label = "Gamer Girlfriend",
            MinPoints = 1000, NextPoints = 2000,
            Tone = "Pacar gamer yang manis dan perhatian, manja saat lelah, suka digombalin dan cemburu tipis.",
        },
        ["lover"] = new()
        {
            Level = 5, Name = "Lover", Label = "Pacar Tercinta (Bucin Tsundere)",
            MinPoints = 2000, NextPoints = 5000,
            Tone = "Sangat sayang dan setia, posesif menggemaskan, menganggapmu orang paling berharga di seluruh galaksi.",
        },
    };

    public static BondStageInfo Untuk(string stageId) =>
        Info.TryGetValue(stageId, out var info) ? info : Info["stranger"];

    /// <summary>
    /// Port penentuan tahap di <c>SilverWolfKizuna.getSnapshot()</c>: murni dari
    /// poin, <b>bukan</b> dari tahap yang disimpan mesin (beda dengan mesin yang
    /// memakai hysteresis).
    /// </summary>
    public static string DariPoin(double poin) => poin switch
    {
        >= 2000 => "lover",
        >= 1000 => "companion",
        >= 400 => "regular",
        >= 100 => "acquaintance",
        _ => "stranger",
    };
}

/// <summary>Konfigurasi kehangatan: waktu paruh peluruhan + lantai.</summary>
public sealed class WarmthConfig
{
    public double HalfLifeMs { get; set; } = 7L * 24 * 60 * 60 * 1000;

    public double Floor { get; set; } = 0.2;
}

/// <summary>Kontinuitas: satuan bucket dan toleransi.</summary>
public sealed class ContinuityConfig
{
    /// <summary>Satu-satunya nilai yang dipakai aplikasi: <c>"day"</c>.</summary>
    public string Unit { get; set; } = "day";

    public int Grace { get; set; }
}

public sealed class StorageConfig
{
    public int MaxUsers { get; set; } = 1000;

    public int DataRetentionDays { get; set; } = 90;

    public int CleanupIntervalHours { get; set; } = 24;
}

public sealed class ContextConfig
{
    public string DefaultLanguage { get; set; } = "en";
}

public sealed class DynamicsConfig
{
    public string Preset { get; set; } = "human";
}

public sealed class OwnerConfig
{
    public double InitialPoints { get; set; }

    public double PointMultiplier { get; set; } = 1;

    public double FirstContactBonus { get; set; }

    public List<string> ExclusiveAchievements { get; set; } = [];
}

/// <summary>
/// Konfigurasi mesin ikatan — port <c>createDefaultKizunaConfig()</c> plus
/// override yang dilakukan <c>SilverWolfKizuna.initialize()</c>.
/// </summary>
public sealed class KizunaConfig
{
    public bool Enabled { get; set; } = true;

    public OwnerConfig Owner { get; set; } = new();

    public Dictionary<string, double> BasePoints { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Aturan tambahan poin. Aplikasi lama selalu mengosongkannya
    /// (<c>config.rules = []</c>), sehingga kalkulator poin hanya memakai
    /// <see cref="BasePoints"/>.
    /// </summary>
    public List<object> Rules { get; set; } = [];

    /// <summary>Ambang pencapaian. Aplikasi lama selalu mengosongkannya.</summary>
    public List<object> Thresholds { get; set; } = [];

    public StorageConfig Storage { get; set; } = new();

    public ContextConfig Context { get; set; } = new();

    public DynamicsConfig Dynamics { get; set; } = new();

    public WarmthConfig Warmth { get; set; } = new();

    public ContinuityConfig Continuity { get; set; } = new();

    public List<BondStage> Stages { get; set; } = [];

    /// <summary>Port <c>createDefaultKizunaConfig()</c>.</summary>
    public static KizunaConfig CreateDefault() => new()
    {
        Owner = new OwnerConfig(),
        BasePoints = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["message"] = 1, ["reaction"] = 1, ["gift"] = 10, ["presence"] = 1, ["touch"] = 1,
        },
        Stages =
        [
            new BondStage { Id = "stranger", MinPoints = 0 },
            new BondStage { Id = "acquaintance", MinPoints = 100 },
            new BondStage { Id = "regular", MinPoints = 500 },
            new BondStage { Id = "companion", MinPoints = 1000 },
        ],
    };

    /// <summary>
    /// Konfigurasi yang dipakai Silver Wolf: tahap 5 tingkat (dengan
    /// <c>regular</c> diturunkan dari 500 ke 400 dan <c>lover</c> ditambahkan),
    /// poin dasar lebih besar, dan kehangatan lebih lambat memudar.
    /// </summary>
    public static KizunaConfig CreateSilverWolf()
    {
        var config = CreateDefault();
        config.Stages =
        [
            new BondStage { Id = "stranger", MinPoints = 0 },
            new BondStage { Id = "acquaintance", MinPoints = 100 },
            new BondStage { Id = "regular", MinPoints = 400 },
            new BondStage { Id = "companion", MinPoints = 1000 },
            new BondStage { Id = "lover", MinPoints = 2000 },
        ];
        config.BasePoints = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["message"] = 4, ["reaction"] = 3, ["gift"] = 25, ["presence"] = 1, ["touch"] = 8,
        };
        config.Warmth = new WarmthConfig { HalfLifeMs = 14L * 24 * 60 * 60 * 1000, Floor = 0.35 };
        return config;
    }
}
