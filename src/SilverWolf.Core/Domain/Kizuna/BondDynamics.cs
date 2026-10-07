namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Dinamika ikatan — port <c>BondDynamics</c> dari
/// <c>@aituber-onair/kizuna</c> (<c>dist/BondDynamics.js</c>), preset
/// <c>"human"</c> (satu-satunya preset yang dipakai aplikasi).
///
/// Kelas ini mengubah satu interaksi menjadi poin akhir plus perubahan
/// kehangatan. Angka-angkanya menentukan seberapa cepat Silver Wolf "naik
/// level" dan kapan suasana berubah jadi dingin — jadi tidak boleh disederhanakan.
/// </summary>
public sealed class BondDynamics
{
    private const long DayMs = 24L * 60 * 60 * 1000;

    // ── Konstanta preset "human" ────────────────────────────────────────────
    private const double NegativityBias = 3;
    private const double OffenseWindowMs = 7 * DayMs;
    private const double FirstOffenseMultiplier = 0.5;
    private const double SecondOffenseMultiplier = 1;
    private const double RepeatedOffenseMultiplier = 1.5;
    private const double MaxEscalationMultiplier = 1.5;
    private const double GraveBaseDamage = 25;
    private const double PositiveRepeatMultiplier = 0.85;
    private const double ConsistencyBonusPerBucket = 0.03;
    private const double MaxConsistencyBonus = 0.24;
    private const double GiftWarmthThreshold = 0.5;
    private const double LowWarmthGiftMultiplier = 0.25;
    private const double LightWarmthPenalty = 0.22;
    private const double GraveWarmthPenalty = 0.65;
    private const double ConflictRecoveryRate = 0.35;
    private const double DemotionHysteresis = 25;
    private const int ScarHealingPositiveInteractions = 6;
    private const int ScarHealingPositiveBuckets = 3;
    private const int MaxTrackedBuckets = 128;

    private static readonly Dictionary<string, double> StageBuffers = new(StringComparer.Ordinal)
    {
        ["stranger"] = 1, ["acquaintance"] = 0.75, ["regular"] = 0.45, ["companion"] = 0.3,
    };

    private static readonly Dictionary<string, double> ReunionRecoveryByStage = new(StringComparer.Ordinal)
    {
        ["stranger"] = 0.6, ["acquaintance"] = 0.75, ["regular"] = 0.9, ["companion"] = 1,
    };

