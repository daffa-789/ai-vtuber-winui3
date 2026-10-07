namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Mesin ikatan Silver Wolf — port yang menyatukan <c>KizunaManager</c>,
/// <c>UserManager</c>, <c>PointCalculator</c>, <c>BondDynamics</c>, dan
/// <c>BondEvaluator</c> dari <c>@aituber-onair/kizuna</c>, plus pembungkus
/// <c>SilverWolfKizuna</c> dari <c>apps/server-node/src/kizuna.js</c>.
///
/// Kenapa digabung, bukan dipecah per kelas seperti aslinya: pustaka aslinya
/// multi-pengguna (livestream dengan banyak penonton). Aplikasi ini
/// <b>satu pengguna</b> (<c>master</c>), tanpa aturan poin, tanpa ambang
/// pencapaian, dan tanpa sesi. Mempertahankan lapisan multi-pengguna hanya
/// akan menambah kode mati yang tidak pernah dieksekusi.
///
/// Yang tidak bisa disederhanakan tetap dipertahankan persis: rumus poin,
/// peluruhan kehangatan, hysteresis tahap, dan format berkas.
/// </summary>
public sealed class KizunaEngine
{
    private const long DayMs = 24L * 60 * 60 * 1000;

    private readonly KizunaConfig _config;
    private readonly IKizunaStorage? _storage;
    private readonly string _storageKey;
    private readonly BondEvaluator _evaluator;
    private readonly BondDynamics _dynamics;
    private readonly Dictionary<string, KizunaUser> _users = new(StringComparer.Ordinal);
    private readonly Func<long> _now;
    private int _sessionCounter;
    private bool _initialized;

    public string UserId { get; }

    public bool IsInitialized => _initialized;

    public KizunaEngine(
        KizunaConfig config,
        IKizunaStorage? storage,
        string userId = "master",
        string storageKey = "silverwolf_kizuna_v1",
        Func<long>? now = null)
    {
        _config = config;
        _storage = storage;
        UserId = userId;
        _storageKey = storageKey;
        _evaluator = new BondEvaluator(config);
        _dynamics = new BondDynamics(config, _evaluator);
        _now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    // ── Siklus hidup ────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await LoadFromStorageAsync(ct).ConfigureAwait(false);
        _initialized = true;
    }

    /// <summary>Port <c>destroy()</c>.</summary>
    public void Destroy() => _initialized = false;

    // ── Interaksi ───────────────────────────────────────────────────────────

    /// <summary>Port <c>recordMessage(text)</c>: teks dipotong 300 karakter.</summary>
    public async Task<InteractionResult> RecordMessageAsync(string? text, long? timestampMs = null, CancellationToken ct = default) =>
        await ProcessInteractionAsync(
            "message",
            (text ?? string.Empty).Length > 300 ? text![..300] : (text ?? string.Empty),
            timestampMs,
            ct).ConfigureAwait(false);

    /// <summary>
    /// Port <c>recordTouch()</c>: mengembalikan hasil interaksi <b>sekaligus</b>
    /// snapshot terbaru, karena pemanggil memakai snapshot itu untuk header
    /// <c>x-kizuna</c>.
    /// </summary>
    public async Task<(InteractionResult Hasil, BondSnapshot Snapshot)> RecordTouchAsync(
        long? timestampMs = null,
        CancellationToken ct = default)
    {
        var hasil = await ProcessInteractionAsync(
            "touch",
            "elusan kepala / headpat mesra di rambut",
            timestampMs,
            ct).ConfigureAwait(false);
        return (hasil, GetSnapshot());
    }

