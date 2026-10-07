using SilverWolf.Core.Text;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Mesin memori tiga tingkat — port <c>TieredMemoryEngine</c> dari
/// <c>apps/server-node/src/tiered-memory.js</c>.
///
/// Fakta penting untuk paritas: <c>MemoryManager</c> di aplikasi lama dibuat
/// dengan <c>enableSummarization: false</c>. Akibatnya
/// <c>createMemoryIfNeeded()</c> langsung kembali dan
/// <c>getMemoryForPrompt()</c> <b>selalu</b> menghasilkan string kosong.
/// Jadi <see cref="MidTermPrompt"/> selalu kosong — ini bukan bug yang perlu
/// diperbaiki, melainkan perilaku yang harus dipertahankan agar prompt sistem
/// identik dengan aplikasi lama.
/// </summary>
public sealed class TieredMemoryEngine
{
    private readonly CharacterVault? _vault;
    private readonly int _maksShortTerm;

    /// <summary>Buffer percakapan aktif (maks <c>maksShortTerm * 2</c> entri).</summary>
    public List<ChatMessage> ShortTermBuffer { get; } = [];

    public int MaksShortTerm => _maksShortTerm;

    public TieredMemoryEngine(CharacterVault? vault, int maksShortTerm = 12)
    {
        _vault = vault;
        _maksShortTerm = maksShortTerm;
    }

    public readonly record struct IsiMemori(
        List<string> Fakta, Mood? Mood, string MidTermPrompt, List<ChatMessage> ShortTerm);

    /// <summary>Port <c>readAll()</c>.</summary>
    public async Task<IsiMemori> ReadAllAsync(CancellationToken ct = default)
    {
        var fakta = new List<string>();
        Mood? mood = null;

        if (_vault is { } vault && vault.Available())
        {
            try
            {
                fakta = await vault.BacaFaktaAsync(ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Aplikasi lama hanya mencetak error lalu lanjut dengan daftar kosong.
            }

            try
            {
                mood = await vault.BacaMoodAsync(ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Sama: mood tetap null bila berkas tidak bisa dibaca.
            }
        }

        return new IsiMemori(fakta, mood, string.Empty, [.. ShortTermBuffer]);
    }

    /// <summary>Port <c>recordTurn(userText, assistantText)</c>.</summary>
    public async Task RecordTurnAsync(string? userText, string? assistantText, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(userText))
        {
            ShortTermBuffer.Add(new ChatMessage("user", userText));
        }

        if (!string.IsNullOrEmpty(assistantText))
        {
            ShortTermBuffer.Add(new ChatMessage("assistant", assistantText));
        }

        while (ShortTermBuffer.Count > _maksShortTerm * 2)
        {
            ShortTermBuffer.RemoveAt(0);
        }

        // Port createMemoryIfNeeded(): no-op karena enableSummarization = false.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Ambil tag emosi dari balasan — dipakai <c>Agent.persist()</c> melalui
    /// <c>bacaTagAwal()</c>.
    /// </summary>
    public static string? BacaTagAwal(string? teks) => EmotionParser.ExtractEmotion(teks).Emotion?.ToLowerInvariant();
}
