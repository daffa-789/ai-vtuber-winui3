using SilverWolf.Core.Configuration;
using SilverWolf.Core.Domain;
using SilverWolf.Core.Domain.Kizuna;
using SilverWolf.Services.Agent;
using SilverWolf.Services.Backend;
using SilverWolf.Services.Configuration;
using SilverWolf.Services.Inference;
using SilverWolf.Services.Llama;

namespace SilverWolf.Services.Bootstrap;

/// <summary>
/// Perakitan seluruh runtime — port <c>jalankan()</c> dan <c>pilihProvider()</c>
/// dari <c>apps/server-node/src/main.js</c>.
///
/// Di aplikasi lama ini adalah fungsi yang menyalakan server HTTP. Sekarang ia
/// mengembalikan objek yang bisa dipanggil langsung oleh <c>SilverWolf.App</c>,
/// tanpa soket dan tanpa proses Node.
/// </summary>
public sealed class CompanionRuntime : IAsyncDisposable
{
    /// <summary>Padanan <c>PERSONA_CADANGAN</c> di <c>main.js</c>.</summary>
    public const string PersonaCadangan =
        "Kamu adalah Silver Wolf, hacker Punklorde dari Stellaron Hunters. " +
        "Kamu memanggil pengguna Master atau Sayang, bicara santai, manis, dan akrab.";

    public AppConfig Konfig { get; }

    public CharacterVault Vault { get; }

    public KizunaEngine Kizuna { get; }

    public TieredMemoryEngine Memory { get; }

    public ILlmProvider Provider { get; }

    public AgentService Agent { get; }

    public CompanionBackend Backend { get; }

    public string ModelName { get; }

    /// <summary>Proses llama-server bila provider-nya lokal. Null untuk stub/ollama.</summary>
    public LlamaServerProcess? Llama { get; }

    /// <summary>Peringatan konfigurasi — aplikasi lama mencetaknya ke stderr.</summary>
    public IReadOnlyList<string> Peringatan => Konfig.Warnings;

    private CompanionRuntime(
        AppConfig konfig,
        CharacterVault vault,
        KizunaEngine kizuna,
        TieredMemoryEngine memory,
        ILlmProvider provider,
        AgentService agent,
        CompanionBackend backend,
        string modelName,
        LlamaServerProcess? llama)
    {
        Konfig = konfig;
        Vault = vault;
        Kizuna = kizuna;
        Memory = memory;
        Provider = provider;
        Agent = agent;
        Backend = backend;
        ModelName = modelName;
        Llama = llama;
    }

    public static async Task<CompanionRuntime> StartAsync(
        string? akar = null,
        Action<string>? log = null,
        Action<Exception>? onError = null,
        bool? muatBoot = null,
        CancellationToken ct = default)
    {
        var root = AppPaths.TentukanAkar(akar);

        var jalurEnv = AppPaths.KonfigEnv(root);
        var env = new EnvSource
        {
            File = File.Exists(jalurEnv)
                ? EnvFile.Parse(await File.ReadAllTextAsync(jalurEnv, ct).ConfigureAwait(false))
                : new Dictionary<string, string>(StringComparer.Ordinal),
            Environ = BacaLingkungan(),
        };

        var konfig = ConfigReader.Baca(env, root);

        string persona;
        try
        {
            persona = await File.ReadAllTextAsync(konfig.AkarPersona, ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            log?.Invoke($"! {error.Message}; memakai persona cadangan minimal");
            persona = PersonaCadangan;
        }

        var vault = new CharacterVault(AppPaths.Memori(root));
        var memory = new TieredMemoryEngine(vault);

        var kizuna = new KizunaEngine(
            KizunaConfig.CreateSilverWolf(),
            new FileKizunaStorage(AppPaths.Kizuna(root)),
            userId: "master",
            storageKey: "silverwolf_kizuna_v1");

        try
        {
            await kizuna.InitializeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            log?.Invoke($"! gagal inisialisasi kizuna: {error.Message}");
        }

        var pilihan = PilihProvider(konfig, log);

        if (pilihan.Llama is { } proses && (muatBoot ?? konfig.VulkanMuatBoot))
        {
            _ = Task.Run(async () =>
            {
                var hasil = await proses.StartAsync(ct).ConfigureAwait(false);
                if (!hasil.Ok)
                {
                    log?.Invoke($"! inferensi lokal belum siap: {hasil.Reason}");
                }
            }, ct);
        }

        // Profil Master dibaca sekali saat menyala supaya alat waktu bisa langsung
        // menghitung ulang tahun & umur sejak giliran pertama.
        MasterProfile? profil = null;
        try
        {
            profil = await vault.BacaProfilAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            log?.Invoke($"! profil master belum terbaca: {error.Message}");
        }

        var agent = new AgentService(
            pilihan.Provider,
            persona,
            vault,
            kizuna,
            memory,
            profil,
            localPrompt: konfig.LlmProvider != "ollama",
            onError: onError,
            // Jejak cemburu ikut ke log supaya Master bisa memastikan fiturnya
            // menyala tanpa harus mendengar suaranya.
            onLog: log);

        var backend = new CompanionBackend(konfig, agent, pilihan.Provider, vault, kizuna, pilihan.ModelName);

        return new CompanionRuntime(
            konfig, vault, kizuna, memory, pilihan.Provider, agent, backend, pilihan.ModelName, pilihan.Llama);
    }

    /// <summary>Port <c>pilihProvider(konfig)</c>.</summary>
    private static (ILlmProvider Provider, string ModelName, LlamaServerProcess? Llama) PilihProvider(
        AppConfig konfig, Action<string>? log)
    {
        if (konfig.Stub)
        {
            return (new StubProvider(), "stub", null);
        }

        if (konfig.LlmProvider == "ollama")
        {
            return (
                new OpenAiCompatibleProvider("ollama", konfig.OllamaUrl, konfig.OllamaModel),
                $"ollama/{konfig.OllamaModel}",
                null);
        }

        var portInferensi = konfig.PortInferensi;
        var proses = new LlamaServerProcess(konfig, portInferensi, log);
        var model = ModelLocator.CariModel(konfig, pesan => log?.Invoke(pesan));
        var alias = ModelLocator.AliasModel(konfig, model.Path);
        var awalan = konfig.LlmProvider == "vulkan" ? "vulkan" : "local";

        return (
            new OpenAiCompatibleProvider("llama-server", $"http://127.0.0.1:{portInferensi}", alias),
            $"{awalan}/{alias}",
            proses);
    }

    private static Dictionary<string, string> BacaLingkungan()
    {
        var hasil = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entri in Environment.GetEnvironmentVariables())
        {
            if (entri.Key is string kunci && entri.Value is string nilai)
            {
                hasil[kunci] = nilai;
            }
        }

        return hasil;
    }

    public async ValueTask DisposeAsync()
    {
        Kizuna.Destroy();
        Llama?.Stop();
        await Provider.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
