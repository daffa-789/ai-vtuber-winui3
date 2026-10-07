namespace SilverWolf.Core.Domain.Kizuna;

/// <summary>
/// Snapshot ikatan yang dilihat pengguna — port
/// <c>SilverWolfKizuna.getSnapshot()</c> dari
/// <c>apps/server-node/src/kizuna.js</c>.
///
/// Ini yang dikirim ke klien lewat <c>/api/health</c> dan <c>/api/kizuna</c>
/// dan yang dipakai HUD: stage, label, level, poin, kemajuan, kehangatan,
/// atmosfer, tren, dan tone.
/// </summary>
public sealed class BondSnapshot
{
    public string UserId { get; set; } = "master";

    public string Stage { get; set; } = "stranger";

    public string StageLabel { get; set; } = BondStages.Info["stranger"].Label;

    public string StageName { get; set; } = BondStages.Info["stranger"].Name;

    public int Level { get; set; } = 1;

    public double Points { get; set; }

    public int NextPoints { get; set; } = 100;

    /// <summary>Persentase kemajuan menuju tahap berikutnya (0–100).</summary>
    public int Progress { get; set; }

    public double Warmth { get; set; } = 1;

    public string Atmosphere { get; set; } = "warm";

    public string Trend { get; set; } = "rising";

    public string Tone { get; set; } = BondStages.Info["stranger"].Tone;

    public DateTime LastContact { get; set; }

    public int Streak { get; set; } = 1;

    /// <summary>Nilai awal saat mesin belum diinisialisasi — persis seperti aplikasi lama.</summary>
    public static BondSnapshot Awal(string userId = "master") => new()
    {
        UserId = userId,
        LastContact = DateTime.UtcNow,
    };
}

/// <summary>
/// Snapshot mentah mesin (bedanya: stage di sini memakai hysteresis, bukan murni poin).
/// Dipakai hanya untuk menyusun konteks bond.
/// </summary>
public sealed class BondRawSnapshot
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = "guest";

    public string Stage { get; set; } = "stranger";

    public int Level { get; set; } = 1;

    public double Points { get; set; }

    public double Warmth { get; set; } = 1;

    public string Trend { get; set; } = "steady";

    public string Atmosphere { get; set; } = "warm";

    public int Streak { get; set; } = 1;

    public DateTime LastContactAt { get; set; }

    public List<(string Emotion, int Count)> FavoriteEmotions { get; set; } = [];

    public List<KizunaScar> Scars { get; set; } = [];

    public List<KizunaAchievement> Achievements { get; set; } = [];
}
