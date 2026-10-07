namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Model persistensi ikatan. Nama properti sengaja mengikuti kunci camelCase
/// yang ditulis <c>@aituber-onair/kizuna</c> ke berkas, karena berkas yang sudah
/// ada di mesin pengguna harus tetap bisa dibaca oleh aplikasi baru.
/// </summary>
public sealed class KizunaUser
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"owner" atau "guest".</summary>
    public string Role { get; set; } = "guest";

    public double Points { get; set; }

    public int Level { get; set; } = 1;

    public List<KizunaAchievement> Achievements { get; set; } = [];

    public List<KizunaScar> Scars { get; set; } = [];

    public List<string> TriggeredThresholds { get; set; } = [];

    public KizunaStats Stats { get; set; } = new();

    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }

    public Dictionary<string, object?> CustomData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class KizunaAchievement
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public DateTime EarnedAt { get; set; }
}

public sealed class KizunaScar
{
    public string Id { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? HealedAt { get; set; }
}

public sealed class KizunaStats
{
    public int TotalInteractions { get; set; } = 1;

    public double TotalPointsEarned { get; set; }

    public KizunaContinuity Continuity { get; set; } = new();

    public Dictionary<string, int> FavoriteEmotions { get; set; } = new(StringComparer.Ordinal);

    public DateTime? LastPointsEarned { get; set; }

    public List<InteractionRecord> InteractionHistory { get; set; } = [];

    public BondDynamicsState? Dynamics { get; set; }
}

public sealed class KizunaContinuity
{
    public int Streak { get; set; } = 1;

    public int TotalActiveBuckets { get; set; } = 1;

    public DateTime LastContactAt { get; set; }

    public string LastBucketKey { get; set; } = string.Empty;

    public long? LastBucketIndex { get; set; }
}

/// <summary>Status dinamika yang disimpan — port <c>createState()</c>.</summary>
public sealed class BondDynamicsState
{
    public double Warmth { get; set; } = 1;

    public DateTime WarmthUpdatedAt { get; set; }

    public bool ConflictChilled { get; set; }

    public string Trend { get; set; } = "steady";

    public string CurrentStage { get; set; } = "stranger";

    public List<DateTime> OffenseTimestamps { get; set; } = [];

    public Dictionary<string, int> PositiveBucketCounts { get; set; } = new(StringComparer.Ordinal);

    public List<string> GraveBucketKeys { get; set; } = [];

    public int PositiveInteractionsSinceScar { get; set; }

    public List<string> PositiveBucketKeysSinceScar { get; set; } = [];
}

public sealed class InteractionRecord
{
    public string Id { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public double Points { get; set; }

    public string? Message { get; set; }

    public string? Emotion { get; set; }

    public string Kind { get; set; } = "message";

    public List<string> AppliedRules { get; set; } = [];

    public string? Valence { get; set; }

    public string? Severity { get; set; }
}

/// <summary>Rekaman batas aturan — selalu kosong di aplikasi ini (tanpa rules).</summary>
public sealed class LimitRecord
{
    public string UserId { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public long LastApplied { get; set; }

    public string BucketKey { get; set; } = string.Empty;

    public int BucketCount { get; set; }
}

/// <summary>Amplop persistensi — port <c>isPersistenceEnvelope()</c>.</summary>
public sealed class KizunaEnvelope
{
    public string Format { get; set; } = "@aituber-onair/kizuna";

    public int Version { get; set; } = 1;

    public Dictionary<string, KizunaUser> Users { get; set; } = new(StringComparer.Ordinal);

    public int SessionCounter { get; set; }

    public List<LimitRecord> LimitRecords { get; set; } = [];
}

/// <summary>Hasil satu interaksi — port nilai balik <c>addPoints()</c>.</summary>
public sealed class InteractionResult
{
    public double PointsAdded { get; set; }

    public double TotalPoints { get; set; }

    public bool LeveledUp { get; set; }

    public int? NewLevel { get; set; }

    public List<string> TriggeredActions { get; set; } = [];
}
