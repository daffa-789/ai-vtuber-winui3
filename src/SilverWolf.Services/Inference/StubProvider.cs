using System.Runtime.CompilerServices;
using SilverWolf.Core.Domain;

namespace SilverWolf.Services.Inference;

/// <summary>
/// Provider palsu untuk <c>VTUBER_STUB=true</c> — port <c>StubProvider</c> dari
/// <c>apps/server-node/src/inference.js</c>.
///
/// Potongannya dipertahankan persis supaya jalur kode streaming (penggabungan
/// teks, deteksi tag, pengetikan bertahap) tetap teruji tanpa model 5 GB.
/// </summary>
public sealed class StubProvider : ILlmProvider
{
    private static readonly string[] Potongan =
    [
        "[senyum] ", "Sistem inti sudah hidup, ", "Master.",
    ];

    public string Id => "llama-server";

    public string Model => "stub";

    public Task<ProviderAvailability> AvailableAsync(CancellationToken ct = default) =>
        Task.FromResult(new ProviderAvailability { Ok = true, Reason = "stub" });

    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? opts = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var potongan in Potongan)
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            yield return StreamChunk.Dari(potongan);
            await Task.Yield();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
