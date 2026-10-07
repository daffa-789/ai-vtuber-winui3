namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Penilai tahap/level ikatan — port <c>BondEvaluator</c> dari
/// <c>@aituber-onair/kizuna</c> (<c>dist/BondEvaluator.js</c>).
///
/// Hanya jalur yang benar-benar dipakai aplikasi yang diimplementasi:
/// <c>continuity.unit = "day"</c>. Jalur <c>"session"</c> dan fungsi bucket
/// khusus tidak pernah dikonfigurasi aplikasi, jadi sengaja tidak disalin.
/// </summary>
public sealed class BondEvaluator
{
    private const long DayMs = 24L * 60 * 60 * 1000;

    private readonly List<BondStage> _stages;
    private readonly double _pointsPerLevel;
    private readonly int _maxLevel;

    public bool HasConfiguredStages { get; }

    public bool UsesStageLevels { get; }

    public IReadOnlyList<BondStage> Stages => _stages;

    public BondEvaluator(KizunaConfig config)
    {
        if (config.Continuity.Unit != "day")
        {
            throw new NotSupportedException(
                $"continuity.unit = \"{config.Continuity.Unit}\" tidak didukung port ini; aplikasi selalu memakai \"day\".");
        }

        HasConfiguredStages = config.Stages.Count > 0;
        UsesStageLevels = HasConfiguredStages; // aplikasi tidak pernah mengatur levels
        _stages = [.. config.Stages.OrderBy(s => s.MinPoints)];
        _pointsPerLevel = Math.Max(1, 100);
        _maxLevel = Math.Max(1, 10);

        if (HasConfiguredStages && (_stages.Count > 0) && _stages[0].MinPoints > 0)
        {
            throw new ArgumentException("Tahap ikatan pertama harus mulai di nol atau di bawahnya.");
        }
    }

    /// <summary>Port <c>resolveStage(points)</c>.</summary>
    public BondStage ResolveStage(double points)
    {
        var resolved = _stages.Count > 0
            ? _stages[0]
            : new BondStage { Id = "stranger", MinPoints = 0 };

        foreach (var stage in _stages)
        {
            if (points < stage.MinPoints)
            {
                break;
            }

            resolved = stage;
        }

        return resolved;
    }

    /// <summary>Port <c>resolveStageWithHysteresis(points, currentStageId, margin)</c>.</summary>
    public BondStage ResolveStageWithHysteresis(double points, string currentStageId, double margin)
    {
        var rawStage = ResolveStage(points);
        var currentIndex = GetStageIndex(currentStageId);
        var rawIndex = _stages.IndexOf(rawStage);

        if (currentIndex < 0 || rawIndex >= currentIndex)
        {
            return rawStage;
        }

        var currentStage = _stages[currentIndex];
        return points < currentStage.MinPoints - Math.Max(0, margin) ? rawStage : currentStage;
    }

    /// <summary>Port <c>calculateLevel(points)</c>.</summary>
    public int CalculateLevel(double points)
    {
        if (UsesStageLevels)
        {
            var stage = ResolveStage(points);
            return Math.Max(1, _stages.IndexOf(stage) + 1);
        }

        return Math.Min((int)Math.Floor(Math.Max(0, points) / _pointsPerLevel) + 1, _maxLevel);
    }

    /// <summary>Port <c>calculateLevelForStage(points, stageId)</c>.</summary>
    public int CalculateLevelForStage(double points, string stageId)
    {
        if (!UsesStageLevels)
        {
            return CalculateLevel(points);
        }

        var stageIndex = GetStageIndex(stageId);
        return stageIndex >= 0 ? stageIndex + 1 : CalculateLevel(points);
    }

    public int GetStageIndex(string stageId) => _stages.FindIndex(s => s.Id == stageId);

    /// <summary>Port <c>normalizePoints(points)</c>.</summary>
    public double NormalizePoints(double points)
    {
        var ambangTertinggi = HasConfiguredStages
            ? (_stages.Count > 0 ? _stages[^1].MinPoints : 1)
            : _pointsPerLevel * Math.Max(1, _maxLevel - 1);

        if (ambangTertinggi <= 0)
        {
            return points > 0 ? 1 : 0;
        }

        return Math.Min(1, Math.Max(0, points) / ambangTertinggi);
    }

    /// <summary>Port <c>createContinuity(interaction)</c>.</summary>
    public KizunaContinuity CreateContinuity(long timestampMs)
    {
        var bucket = ResolveBucket(timestampMs);
        return new KizunaContinuity
        {
            Streak = 1,
            TotalActiveBuckets = 1,
            LastContactAt = Waktu.DariMs(timestampMs),
            LastBucketKey = bucket.Key,
            LastBucketIndex = bucket.Index,
        };
    }

    /// <summary>Port <c>updateContinuity(continuity, interaction)</c>.</summary>
    public void UpdateContinuity(KizunaContinuity continuity, long timestampMs, int grace)
    {
        var bucket = ResolveBucket(timestampMs);

        if (timestampMs > Waktu.KeMs(continuity.LastContactAt))
        {
            continuity.LastContactAt = Waktu.DariMs(timestampMs);
        }

        if (bucket.Key == continuity.LastBucketKey)
        {
            return;
        }

        if (bucket.Index <= (continuity.LastBucketIndex ?? long.MinValue))
        {
            return;
        }

        continuity.TotalActiveBuckets++;

        if (continuity.LastBucketIndex is { } indeksLama)
        {
            var terlewat = bucket.Index - indeksLama - 1;
            continuity.Streak = terlewat <= Math.Max(0, grace) ? continuity.Streak + 1 : 1;
        }
        else
        {
            continuity.Streak++;
        }

        continuity.LastBucketKey = bucket.Key;
        continuity.LastBucketIndex = bucket.Index;
    }

    /// <summary>Port <c>resolveBucket()</c> untuk <c>unit = "day"</c>.</summary>
    public (string Key, long Index) ResolveBucket(long timestampMs)
    {
        var dayIndex = (long)Math.Floor(timestampMs / (double)DayMs);
        return ($"day:{dayIndex}", dayIndex);
    }
}

/// <summary>Konversi waktu Unix-milidetik ↔ DateTime (UTC).</summary>
public static class Waktu
{
    public static DateTime DariMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    public static long KeMs(DateTime nilai) => new DateTimeOffset(nilai).ToUnixTimeMilliseconds();
}
