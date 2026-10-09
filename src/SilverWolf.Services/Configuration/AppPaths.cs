namespace SilverWolf.Services.Configuration;

/// <summary>
/// Penemu jalur instalasi — pengembangan dari <c>cariAkarRepo()</c> di
/// <c>apps/server-node/src/config.js</c>.
///
/// Aplikasi lama berjalan dari dalam repo Electron dan menemukan akarnya lewat
/// <c>package.json</c> / <c>.git</c>. Aplikasi baru tidak punya keduanya, jadi
/// <c>SilverWolf.sln</c> ditambahkan sebagai penanda akar.
///
/// ⚠️ Jangan pakai keberadaan folder <c>assets</c> sebagai penanda. Windows
/// tidak membedakan huruf besar-kecil, sehingga <c>src/SilverWolf.App/Assets/</c>
/// (logo template WinUI) akan cocok dan akarnya salah terdeteksi.
/// </summary>
public static class AppPaths
{
    public const int MaksKedalaman = 12;

    /// <summary>
    /// Penanda akar. <c>package.json</c> dan <c>.git</c> dipertahankan supaya
    /// aplikasi baru tetap bisa dijalankan dari dalam repo lama (mis. saat
    /// membandingkan perilaku dengan aplikasi Electron).
    /// </summary>
    private static readonly string[] PenandaBerkas =
    [
        "SilverWolf.sln",
        "SilverWolf.slnx",
        "package.json",
    ];

    private static readonly string[] PenandaFolder = [".git"];

    /// <summary>
    /// Naiki direktori dari <paramref name="mulai"/> sampai ketemu penanda akar.
    /// Mengembalikan <c>null</c> bila tidak ketemu.
    /// </summary>
    public static string? CariAkarRepo(string? mulai = null)
    {
        var dir = Path.GetFullPath(mulai ?? AppContext.BaseDirectory);

        for (var i = 0; i < MaksKedalaman; i++)
        {
            if (PunyaPenanda(dir))
            {
                return dir;
            }

            var naik = Path.GetDirectoryName(dir);
            if (naik is null || naik == dir)
            {
                break;
            }

            dir = naik;
        }

        return null;
    }

    private static bool PunyaPenanda(string dir) =>
        PenandaBerkas.Any(nama => File.Exists(Path.Combine(dir, nama)))
        || PenandaFolder.Any(nama => Directory.Exists(Path.Combine(dir, nama)));

    /// <summary>
    /// Akar yang dipakai runtime. Urutan: parameter &gt; <c>VTUBER_ROOT</c> &gt;
    /// penelusuran &gt; direktori kerja.
    /// </summary>
    public static string TentukanAkar(string? eksplisit = null)
    {
        if (!string.IsNullOrWhiteSpace(eksplisit))
        {
            return Path.GetFullPath(eksplisit);
        }

        var dariEnv = Environment.GetEnvironmentVariable("VTUBER_ROOT");
        if (!string.IsNullOrWhiteSpace(dariEnv))
        {
            return Path.GetFullPath(dariEnv);
        }

        return CariAkarRepo() ?? Directory.GetCurrentDirectory();
    }

    // ── Jalur turunan ───────────────────────────────────────────────────────

    public static string Aset(string akar) => Path.Combine(akar, "assets");

    public static string Memori(string akar) => Path.Combine(akar, "silver_wolf_memory");

    public static string Kizuna(string akar) => Path.Combine(Memori(akar), "kizuna");

    /// <summary>
    /// Arsip audio balasan. Berbeda dari cache TTS di <c>%TEMP%</c> (yang
    /// dibuang saat aplikasi ditutup), arsip ini menetap supaya balasan lama
    /// bisa diputar ulang. Dipangkas bergilir ke sejumlah berkas terbaru —
    /// lihat <c>SuaraArsip</c>.
    /// </summary>
    public static string Suara(string akar) => Path.Combine(Memori(akar), "suara");

    public static string Model(string akar) => Path.Combine(akar, "model");

    public static string Llama(string akar) => Path.Combine(akar, "bin", "llama");

    /// <summary>
    /// Folder model Live2D. Di aplikasi lama ini dilayani sebagai
    /// <c>/models/silverwolf</c> dari <c>assets/live2d</c> — lihat
    /// <c>sajiStatis(res, join(o.assetRoot, 'live2d'), …)</c> di <c>server.js</c>.
    /// </summary>
    public static string Live2D(string akar) => Path.Combine(Aset(akar), "live2d", "silverwolf");

    public static string ModelSuaraPiper(string akar) => Path.Combine(Aset(akar), "piper");

    public static string KonfigEnv(string akar) => Path.Combine(akar, ".env");

    /// <summary>
    /// Font yang dibundel lokal. Aplikasi lama mengambilnya dari
    /// <c>fonts.googleapis.com</c> saat runtime; aplikasi desktop harus bisa
    /// jalan offline, jadi ketiganya disimpan di sini. Lihat
    /// <c>assets/fonts/README.md</c>.
    /// </summary>
    public static string Font(string akar) => Path.Combine(Aset(akar), "fonts");

    public static string Ikon(string akar) => Path.Combine(Aset(akar), "ikon");

    /// <summary>
    /// Periksa jalur yang wajib ada — dipakai untuk diagnostik saat aset belum
    /// lengkap di mesin ini.
    /// </summary>
    public static IReadOnlyList<(string Nama, string Jalur, bool Ada)> Periksa(string akar)
    {
        var daftar = new (string Nama, string Jalur)[]
        {
            ("memori karakter", Memori(akar)),
            ("persona", Path.Combine(Memori(akar), "persona.md")),
            ("model LLM", Model(akar)),
            ("binary llama-server", Llama(akar)),
            ("model suara Piper", ModelSuaraPiper(akar)),
            ("model Live2D", Live2D(akar)),
            ("font lokal", Font(akar)),
            ("ikon aplikasi", Ikon(akar)),
        };

        return [.. daftar.Select(x => (x.Nama, x.Jalur, Ada: File.Exists(x.Jalur) || Directory.Exists(x.Jalur)))];
    }
}
