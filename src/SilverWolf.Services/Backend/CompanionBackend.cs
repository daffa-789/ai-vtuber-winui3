using SilverWolf.Core.Configuration;
using SilverWolf.Core.Domain;
using SilverWolf.Core.Domain.Kizuna;
using SilverWolf.Services.Agent;
using SilverWolf.Services.Inference;

namespace SilverWolf.Services.Backend;

/// <summary>Satu berkas status — padanan objek JSON <c>/api/health</c>.</summary>
public sealed class HealthSnapshot
{
    public bool Ok { get; set; }

    public bool Loading { get; set; }

    public string Model { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    /// <summary>"siap" | "memuat" | "tidak-jalan".</summary>
    public string StatusText { get; set; } = string.Empty;

    public List<string> Cadangan { get; set; } = [];

    public bool Key { get; set; } = true;

    public string Tts { get; set; } = "siap";

    public string? Memori { get; set; }

    public BondSnapshot? Kizuna { get; set; }

    /// <summary>
    /// Aplikasi lama mengirim "node". Sekarang backendnya C#, jadi nilainya
    /// "csharp" — satu-satunya perubahan yang disengaja pada payload health
    /// karena nilainya tidak pernah ditampilkan ke pengguna.
    /// </summary>
    public string Sisi { get; set; } = "csharp";

    public SttStatus Stt { get; set; } = new();
}

public sealed class SttStatus
{
    public bool Hidup { get; set; }

    public string Model { get; set; } = string.Empty;

    public bool Siap { get; set; } = true;
}

/// <summary>
/// Hasil pemanggilan yang setara dengan satu respons HTTP aplikasi lama:
/// kode status, isi <c>{ error }</c> bila gagal, snapshot ikatan (pengganti
/// header <c>x-kizuna</c>), dan aliran teks.
/// </summary>
public sealed class BackendStream
{
    public int Status { get; init; } = 200;

    public string? Error { get; init; }

    public BondSnapshot? Kizuna { get; init; }

    public IAsyncEnumerable<StreamChunk>? Body { get; init; }

    public static BackendStream Galat(int status, string pesan) => new() { Status = status, Error = pesan };
}

/// <summary>
/// Pengganti <c>buatServer()</c> dari <c>apps/server-node/src/server.js</c>.
///
/// Tidak ada lagi HTTP: aplikasi WinUI memanggil kelas ini langsung. Namun
/// kontraknya sengaja dibuat setara — kode status, pesan galat, dan kapan
/// snapshot ikatan dikirim — supaya perilaku yang dirasakan pengguna identik.
///
/// Catatan paritas: hanya <c>/api/kizuna/touch</c> dan
/// <c>/api/autonomous/proactive</c> yang mengirim header <c>x-kizuna</c>;
/// <c>/api/chat</c> tidak. Karena itu <see cref="ChatAsync"/> mengembalikan
/// <c>Kizuna = null</c> dan UI harus menyegarkan snapshot setelah aliran selesai,
/// persis seperti <c>store.send()</c> yang memanggil <c>fetchKizuna()</c>.
/// </summary>
public sealed class CompanionBackend
{
    public AppConfig Konfig { get; }

    public AgentService Agent { get; }

    public ILlmProvider Provider { get; }

    public CharacterVault? Vault { get; }

    public KizunaEngine? Kizuna { get; }

    /// <summary>
    /// Nama model yang dilaporkan health. Bisa berubah saat pemilih model
    /// memuat GGUF lain ke llama-server yang sama.
    /// </summary>
    public string ModelName { get; internal set; }

    public CompanionBackend(
        AppConfig konfig,
        AgentService agent,
        ILlmProvider provider,
        CharacterVault? vault,
        KizunaEngine? kizuna,
        string modelName)
    {
        Konfig = konfig;
        Agent = agent;
        Provider = provider;
        Vault = vault;
        Kizuna = kizuna;
        ModelName = modelName;
    }

    /// <summary>Port <c>health(o, res)</c>.</summary>
    public async Task<HealthSnapshot> GetHealthAsync(CancellationToken ct = default)
    {
        var av = await Provider.AvailableAsync(ct).ConfigureAwait(false);

        var statusText = "siap";
        if (!av.Ok)
        {
            statusText = av.Loading ? "memuat" : "tidak-jalan";
        }

        var model = av.Ok ? ModelName : $"{ModelName}/{statusText} ({av.Reason})";
        var memori = Vault?.Available() == true
            ? "memori lokal (silver_wolf_memory/)"
            : Vault?.UnavailableReason();

        return new HealthSnapshot
        {
            Ok = av.Ok,
            Loading = av.Loading,
            Model = model,
            Provider = Konfig.LlmProvider,
            StatusText = statusText,
            Cadangan = [],
            Key = true,
            Tts = "siap",
            Memori = memori,
            Kizuna = Kizuna?.GetSnapshot(),
            Sisi = "csharp",
            Stt = new SttStatus { Hidup = Konfig.SttHidup, Model = Konfig.SttModel, Siap = true },
        };
    }

    /// <summary>Port <c>chat(o, req, res)</c>.</summary>
    public Task<BackendStream> ChatAsync(IReadOnlyList<ChatMessage>? mentah, CancellationToken ct = default)
    {
        if (TerlaluBesar(mentah))
        {
            return Task.FromResult(BackendStream.Galat(413, "body terlalu besar"));
        }

        var pesan = ChatHistory.Rapikan(mentah, Konfig.MaksPesan, Konfig.MaksKarakter);
        if (pesan.Count == 0)
        {
            return Task.FromResult(BackendStream.Galat(400, "riwayat kosong"));
        }

        return Task.FromResult(new BackendStream
        {
            Status = 200,
            Kizuna = null, // /api/chat tidak pernah mengirim header x-kizuna
            Body = Agent.ChatAsync(pesan, new LlmOptions { MaxTokens = 512, Temperature = 0.7 }, ct),
        });
    }

    /// <summary>Port <c>touch(o, req, res)</c>.</summary>
    public Task<BackendStream> TouchAsync(CancellationToken ct = default) =>
        Task.FromResult(new BackendStream
        {
            Status = 200,
            Kizuna = Kizuna?.GetSnapshot(), // diambil SEBELUM recordTouch berjalan
            Body = Agent.ChatTouchAsync(ct: ct),
        });

    /// <summary>Port <c>proactive(o, req, res)</c>.</summary>
    public Task<BackendStream> ProactiveAsync(double? idleDetik = null, CancellationToken ct = default)
    {
        var idle = 60d;
        if (idleDetik is { } nilai && double.IsFinite(nilai))
        {
            idle = nilai;
        }

        return Task.FromResult(new BackendStream
        {
            Status = 200,
            Kizuna = Kizuna?.GetSnapshot(),
            Body = Agent.ChatProactiveAsync(idle, ct: ct),
        });
    }

    /// <summary>Port <c>/api/kizuna</c> (GET).</summary>
    public BondSnapshot? GetKizuna() => Kizuna?.GetSnapshot();

    private bool TerlaluBesar(IReadOnlyList<ChatMessage>? mentah)
    {
        if (mentah is null)
        {
            return false;
        }

        var total = 0;
        foreach (var m in mentah)
        {
            total += (m.Content?.Length ?? 0) + (m.Role?.Length ?? 0);
            if (total > Konfig.MaksBody)
            {
                return true;
            }
        }

        return false;
    }
}
