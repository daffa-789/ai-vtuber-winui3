namespace SilverWolf.Core.Configuration;

/// <summary>
/// Port <c>bacaKonfig(env, akar)</c> dari <c>apps/server-node/src/config.js</c>.
///
/// Nama properti mengikuti identifier Baru (Inggris) sesuai konvensi proyek,
/// tetapi <b>nilai string yang dilihat pengguna tetap Indonesia</b>
/// ("siap", "memuat", "tidak-jalan", "pet", "browser", "layar-penuh") karena
/// nilai-nilai itu dikirim ke klien lewat <c>/api/health</c> dan dipakai UI.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Akar repo — dipakai untuk menjawab semua jalur relatif.</summary>
    public string Akar { get; set; } = string.Empty;

    /// <summary>Jalur berkas persona yang ditemukan (bisa belum ada).</summary>
    public string AkarPersona { get; set; } = string.Empty;

    /// <summary>Peringatan konversi dari <see cref="EnvSource"/>.</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>VTUBER_STUB — pakai provider stub tanpa LLM.</summary>
    public bool Stub { get; set; }

    // ── Wujud ────────────────────────────────────────────────────────────────

    public int Port { get; set; } = 8787;

    /// <summary>VTUBER_TAMPAK — "pet" atau "browser".</summary>
    public string Tampak { get; set; } = "pet";

    /// <summary>VTUBER_PET_SEMBUNYI — "tidak" | "layar-penuh" | "maksimal".</summary>
    public string PetSembunyi { get; set; } = "layar-penuh";

    public bool PetTray { get; set; } = true;

    public string PetHotkey { get; set; } = "ctrl+shift+s";

    // ── Otak ─────────────────────────────────────────────────────────────────

    /// <summary>VTUBER_LLM_PROVIDER yang sudah dinormalisasi: "vulkan" | "local" | "ollama".</summary>
    public string LlmProvider { get; set; } = "vulkan";

    public string LocalModelPath { get; set; } = "model/gemma-4-E4B-it-UD-Q4_K_XL.gguf";
    public int LocalModelThreads { get; set; } = 4;
    public int LocalModelCtx { get; set; } = 8192;
    public string LocalModelAlias { get; set; } = "gemma-4b";
    public double LocalMinP { get; set; }
    public double LocalTopP { get; set; } = 0.95;

    /// <summary>VTUBER_LOCAL_REASONING — "on" | "off" | "auto".</summary>
    public string LocalReasoning { get; set; } = "off";

    public string LlamaServer { get; set; } = "bin/llama";
    public int VulkanNgl { get; set; } = 99;
    public bool VulkanFa { get; set; } = true;
    public int VulkanCtx { get; set; } = 8192;

    /// <summary>
    /// VTUBER_VULKAN_CACHE_TYPE — kuantisasi KV cache Vulkan.
    /// "q8_0" (default, hemat memori ~2×) atau "f16" (lebih cepat, 2× memori).
    /// </summary>
    public string VulkanCacheType { get; set; } = "q8_0";

    public string VulkanPerangkat { get; set; } = string.Empty;
    public bool VulkanMuatBoot { get; set; } = true;
    public bool VulkanSlotDiam { get; set; } = true;
    public string OllamaUrl { get; set; } = "http://127.0.0.1:11434";
    public string OllamaModel { get; set; } = "llama3.2:3b";

    // ── STT ──────────────────────────────────────────────────────────────────

    public bool SttHidup { get; set; } = true;
    public string SttModel { get; set; } = "base";
    public string SttModelPath { get; set; } = "assets/whisper";

    // ── TTS (M11) ────────────────────────────────────────────────────────────

    /// <summary>VTUBER_TTS — hidupkan/matikan suara.</summary>
    public bool TtsHidup { get; set; } = true;

    /// <summary>
    /// VTUBER_TTS_RANTAI — urutan rantai yang dicoba, dipisah koma.
    /// Contoh: "piper+rvc,piper" berarti coba Piper+RVC dulu; kalau gagal,
    /// jatuh ke Piper saja. Urutan ini penting: RVC bisa gagal (index/torch)
    /// dan kita tetap ingin ada suara, bukan diam.
    /// </summary>
    public string TtsRantai { get; set; } = "piper+rvc,piper";

    /// <summary>
    /// VTUBER_TTS_PER_KALIMAT — proses per kalimat lalu putar berurutan.
    /// Ini bukan sekadar optimasi: RVC di CPU berjalan RTF 1,1–2,5×, jadi
    /// menunggu seluruh balasan selesai akan terasa sangat lambat.
    /// </summary>
    public bool TtsPerKalimat { get; set; } = true;

    /// <summary>VTUBER_TTS_BATAS_DETIK — batas waktu satu kalimat diproses.</summary>
    public int TtsBatasDetik { get; set; } = 40;

    /// <summary>VTUBER_TTS_CACHE — simpan hasil WAV agar kalimat berulang tak dihitung ulang.</summary>
    public bool TtsCache { get; set; } = true;

    /// <summary>VTUBER_RVC — apakah tahap RVC dipakai.</summary>
    public bool Rvc { get; set; } = true;

    public string RvcModel { get; set; } = "SilverWolfJP";
    public string RvcVersi { get; set; } = "v2";
    public string RvcF0 { get; set; } = "rmvpe";
    public int RvcTranspose { get; set; } = -3;
    public double RvcIndeksLaju { get; set; } = 0.6;

    /// <summary>
    /// Jalur Python untuk rantai TTS. Kalau kosong, dicari otomatis dari
    /// <c>VTUBER_PY_RVC</c> lalu dari daftar kandidat yang dikenal.
    /// </summary>
    public string PyRvc { get; set; } = string.Empty;

    // ── Batas server (konstanta di aplikasi lama) ────────────────────────────

    public int MaksBody { get; set; } = 1024 * 1024;
    public int MaksPesan { get; set; } = 64;
    public int MaksKarakter { get; set; } = 8192;

    /// <summary>
    /// Port inferensi llama-server. Di aplikasi lama dihitung di
    /// <c>pilihProvider()</c>: <c>port !== 0 ? port + 1 : 18788</c>.
    /// </summary>
    public int PortInferensi => Port != 0 ? Port + 1 : 18788;

    /// <summary>Port <c>readAll</c> dipakai hanya bila provider membutuhkannya.</summary>
    public bool PakaiLlamaLokal => !Stub && LlmProvider != "ollama";
}