    private static readonly HashSet<string> NegativeEmotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "angry", "annoyed", "disgusted", "frustrated", "hostile", "hurt",
    };

    private readonly BondEvaluator _evaluator;

    public double HalfLifeMs { get; }

    public double WarmthFloor { get; }

    public BondDynamics(KizunaConfig config, BondEvaluator evaluator)
    {
        _evaluator = evaluator;
        HalfLifeMs = config.Warmth.HalfLifeMs;
        WarmthFloor = config.Warmth.Floor;
    }

    /// <summary>Port <c>createState(interaction, points)</c>.</summary>
    public BondDynamicsState CreateState(long timestampMs, double points) => new()
    {
        Warmth = 1,
        WarmthUpdatedAt = Waktu.DariMs(timestampMs),
        ConflictChilled = false,
        Trend = "steady",
        CurrentStage = _evaluator.ResolveStage(points).Id,
        OffenseTimestamps = [],
        PositiveBucketCounts = new Dictionary<string, int>(StringComparer.Ordinal),
        GraveBucketKeys = [],
        PositiveInteractionsSinceScar = 0,
        PositiveBucketKeysSinceScar = [],
    };

    /// <summary>Port <c>ensureState(user, interaction)</c>.</summary>
    public BondDynamicsState EnsureState(KizunaUser user, long? timestampMs = null)
    {
        if (user.Stats.Dynamics is not null)
        {
            return user.Stats.Dynamics;
        }

        var ts = timestampMs ?? Waktu.KeMs(user.LastSeen);
        user.Stats.Dynamics = CreateState(ts, user.Points);
        return user.Stats.Dynamics;
    }

    /// <summary>Port <c>getWarmth(user, at)</c>.</summary>
    public double GetWarmth(KizunaUser user, long atMs) => CalculateWarmth(EnsureState(user), atMs);

    /// <summary>Port <c>getAtmosphere(warmth)</c>.</summary>
    public static string GetAtmosphere(double warmth) => warmth switch
    {
        >= 0.75 => "warm",
        >= 0.5 => "neutral",
        >= 0.3 => "cool",
        _ => "cold",
    };

    /// <summary>Port <c>resolveStage(user, points)</c> — memakai hysteresis.</summary>
    public BondStage ResolveStage(KizunaUser user, double points)
    {
        var state = EnsureState(user);
        return _evaluator.ResolveStageWithHysteresis(points, state.CurrentStage, Math.Max(0, DemotionHysteresis));
    }

    public readonly record struct HasilDinamika(
        double Points, string Valence, string? Severity, List<KizunaScar> CreatedScars, List<KizunaScar> HealedScars);

    /// <summary>
    /// Port <c>applyInteraction(user, interaction, rawPoints, appliedRules, bucketKey)</c>.
    /// Aplikasi tidak pernah mengatur <c>severity</c>/<c>valence</c> pada interaksi,
    /// sehingga jalur yang hidup adalah jalur positif; jalur negatif tetap
    /// diimplementasi agar perilaku pustaka aslinya utuh.
    /// </summary>
    public HasilDinamika ApplyInteraction(
        KizunaUser user,
        string kind,
        double rawPoints,
        string? emotion,
        string bucketKey,
        long timestampMs)
    {
        var state = EnsureState(user, timestampMs);
        var valence = ResolveValence(rawPoints, emotion);

        var previousUpdatedAt = Waktu.KeMs(state.WarmthUpdatedAt);
        var chronological = timestampMs >= previousUpdatedAt;
        var at = Math.Max(timestampMs, previousUpdatedAt);

        state.Warmth = CalculateWarmth(state, at);
        state.WarmthUpdatedAt = Waktu.DariMs(at);

        if (valence == "neutral")
        {
            if (chronological)
            {
                state.Trend = "steady";
            }

            return new HasilDinamika(0, valence, null, [], []);
        }

        return valence == "positive"
            ? ApplyPositive(user, kind, rawPoints, bucketKey, at, chronological, state)
            : ApplyNegative(user, kind, rawPoints, bucketKey, at, chronological, state);
    }

    private HasilDinamika ApplyPositive(
        KizunaUser user,
        string kind,
        double rawPoints,
        string bucketKey,
        long at,
        bool chronological,
        BondDynamicsState state)
    {
        var repeatCount = state.PositiveBucketCounts.TryGetValue(bucketKey, out var rc) ? rc : 0;
        var diminishingMultiplier = Math.Pow(PositiveRepeatMultiplier, repeatCount);
        var consistencyMultiplier = 1 + Math.Min(
            MaxConsistencyBonus,
            Math.Max(0, user.Stats.Continuity.Streak - 1) * ConsistencyBonusPerBucket);
        var giftMultiplier = kind == "gift" && state.Warmth < GiftWarmthThreshold
            ? LowWarmthGiftMultiplier
            : 1;

        var calculatedPoints = Math.Max(0, rawPoints) * diminishingMultiplier * consistencyMultiplier * giftMultiplier;
        var points = double.IsFinite(calculatedPoints) ? calculatedPoints : 0;

        SetBucketCount(state.PositiveBucketCounts, bucketKey, repeatCount + 1, MaxTrackedBuckets);

        var giftDuringConflict = kind == "gift" && state.ConflictChilled;
        if (chronological && !giftDuringConflict)
        {
            state.Trend = state.ConflictChilled ? "repairing" : "rising";
        }

        var recoveryRate = state.ConflictChilled
            ? ConflictRecoveryRate
            : ReunionRecoveryByStage.TryGetValue(state.CurrentStage, out var r) ? r : 0.75;

        if (chronological && !giftDuringConflict)
        {
            state.Warmth = Clamp(state.Warmth + ((1 - state.Warmth) * recoveryRate));
            if (state.ConflictChilled && state.Warmth >= 0.9)
            {
                state.ConflictChilled = false;
            }
        }

        var healedScars = new List<KizunaScar>();
        var activeScars = user.Scars.Where(s => s.HealedAt is null).ToList();
        if (activeScars.Count > 0 && kind != "gift" && chronological)
        {
            state.PositiveInteractionsSinceScar++;
            if (state.PositiveBucketKeysSinceScar.Count < ScarHealingPositiveBuckets
                && !state.PositiveBucketKeysSinceScar.Contains(bucketKey, StringComparer.Ordinal))
            {
                state.PositiveBucketKeysSinceScar.Add(bucketKey);
            }

            if (state.PositiveInteractionsSinceScar >= ScarHealingPositiveInteractions
                && state.PositiveBucketKeysSinceScar.Count >= ScarHealingPositiveBuckets)
            {
                foreach (var scar in activeScars)
                {
                    scar.HealedAt = Waktu.DariMs(at);
                    healedScars.Add(scar);
                }

                state.PositiveInteractionsSinceScar = 0;
                state.PositiveBucketKeysSinceScar = [];
            }
        }

        return new HasilDinamika(points, "positive", null, [], healedScars);
    }

    private HasilDinamika ApplyNegative(
        KizunaUser user,
        string kind,
        double rawPoints,
        string bucketKey,
        long at,
        bool chronological,
        BondDynamicsState state)
    {
        state.OffenseTimestamps = state.OffenseTimestamps
            .Where(t => at - Waktu.KeMs(t) <= OffenseWindowMs)
            .ToList();

        var offenseNumber = state.OffenseTimestamps.Count + 1;
        var escalation = Math.Min(MaxEscalationMultiplier, offenseNumber switch
        {
            1 => FirstOffenseMultiplier,
            2 => SecondOffenseMultiplier,
            _ => RepeatedOffenseMultiplier,
        });

        // Aplikasi tidak pernah mengirim severity; jadi selalu "light" di sini.
        const string severity = "light";

        var damageMultiplier = severity == "grave" ? Math.Max(1, escalation) : escalation;
        var magnitude = severity == "grave"
            ? Math.Max(Math.Abs(rawPoints), GraveBaseDamage)
            : Math.Max(Math.Abs(rawPoints), 1);
        var stageBuffer = severity == "grave" ? 1 : (StageBuffers.TryGetValue(state.CurrentStage, out var sb) ? sb : 1);

        var calculatedPoints = -magnitude * NegativityBias * damageMultiplier * stageBuffer;
        var points = double.IsFinite(calculatedPoints) ? calculatedPoints : 0;

        if (chronological)
        {
            state.OffenseTimestamps.Add(Waktu.DariMs(at));
            if (state.OffenseTimestamps.Count > 3)
            {
                state.OffenseTimestamps = [.. state.OffenseTimestamps.TakeLast(3)];
            }

            state.ConflictChilled = true;
            state.Trend = "falling";

            var warmthPenalty = severity == "grave" ? GraveWarmthPenalty : LightWarmthPenalty;
            state.Warmth = Math.Max(WarmthFloor, state.Warmth - (warmthPenalty * damageMultiplier));
            state.PositiveInteractionsSinceScar = 0;
            state.PositiveBucketKeysSinceScar = [];
        }

        return new HasilDinamika(points, "negative", severity, [], []);
    }

    /// <summary>Port <c>calculateWarmth(state, at)</c>: peluruhan eksponensial.</summary>
    public double CalculateWarmth(BondDynamicsState state, long atMs)
    {
        var elapsed = Math.Max(0, atMs - Waktu.KeMs(state.WarmthUpdatedAt));
        var remaining = Math.Pow(2, -elapsed / HalfLifeMs);
        return Clamp(WarmthFloor + ((Math.Max(WarmthFloor, state.Warmth) - WarmthFloor) * remaining));
    }

    private static string ResolveValence(double rawPoints, string? emotion)
    {
        if (rawPoints < 0)
        {
            return "negative";
        }

        return !string.IsNullOrWhiteSpace(emotion) && NegativeEmotions.Contains(emotion) ? "negative" : "positive";
    }

    private static double Clamp(double value) => Math.Min(1, Math.Max(0, value));

    /// <summary>
    /// Port <c>setBucketCount()</c>: hapus lalu tambahkan lagi supaya kunci
    /// berpindah ke ekor, lalu pangkas yang paling tua bila melebihi batas.
    /// </summary>
    private static void SetBucketCount(Dictionary<string, int> counts, string key, int count, int limit)
    {
        counts.Remove(key);
        counts[key] = count;

        if (counts.Count <= limit)
        {
            return;
        }

        foreach (var expired in counts.Keys.Take(Math.Max(0, counts.Count - limit)).ToList())
        {
            counts.Remove(expired);
        }
    }
}
