using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SilverWolf.Core.Domain;
using SilverWolf.Core.Domain.Kizuna;
using SilverWolf.Services.Inference;

namespace SilverWolf.Services.Agent;

/// <summary>
/// Agen percakapan — port kelas <c>Agent</c> dari
/// <c>apps/server-node/src/agent.js</c>.
///
/// Urutan operasi dipertahankan persis:
/// baca memori → susun prompt sistem → alirkan balasan → <b>baru</b> simpan
/// ke memori, kizuna, dan vault. Menyimpan sebelum mengalirkan akan mengubah
/// apa yang Silver Wolf "ingat" pada giliran berikutnya.
/// </summary>
public sealed class AgentService
{
    private static readonly Regex SpasiGanda = new(@"\s+", RegexOptions.Compiled);

    private readonly ILlmProvider _provider;
    private readonly string _persona;
    private readonly CharacterVault? _vault;
    private readonly KizunaEngine? _kizuna;
    private readonly TieredMemoryEngine? _memoryEngine;
    private MasterProfile? _profil;

    public AgentService(
        ILlmProvider provider,
        string persona,
        CharacterVault? vault = null,
        KizunaEngine? kizuna = null,
        TieredMemoryEngine? memoryEngine = null,
        MasterProfile? profil = null,
        bool localPrompt = true,
        Action<Exception>? onError = null)
    {
        _provider = provider;
        _persona = persona;
        _vault = vault;
        _kizuna = kizuna;
        _memoryEngine = memoryEngine;
        _profil = profil;
        LocalPrompt = provider.Id == "ollama" ? false : localPrompt;
        OnError = onError;
    }

    /// <summary>
    /// Profil Master (tanggal lahir, sebutan). Diset dari <c>Profil.md</c> saat
    /// runtime menyala; bisa juga diisi langsung oleh uji.
    /// </summary>
    public MasterProfile? Profil
    {
        get => _profil;
        set => _profil = value;
    }

    /// <summary>
    /// Apakah prompt "lokal" dipakai (dengan instruksi tag emosi wajib).
    /// Selalu false untuk Ollama — sama seperti aplikasi lama.
    /// </summary>
    public bool LocalPrompt { get; }

    /// <summary>Pengganti <c>console.error('memori:', error)</c>.</summary>
    public Action<Exception>? OnError { get; set; }

    private void Laporkan(Exception error) => OnError?.Invoke(error);

    /// <summary>
    /// Isi memori + konteks ikatan yang dibutuhkan <c>gabungSystem()</c>.
    /// Padanan objek <c>{ fakta, mood, midTermPrompt, kizunaContext }</c>.
    /// </summary>
    public readonly record struct MemoriAgen(
        List<string> Fakta, Mood? Mood, string MidTermPrompt, string KizunaContext, List<ChatMessage> ShortTerm);

    /// <summary>Port <c>memory()</c>.</summary>
    public async Task<MemoriAgen> MemoryAsync(CancellationToken ct = default)
    {
        var mem = _memoryEngine is not null
            ? await _memoryEngine.ReadAllAsync(ct).ConfigureAwait(false)
            : await BacaLangsungDariVaultAsync(ct).ConfigureAwait(false);

        var kizunaContext = string.Empty;
        if (_kizuna is not null)
        {
            try
            {
                kizunaContext = _kizuna.GetBondContext();
            }
            catch (Exception error)
            {
                Laporkan(error);
            }
        }

        return new MemoriAgen(mem.Fakta, mem.Mood, mem.MidTermPrompt, kizunaContext, mem.ShortTerm);
    }

    private async Task<TieredMemoryEngine.IsiMemori> BacaLangsungDariVaultAsync(CancellationToken ct)
    {
        var fakta = new List<string>();
        Mood? mood = null;

        if (_vault is null || !_vault.Available())
        {
            return new TieredMemoryEngine.IsiMemori(fakta, mood, string.Empty, []);
        }

        try
        {
            fakta = await _vault.BacaFaktaAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Laporkan(error);
        }

        try
        {
            mood = await _vault.BacaMoodAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Laporkan(error);
        }

        return new TieredMemoryEngine.IsiMemori(fakta, mood, string.Empty, []);
    }

    /// <summary>
    /// Baca <c>Profil.md</c> dari vault. Gagal baca tidak menggagalkan giliran —
    /// cukup berarti "belum ada tanggal lahir yang tercatat".
    /// </summary>
    private async Task<MasterProfile?> BacaProfilAsync(CancellationToken ct)
    {
        if (_vault is null || !_vault.Available())
        {
            return null;
        }

        try
        {
            return await _vault.BacaProfilAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Laporkan(error);
            return null;
        }
    }

