using SilverWolf.Core.Domain;

namespace SilverWolf.Services.Inference;

/// <summary>Opsi pemanggilan LLM — padanan <c>{ maxTokens, temperature }</c>.</summary>
public sealed class LlmOptions
{
    public int? MaxTokens { get; set; }

    public double? Temperature { get; set; }
}

/// <summary>
/// Hasil pengecekan kesiapan provider — port nilai balik <c>available()</c>.
/// </summary>
public sealed class ProviderAvailability
{
    public bool Ok { get; set; }

    public bool Loading { get; set; }

    /// <summary>"siap", "memuat model ke VRAM...", "HTTP 503", atau pesan galat.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Satu potongan aliran — port <c>{ text }</c> dan <c>{ err }</c> dari
/// <c>OpenAiCompatibleProvider.stream()</c>.
/// </summary>
public sealed class StreamChunk
{
    public string? Text { get; set; }

    public Exception? Err { get; set; }

    public static StreamChunk Dari(string teks) => new() { Text = teks };

    public static StreamChunk Galat(Exception err) => new() { Err = err };

    public static StreamChunk Galat(string pesan) => new() { Err = new InvalidOperationException(pesan) };
}

/// <summary>Kontrak provider inferensi.</summary>
public interface ILlmProvider
{
    /// <summary>"llama-server" atau "ollama" — menentukan bentuk permintaan.</summary>
    string Id { get; }

    string Model { get; }

    Task<ProviderAvailability> AvailableAsync(CancellationToken ct = default);

    IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? opts = null,
        CancellationToken ct = default);

    ValueTask DisposeAsync();
}