    private async Task<InteractionResult> ProcessInteractionAsync(
        string kind,
        string text,
        long? timestampMs,
        CancellationToken ct)
    {
        var ts = timestampMs ?? _now();
        if (!double.IsFinite(ts))
        {
            throw new ArgumentOutOfRangeException(nameof(timestampMs), "Timestamp interaksi tidak valid.");
        }

        if (!_initialized)
        {
            await InitializeAsync(ct).ConfigureAwait(false);
        }

        var existingUser = GetUser(UserId);
        var bucket = _evaluator.ResolveBucket(ts);
        var previousContinuity = existingUser?.Stats.Continuity;

        var isNewerBucket = previousContinuity?.LastBucketIndex is null
            || bucket.Index > previousContinuity.LastBucketIndex.Value;
        var isFirstContactInBucket = previousContinuity is null
            || (bucket.Key != previousContinuity.LastBucketKey && isNewerBucket);

        var user = GetOrCreateUser(ts, isOwner: false);

        // Port calculatePoints(): tanpa aturan dan tanpa bonus owner, hasilnya
        // murni poin dasar sesuai jenis interaksi.
        var basePoints = _config.BasePoints.TryGetValue(kind, out var bp) ? bp : 1;
        _ = isFirstContactInBucket; // hanya berpengaruh bila role = owner

        var dinamika = _dynamics.ApplyInteraction(user, kind, basePoints, emotion: null, bucket.Key, ts);
        var hasil = AddPoints(user, dinamika.Points, ts);

        AddInteractionRecord(user, ts, hasil.PointsAdded, kind, text, dinamika.Valence, dinamika.Severity);

        await SaveToStorageAsync(ct).ConfigureAwait(false);
        return hasil;
    }

    /// <summary>Port <c>addPoints()</c>.</summary>
    private InteractionResult AddPoints(KizunaUser user, double requestedPoints, long occurredAt)
    {
        var oldPoints = user.Points;
        var oldLevel = user.Level;

        var state = _dynamics.EnsureState(user, occurredAt);
        var oldStage = state.CurrentStage;

        var adjusted = user.Points + (double.IsFinite(requestedPoints) ? requestedPoints : 0);
        user.Points = double.IsFinite(adjusted) ? Math.Max(0, adjusted) : oldPoints;
        var pointsAdded = user.Points - oldPoints;

        if (occurredAt > Waktu.KeMs(user.LastSeen))
        {
            user.LastSeen = Waktu.DariMs(occurredAt);
        }

        user.Stats.TotalPointsEarned += Math.Max(0, pointsAdded);
        if (pointsAdded > 0
            && (user.Stats.LastPointsEarned is null || occurredAt > Waktu.KeMs(user.Stats.LastPointsEarned.Value)))
        {
            user.Stats.LastPointsEarned = Waktu.DariMs(occurredAt);
        }

        var newStage = _dynamics.ResolveStage(user, user.Points);
        state.CurrentStage = newStage.Id;

        var newLevel = _evaluator.CalculateLevelForStage(user.Points, newStage.Id);
        var leveledUp = newLevel > oldLevel;
        user.Level = newLevel;

        _ = oldStage; // dipakai pustaka asli untuk event stage_down; tidak ada pemantau di sini

        return new InteractionResult
        {
            PointsAdded = pointsAdded,
            TotalPoints = user.Points,
            LeveledUp = leveledUp,
            NewLevel = leveledUp ? newLevel : null,
            TriggeredActions = [],
        };
    }

    // ── Pengguna ────────────────────────────────────────────────────────────

    private KizunaUser? GetUser(string id) => _users.TryGetValue(id, out var u) ? u : null;

    private KizunaUser GetOrCreateUser(long ts, bool isOwner)
    {
        if (GetUser(UserId) is { } existing)
        {
            UpdateUserActivity(existing, ts);
            return existing;
        }

        var role = isOwner ? "owner" : "guest";
        var initialPoints = role == "owner" ? _config.Owner.InitialPoints : 0;
        var points = double.IsFinite(initialPoints) && initialPoints > 0 ? initialPoints : 0;

        var user = new KizunaUser
        {
            Id = UserId,
            DisplayName = UserId,
            Role = role,
            Points = points,
            Level = _evaluator.CalculateLevel(points),
            Stats = new KizunaStats
            {
                TotalInteractions = 1,
                TotalPointsEarned = 0,
                Continuity = _evaluator.CreateContinuity(ts),
                FavoriteEmotions = new Dictionary<string, int>(StringComparer.Ordinal),
                InteractionHistory = [],
            },
            FirstSeen = Waktu.DariMs(ts),
            LastSeen = Waktu.DariMs(ts),
        };

        user.Stats.Dynamics = _dynamics.CreateState(ts, points);
        _users[UserId] = user;
        return user;
    }

    private void UpdateUserActivity(KizunaUser user, long ts)
    {
        if (ts < Waktu.KeMs(user.FirstSeen))
        {
            user.FirstSeen = Waktu.DariMs(ts);
        }

        if (ts > Waktu.KeMs(user.LastSeen))
        {
            user.LastSeen = Waktu.DariMs(ts);
        }

        user.Stats.TotalInteractions++;
        _evaluator.UpdateContinuity(user.Stats.Continuity, ts, _config.Continuity.Grace);
    }