    /// <summary>Port <c>chat(riwayat, opts, signal)</c>.</summary>
    public async IAsyncEnumerable<StreamChunk> ChatAsync(
        IReadOnlyList<ChatMessage> riwayat,
        LlmOptions? opts = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var mem = await MemoryAsync(ct).ConfigureAwait(false);

        if (_profil is null)
        {
            _profil = await BacaProfilAsync(ct).ConfigureAwait(false);
        }

        var konteksAlat = new DaftarAlat()
            .Tambah(new AlatWaktu(_profil))
            .JalankanOtomatis();

        var pesan = new List<ChatMessage>
        {
            new("system", PersonaComposer.GabungSystem(
                _persona,
                mem.Fakta,
                mem.Mood,
                LocalPrompt,
                mem.KizunaContext,
                mem.MidTermPrompt,
                konteksAlat)),
        };

        pesan.AddRange(riwayat.Where(m => m.Role != "system"));

        var jawaban = string.Empty;
        await foreach (var potongan in _provider.StreamAsync(pesan, opts, ct).ConfigureAwait(false))
        {
            if (potongan.Err is not null)
            {
                yield return potongan;
                yield break;
            }

            jawaban += potongan.Text ?? string.Empty;
            yield return potongan;
        }

        if (jawaban.Length > 0)
        {
            try
            {
                await PersistAsync(riwayat, jawaban, mem, ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Laporkan(error);
            }
        }
    }

    /// <summary>Port <c>chatProactive(idleDetik, opts, signal)</c>.</summary>
    public async IAsyncEnumerable<StreamChunk> ChatProactiveAsync(
        double idleDetik = 60,
        LlmOptions? opts = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _kizuna?.GetSnapshot();
        var promptProaktif = ProactiveDirector.BuatPromptProaktif(snapshot, idleDetik);

        var riwayat = new List<ChatMessage>
        {
            new("user",
                "[Kondisi: kamu sedang menatap layar dan memperhatikan Master/pacarmu yang sedang hening]. "
                + promptProaktif),
        };

        await foreach (var potongan in ChatAsync(
            riwayat,
            new LlmOptions { MaxTokens = 256, Temperature = 0.8 },
            ct).ConfigureAwait(false))
        {
            yield return potongan;
        }
    }

    /// <summary>Port <c>chatTouch(opts, signal)</c>.</summary>
    public async IAsyncEnumerable<StreamChunk> ChatTouchAsync(
        LlmOptions? opts = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _kizuna?.GetSnapshot();
        if (_kizuna is not null)
        {
            try
            {
                var hasil = await _kizuna.RecordTouchAsync(ct: ct).ConfigureAwait(false);
                snapshot = hasil.Snapshot;
            }
            catch (Exception error)
            {
                Laporkan(error);
            }
        }

        var promptSentuhan = ProactiveDirector.BuatPromptSentuhan(snapshot);
        var riwayat = new List<ChatMessage>
        {
            new("user",
                "[Interaksi Fisik: Master baru saja mengelus kepalamu dengan lembut]. "
                + promptSentuhan),
        };

        await foreach (var potongan in ChatAsync(
            riwayat,
            new LlmOptions { MaxTokens = 256, Temperature = 0.85 },
            ct).ConfigureAwait(false))
        {
            yield return potongan;
        }
    }

    /// <summary>Port <c>persist(riwayat, jawaban, mem)</c>.</summary>
    private async Task PersistAsync(
        IReadOnlyList<ChatMessage> riwayat,
        string jawaban,
        MemoriAgen mem,
        CancellationToken ct)
    {
        var ucapan = string.Empty;
        for (var i = riwayat.Count - 1; i >= 0; i--)
        {
            if (riwayat[i].Role == "user")
            {
                ucapan = riwayat[i].Content;
                break;
            }
        }

        if (_memoryEngine is not null)
        {
            try
            {
                await _memoryEngine.RecordTurnAsync(ucapan, jawaban, ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Laporkan(error);
            }
        }

        if (_kizuna is not null)
        {
            try
            {
                await _kizuna.RecordMessageAsync(jawaban, ct: ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Laporkan(error);
            }
        }

        if (_vault is null || !_vault.Available())
        {
            return;
        }

        var mood = Mood.Perbarui(mem.Mood, TieredMemoryEngine.BacaTagAwal(jawaban));
        await _vault.SimpanMoodAsync(mood, ct).ConfigureAwait(false);
        await _vault.CatatHariAsync($"Master: {Ringkas(ucapan)} | Silver Wolf: {Ringkas(jawaban)}", ct)
            .ConfigureAwait(false);
    }

    private static string Ringkas(string s)
    {
        var padat = SpasiGanda.Replace(s, " ").Trim();
        return padat.Length > 240 ? padat[..240] : padat;
    }
}