/// <summary>Pembaca konfigurasi + penemu berkas persona.</summary>
public static class ConfigReader
{
    private static readonly string[] KandidatPersona =
    [
        "silver_wolf_memory/persona.md",
        "silver wolf memory/persona.md",
        "memori-waifu/persona.md",
        "persona.md",
    ];

    /// <summary>
    /// Port <c>temukanPersona(akar)</c>. Mengembalikan kandidat pertama yang ada;
    /// kalau tidak ada, tetap mengembalikan kandidat pertama (perilaku lama:
    /// pembacaan persona nanti gagal dan jatuh ke persona cadangan).
    /// </summary>
    public static string TemukanPersona(string akar)
    {
        foreach (var rel in KandidatPersona)
        {
            var path = Path.Combine(akar, rel);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(akar, KandidatPersona[0]);
    }

    /// <summary>Port <c>bacaKonfig(env, akar)</c>.</summary>
    public static AppConfig Baca(EnvSource env, string akar)
    {
        var rawProvider = env.Value("VTUBER_LLM_PROVIDER", "vulkan");
        var llmProvider = "vulkan";
        if (rawProvider is "local" or "llama_cpp")
        {
            llmProvider = "local";
        }
        else if (rawProvider == "ollama")
        {
            llmProvider = "ollama";
        }

        var rawTampak = env.Value("VTUBER_TAMPAK", "pet");
        var rawPetSembunyi = env.Value("VTUBER_PET_SEMBUNYI", "layar-penuh");
        var rawReasoning = env.Value("VTUBER_LOCAL_REASONING", "off");
        var rawCacheType = env.Value("VTUBER_VULKAN_CACHE_TYPE", "q8_0");

        return new AppConfig
        {
            Akar = akar,
            AkarPersona = TemukanPersona(akar),
            Warnings = env.Warnings,
            Stub = env.Bool("VTUBER_STUB", false),

            Port = env.Int("VTUBER_PORT", 8787),
            Tampak = rawTampak == "browser" ? "browser" : "pet",
            PetSembunyi = rawPetSembunyi is "tidak" or "layar-penuh" or "maksimal" ? rawPetSembunyi : "layar-penuh",
            PetTray = env.Bool("VTUBER_PET_TRAY", true),
            PetHotkey = env.Value("VTUBER_PET_HOTKEY", "ctrl+shift+s"),

            LlmProvider = llmProvider,
            LocalModelPath = env.Value("VTUBER_LOCAL_MODEL_PATH", "model/gemma-4-E4B-it-UD-Q4_K_XL.gguf"),
            LocalModelThreads = env.Int("VTUBER_LOCAL_MODEL_THREADS", 4),
            LocalModelCtx = env.Int("VTUBER_LOCAL_MODEL_CTX", 8192),
            LocalModelAlias = env.Value("VTUBER_LOCAL_MODEL_ALIAS", "gemma-4b"),
            LocalMinP = env.Float("VTUBER_LOCAL_MIN_P", 0),
            LocalTopP = env.Float("VTUBER_LOCAL_TOP_P", 0.95),
            LocalReasoning = rawReasoning is "on" or "off" or "auto" ? rawReasoning : "off",
            LlamaServer = env.Value("VTUBER_LLAMA_SERVER", "bin/llama"),
            VulkanNgl = env.Int("VTUBER_VULKAN_NGL", 99),
            VulkanFa = env.Bool("VTUBER_VULKAN_FA", true),
            VulkanCtx = env.Int("VTUBER_VULKAN_CTX", 8192),
            VulkanCacheType = rawCacheType is "f16" or "q8_0" or "q4_0" ? rawCacheType : "q8_0",
            VulkanPerangkat = env.Value("VTUBER_VULKAN_PERANGKAT", string.Empty),
            VulkanMuatBoot = env.Bool("VTUBER_VULKAN_MUAT_BOOT", true),
            VulkanSlotDiam = env.Bool("VTUBER_VULKAN_SLOT_DIAM", true),
            OllamaUrl = env.Value("VTUBER_OLLAMA_URL", "http://127.0.0.1:11434"),
            OllamaModel = env.Value("VTUBER_OLLAMA_MODEL", "llama3.2:3b"),

            SttHidup = env.Bool("VTUBER_STT", true),
            SttModel = env.Value("VTUBER_STT_MODEL", "base"),
            SttModelPath = env.Value("VTUBER_STT_MODEL_PATH", "assets/whisper"),

            // ── TTS (M11) ────────────────────────────────────────────────────
            // Semua nilai dibaca dari .env; itu satu-satunya sumber kebenaran
            // (lihat tools/tts/README.md). Profil suara terkunci:
            // transpose -3, index_rate 0.6, f0 rmvpe.
            TtsHidup = env.Bool("VTUBER_TTS", true),
            TtsRantai = env.Value("VTUBER_TTS_RANTAI", "piper+rvc,piper"),
            TtsPerKalimat = env.Bool("VTUBER_TTS_PER_KALIMAT", true),
            TtsBatasDetik = env.Int("VTUBER_TTS_BATAS_DETIK", 40),
            TtsCache = env.Bool("VTUBER_TTS_CACHE", true),
            Rvc = env.Bool("VTUBER_RVC", true),
            RvcModel = env.Value("VTUBER_RVC_MODEL", "SilverWolfJP"),
            RvcVersi = env.Value("VTUBER_RVC_VERSI", "v2"),
            RvcF0 = env.Value("VTUBER_RVC_F0", "rmvpe"),
            RvcTranspose = env.Int("VTUBER_RVC_TRANSPOSE", -3),
            RvcIndeksLaju = env.Float("VTUBER_RVC_INDEKS_LAJU", 0.6),
            PyRvc = env.Value("VTUBER_PY_RVC", string.Empty),
        };
    }
}