    private void AddInteractionRecord(
        KizunaUser user,
        long ts,
        double pointsEarned,
        string kind,
        string text,
        string valence,
        string? severity)
    {
        var rekam = new InteractionRecord
        {
            Id = $"interaction_{_now()}_{Random.Shared.Next():x}",
            Timestamp = Waktu.DariMs(ts),
            Points = pointsEarned,
            Message = text,
            Kind = kind,
            Valence = valence,
            Severity = severity,
        };

        user.Stats.InteractionHistory.Add(rekam);
        if (user.Stats.InteractionHistory.Count > 100)
        {
            user.Stats.InteractionHistory = [.. user.Stats.InteractionHistory.TakeLast(100)];
        }
    }

    // ── Snapshot & konteks ──────────────────────────────────────────────────

    public BondRawSnapshot? GetBondSnapshot()
    {
        if (GetUser(UserId) is not { } user)
        {
            return null;
        }

        var state = _dynamics.EnsureState(user);
        var warmth = _dynamics.GetWarmth(user, _now());

        return new BondRawSnapshot
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Stage = state.CurrentStage,
            Level = user.Level,
            Points = user.Points,
            Warmth = warmth,
            Trend = state.Trend,
            Atmosphere = BondDynamics.GetAtmosphere(warmth),
            Streak = user.Stats.Continuity.Streak,
            LastContactAt = user.Stats.Continuity.LastContactAt,
            FavoriteEmotions =
            [
                .. user.Stats.FavoriteEmotions
                    .Select(x => (x.Key, x.Value))
                    .OrderByDescending(x => x.Value),
            ],
            Scars = user.Scars,
            Achievements = user.Achievements,
        };
    }

    /// <summary>Port <c>SilverWolfKizuna.getSnapshot()</c>.</summary>
    public BondSnapshot GetSnapshot()
    {
        var raw = _initialized ? GetBondSnapshot() : null;
        if (raw is null)
        {
            return BondSnapshot.Awal(UserId);
        }

        var stageId = BondStages.DariPoin(raw.Points);
        var info = BondStages.Untuk(stageId);
        var range = Math.Max(1, info.NextPoints - info.MinPoints);
        var progress = Math.Min(100, Math.Max(0, (int)Math.Round(
            ((raw.Points - info.MinPoints) / range) * 100, MidpointRounding.AwayFromZero)));

        return new BondSnapshot
        {
            UserId = raw.UserId,
            Stage = stageId,
            StageLabel = info.Label,
            StageName = info.Name,
            Level = info.Level,
            Points = raw.Points,
            NextPoints = info.NextPoints,
            Progress = progress,
            Warmth = Math.Min(1, Math.Max(0, raw.Warmth)),
            Atmosphere = raw.Atmosphere,
            Trend = raw.Trend,
            Tone = info.Tone,
            LastContact = raw.LastContactAt,
            Streak = raw.Streak,
        };
    }

    /// <summary>Port <c>SilverWolfKizuna.getBondContext()</c>.</summary>
    public string GetBondContext()
    {
        if (!_initialized || GetBondSnapshot() is not { } raw)
        {
            return string.Empty;
        }

        var snap = GetSnapshot();
        var ctx = BondContextBuilder.Build(raw, _config.Context.DefaultLanguage);

        return string.Join('\n',
            $"## Hubungan & Ikatan (Kizuna Level {snap.Level} - {snap.StageName}: \"{snap.StageLabel}\")",
            $"- Total Poin Ikatan: {FormatPoin(snap.Points)} XP (Kemajuan ke level berikutnya: {snap.Progress}%)",
            $"- Kehangatan Hubungan: {Math.Round(snap.Warmth * 100, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}% (Suasana: {snap.Atmosphere}, Tren: {snap.Trend})",
            $"- Panduan Sikap: {snap.Tone}",
            $"- Raw Context: {ctx.Trim()}");
    }

    private static string FormatPoin(double poin) =>
        poin == Math.Floor(poin)
            ? ((long)poin).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : poin.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // ── Pembersihan ─────────────────────────────────────────────────────────

    /// <summary>
    /// Port <c>performCleanup()</c>. Pustaka asli menjadwalkannya tiap
    /// <c>cleanupIntervalHours</c> (24 jam). Di sini sengaja tidak memakai timer
    /// internal supaya bisa diuji; host (Services/App) yang menjadwalkannya.
    ///
    /// Perhatikan: pengguna bernama "master" berperan <b>guest</b> (aplikasi
    /// tidak pernah mengirim <c>isOwner</c>), jadi aturan retensi 90 hari ini
    /// benar-benar berlaku padanya — sama seperti aplikasi lama.
    /// </summary>
    public void PerformCleanup()
    {
        var now = _now();
        var retentionMs = _config.Storage.DataRetentionDays * DayMs;

        foreach (var id in _users
            .Where(u => u.Value.Role != "owner" && now - Waktu.KeMs(u.Value.LastSeen) > retentionMs)
            .Select(u => u.Key)
            .ToList())
        {
            _users.Remove(id);
        }

        var excess = _users.Count - _config.Storage.MaxUsers;
        if (excess <= 0)
        {
            return;
        }

        var tertua = _users.Values
            .Where(u => u.Role != "owner")
            .OrderBy(u => Waktu.KeMs(u.LastSeen))
            .Take(excess)
            .Select(u => u.Id)
            .ToList();

        foreach (var id in tertua)
        {
            _users.Remove(id);
        }
    }

    // ── Persistensi ─────────────────────────────────────────────────────────

    private async Task LoadFromStorageAsync(CancellationToken ct)
    {
        if (_storage is null)
        {
            return;
        }

        var data = await _storage.LoadAsync(_storageKey, ct).ConfigureAwait(false);
        if (data is null)
        {
            return;
        }

        if (IsPersistenceEnvelope(data))
        {
            foreach (var (id, user) in data.Users)
            {
                _users[id] = NormalisasiUser(id, user);
            }

            _sessionCounter = data.SessionCounter;
        }
    }

    private async Task SaveToStorageAsync(CancellationToken ct)
    {
        if (_storage is null)
        {
            return;
        }

        var snapshot = new KizunaEnvelope
        {
            Users = new Dictionary<string, KizunaUser>(_users, StringComparer.Ordinal),
            SessionCounter = _sessionCounter,
            LimitRecords = [],
        };

        await _storage.SaveAsync(_storageKey, snapshot, ct).ConfigureAwait(false);
    }

    private static bool IsPersistenceEnvelope(KizunaEnvelope e) =>
        e.Format == "@aituber-onair/kizuna"
        && e.Version == 1
        && e.Users is not null
        && e.SessionCounter >= 0
        && e.LimitRecords is not null;

    /// <summary>Port <c>normalizeUser()</c>: amankan data yang dibaca dari berkas.</summary>
    private KizunaUser NormalisasiUser(string id, KizunaUser data)
    {
        var role = data.Role == "owner" ? "owner" : "guest";
        var lastSeen = data.LastSeen;
        var points = Math.Max(0, data.Points);

        var continuity = data.Stats?.Continuity ?? _evaluator.CreateContinuity(Waktu.KeMs(lastSeen));
        var dinamika = data.Stats?.Dynamics ?? _dynamics.CreateState(Waktu.KeMs(lastSeen), points);
        dinamika.Warmth = Math.Min(1, Math.Max(0, dinamika.Warmth));
        dinamika.CurrentStage = _evaluator.GetStageIndex(dinamika.CurrentStage) >= 0
            ? dinamika.CurrentStage
            : _evaluator.ResolveStage(points).Id;

        var stats = data.Stats ?? new KizunaStats();

        return new KizunaUser
        {
            Id = id,
            DisplayName = string.IsNullOrEmpty(data.DisplayName) ? id : data.DisplayName,
            Role = role,
            Points = points,
            Level = _evaluator.CalculateLevelForStage(points, dinamika.CurrentStage),
            Achievements = data.Achievements ?? [],
            Scars = data.Scars ?? [],
            TriggeredThresholds = data.TriggeredThresholds ?? [],
            Stats = new KizunaStats
            {
                TotalInteractions = stats.TotalInteractions,
                TotalPointsEarned = stats.TotalPointsEarned,
                Continuity = continuity,
                FavoriteEmotions = stats.FavoriteEmotions ?? new Dictionary<string, int>(StringComparer.Ordinal),
                LastPointsEarned = stats.LastPointsEarned,
                InteractionHistory = stats.InteractionHistory ?? [],
                Dynamics = dinamika,
            },
            FirstSeen = data.FirstSeen,
            LastSeen = lastSeen,
            CustomData = data.CustomData ?? new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }
}
