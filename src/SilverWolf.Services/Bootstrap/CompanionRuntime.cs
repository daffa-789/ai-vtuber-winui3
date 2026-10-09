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

    /// <summary>
    /// Nama model yang dilaporkan health. Berubah bersama <see cref="GantiModelAsync"/>.
    /// </summary>
    public string ModelName { get; private set; }

    /// <summary>Proses llama-server bila provider-nya lokal. Null untuk stub/ollama.</summary>
    public LlamaServerProcess? Llama { get; private set; }

    /// <summary>Peringatan konfigurasi — aplikasi lama mencetaknya ke stderr.</summary>
    public IReadOnlyList<string> Peringatan => Konfig.Warnings;

    /// <summary>
    /// Tugas pemuatan model saat boot. Dipakai <see cref="GantiModelAsync"/> untuk
    /// menghentikan pemuatan lama yang belum selesai sebelum memulai yang baru —
    /// tanpa ini dua <c>llama-server</c> bisa hidup bersamaan dan memperebutkan
    /// port yang sama.
    /// </summary>
    private Task? _muatBoot;

    /// <summary>Sumber pembatalan khusus pemuatan boot; dipisah dari token pemanggil.</summary>
    private CancellationTokenSource? _batalBoot;

    /// <summary>
    /// Kunci pemuatan. Menjamin hanya satu pemuatan model berjalan pada satu
    /// waktu, berapa pun kali pengguna mengganti model berturut-turut.
    /// </summary>
    private readonly SemaphoreSlim _kunciMuat = new(1, 1);

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
        string? modelPath = null,
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

        // Pilihan model tersimpan mengalahkan .env bila berkasnya benar-benar
        // ada. .env tetap sumber kebenaran untuk nilai BAWANNYA — preferensi
        // ini hanya menimpa setelah pengguna memilih sendiri.
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var calon = ModelLocator.Absolut(konfig, modelPath);
            if (File.Exists(calon))
            {
                konfig.LocalModelPath = ModelLocator.Relatif(konfig, calon);
            }
            else
            {
                log?.Invoke($"! model tersimpan tidak ditemukan, memakai .env: {calon}");
            }
        }

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

        var runtime = new CompanionRuntime(
            konfig, vault, kizuna, memory, pilihan.Provider, agent, backend, pilihan.ModelName, pilihan.Llama);

        if (pilihan.Llama is { } proses && (muatBoot ?? konfig.VulkanMuatBoot))
        {
            // Token terpisah agar GantiModelAsync bisa membatalkan pemuatan boot
            // tanpa membatalkan seluruh sesi.
            var batal = CancellationTokenSource.CreateLinkedTokenSource(ct);
            runtime._batalBoot = batal;
            runtime._muatBoot = Task.Run(async () =>
            {
                var hasil = await proses.StartAsync(batal.Token).ConfigureAwait(false);
                if (!hasil.Ok)
                {
                    log?.Invoke($"! inferensi lokal belum siap: {hasil.Reason}");
                }
            }, batal.Token);
        }

        return runtime;
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

    /// <summary>Hasil <see cref="GantiModelAsync"/>.</summary>
    public readonly record struct HasilGanti(bool Ok, string Reason, string Nama);

    /// <summary>
    /// Ganti model GGUF yang dipakai inferensi.
    ///
    /// <para>
    /// <b>Kenapa harus mematikan llama-server.</b> Model dipilih lewat argumen
    /// <c>-m</c> saat proses dimulai dan tidak bisa diganti dari dalam; satu
    /// proses hanya memuat satu GGUF. Jadi satu-satunya jalan adalah mematikan
    /// proses lama lalu menjalankan yang baru — itu sebabnya operasi ini
    /// memakan waktu puluhan detik (GGUF 5 GB butuh ~40 dtk masuk VRAM).
    /// </para>
    ///
    /// <para>
    /// <b>Yang tidak ikut berubah.</b> Port, alias, dan URL provider tetap sama,
    /// karena <c>LlamaServerProcess</c> membaca konfigurasinya dari objek
    /// <see cref="AppConfig"/> yang sama dan menyambung kembali ke port yang
    /// sama. Karena itu <c>AgentService</c> tidak perlu dibangun ulang dan
    /// riwayat percakapan tidak hilang.
    /// </para>
    ///
    /// <param name="jalurModel">
    /// Jalur absolut atau relatif terhadap <see cref="AppConfig.Akar"/>.
    /// </param>
    /// </summary>
    public async Task<HasilGanti> GantiModelAsync(string jalurModel, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jalurModel))
        {
            return new HasilGanti(false, "jalur model kosong", ModelName);
        }

        var absolut = ModelLocator.Absolut(Konfig, jalurModel);
        if (!File.Exists(absolut))
        {
            return new HasilGanti(false, $"berkas model tidak ada: {absolut}", ModelName);
        }

        if (Llama is null)
        {
            // stub / ollama: tidak ada proses lokal yang bisa dimuat ulang.
            // Konfigurasi tetap diperbarui supaya health tidak berbohong.
            Konfig.LocalModelPath = ModelLocator.Relatif(Konfig, absolut);
            return new HasilGanti(false, "provider bukan llama-server lokal (tidak ada yang dimuat ulang)", ModelName);
        }

        await _kunciMuat.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 1. Batalkan pemuatan boot yang mungkin masih berjalan, lalu tunggu
            //    sampai benar-benar berhenti. Tanpa ini proses lama tetap hidup
            //    memegang port 8788 dan proses baru gagal menyambung.
            if (_batalBoot is { } batal)
            {
                try
                {
                    batal.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // sudah selesai
                }
            }

            if (_muatBoot is { } tugas)
            {
                try
                {
                    await tugas.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // memang sengaja dibatalkan
                }
                catch (Exception)
                {
                    // kegagalan pemuatan lama tidak menghalangi yang baru
                }

                _muatBoot = null;
                _batalBoot = null;
            }

            // 2. Matikan server lama SEBELUM mengubah jalur model — kalau diubah
            //    lebih dulu dan prosesnya masih hidup, ia tetap melayani model
            //    yang lama dan pengguna melihat hasil yang membingungkan.
            Llama.Stop();

            // 3. Arahkan konfigurasi ke model baru.
            Konfig.LocalModelPath = ModelLocator.Relatif(Konfig, absolut);

            // 4. Muat ulang. StartAsync memanggil ModelLocator.CariModel lagi,
            //    yang kini menemukan LocalModelPath yang baru.
            var hasil = await Llama.StartAsync(ct).ConfigureAwait(false);

            var nama = Path.GetFileNameWithoutExtension(absolut);
            if (hasil.Ok)
            {
                ModelName = $"{(Konfig.LlmProvider == "vulkan" ? "vulkan" : "local")}/" +
                            ModelLocator.AliasModel(Konfig, absolut);
                Backend.ModelName = ModelName;
            }

            return new HasilGanti(hasil.Ok, hasil.Reason, nama);
        }
        catch (OperationCanceledException)
        {
            return new HasilGanti(false, "dibatalkan", ModelName);
        }
        finally
        {
            _kunciMuat.Release();
        }
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
        // Batalkan pemuatan boot yang masih berjalan lebih dulu, supaya ia tidak
        // menyalakan llama-server BARU sesudah Dispose melewati Stop() di bawah.
        try
        {
            _batalBoot?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // sudah selesai
        }

        Kizuna.Destroy();
        Llama?.Stop();
        await Provider.DisposeAsync().ConfigureAwait(false);

        _batalBoot?.Dispose();
        _kunciMuat.Dispose();
        GC.SuppressFinalize(this);
    }
}
