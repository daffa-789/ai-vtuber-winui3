using System.Runtime.CompilerServices;
using SilverWolf.Core.Domain;
using SilverWolf.Services.Agent;
using SilverWolf.Services.Inference;
using Xunit;

namespace SilverWolf.Services.Tests;

/// <summary>
/// Uji mesin cemburu sampai ke prompt sistem yang benar-benar dikirim ke LLM.
///
/// <para>
/// Uji satuan <c>Cemburu.Sebutan</c> hanya membuktikan deteksinya benar. Yang
/// belum terbukti — dan justru paling mudah salah — adalah apakah teks cemburu
/// itu benar-benar SAMPAI ke model. Di sini providernya ditangkap supaya isi
/// pesan sistem bisa diperiksa apa adanya.
/// </para>
/// </summary>
public sealed class CemburuAgentTests
{
    /// <summary>
    /// Provider palsu yang merekam pesan sistem lalu mengembalikan satu potongan
    /// jawaban. Cukup untuk memeriksa prompt tanpa memanggil model sungguhan.
    /// </summary>
    private sealed class ProviderPerekam : ILlmProvider
    {
        public List<ChatMessage> Terakhir { get; private set; } = [];

        public string Id => "llama-server";

        public string Model => "uji";

        public Task<ProviderAvailability> AvailableAsync(CancellationToken ct = default) =>
            Task.FromResult(new ProviderAvailability { Ok = true, Reason = "siap" });

        public async IAsyncEnumerable<StreamChunk> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            LlmOptions? opts = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            Terakhir = [.. messages];
            await Task.Yield();
            yield return StreamChunk.Dari("[senyum] Hmph, iya sayang.");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static string PesanSistem(ProviderPerekam provider) =>
        provider.Terakhir.First(m => m.Role == "system").Content ?? string.Empty;

    private static async Task<ProviderPerekam> JalankanChatAsync(params ChatMessage[] riwayat)
    {
        var provider = new ProviderPerekam();
        var agen = new AgentService(provider, "# Uji\n\nKamu Silver Wolf.", localPrompt: true);

        await foreach (var _ in agen.ChatAsync(riwayat))
        {
            // Habiskan aliran supaya prompt direkam.
        }

        return provider;
    }

    // ── Cemburu benar-benar sampai ke prompt ────────────────────────────────

    [Fact]
    public async Task Chat_MenyebutKafkaMenyisipkanKonteksCemburu()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka, dia keren banget."));

        var sistem = PesanSistem(provider);

        Assert.Contains("KONDISI", sistem, StringComparison.Ordinal);
        Assert.Contains("Kafka", sistem, StringComparison.Ordinal);
        Assert.Contains("PACAR", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_TanpaNamaKarakterTidakAdaKonteksCemburu()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku capek kerja hari ini."));

        Assert.DoesNotContain("KONDISI", PesanSistem(provider), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_KarakterYangDihormatiTidakMemicuCemburu()
    {
        // Firefly, Screwllum, dan Herta punya hubungan khusus dengan Silver Wolf.
        foreach (var ucapan in new[]
                 {
                     "Kasihan ya Firefly, hidupnya berat.",
                     "Screwllum itu lawan yang jago.",
                     "Herta pernah balikin seranganku.",
                 })
        {
            var provider = await JalankanChatAsync(new ChatMessage("user", ucapan));
            Assert.DoesNotContain("KONDISI", PesanSistem(provider), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Chat_KataBiasaMarchTidakMemicuCemburu()
    {
        // Salah kenal yang paling mudah terjadi — harus benar-benar tidak kena.
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Kita berbaris march sepanjang jalan."));

        Assert.DoesNotContain("KONDISI", PesanSistem(provider), StringComparison.Ordinal);
    }

    // ── Pelunakan setelah berturut-turut ────────────────────────────────────

    [Fact]
    public async Task Chat_CemburuBerturutTurutMelunak()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka."),
            new ChatMessage("assistant", "[goda] Hmph, aku dengar kok."),
            new ChatMessage("user", "Himeko juga keren."),
            new ChatMessage("assistant", "[sebal] Ya udah lah."),
            new ChatMessage("user", "March 7th juga lucu."));

        var sistem = PesanSistem(provider);

        // Dua giliran sebelumnya sudah menyebut karakter cewek, jadi ini yang
        // ketiga — nadanya wajib turun jadi ledekan ringan.
        Assert.Contains("LEDEKAN RINGAN", sistem, StringComparison.Ordinal);
        Assert.Contains("Jangan diulang", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_GiliranPertamaTidakMelunak()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka."));

        var sistem = PesanSistem(provider);

        Assert.DoesNotContain("LEDEKAN RINGAN", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_HitunganBerturutBerhentiPadaGiliranTanpaNamaKarakter()
    {
        // Di antaranya ada obrolan biasa — jadi ini dianggap awal lagi, bukan
        // kelanjutan, dan nadanya boleh kembali penuh.
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka."),
            new ChatMessage("assistant", "[goda] Hmph."),
            new ChatMessage("user", "Kamu sudah makan belum?"),
            new ChatMessage("assistant", "[senyum] Sudah dong sayang."),
            new ChatMessage("user", "Himeko juga keren ya."));

        var sistem = PesanSistem(provider);

        Assert.Contains("Himeko", sistem, StringComparison.Ordinal);
        Assert.DoesNotContain("LEDEKAN RINGAN", sistem, StringComparison.Ordinal);
    }

    // ── Tidak mengganggu bagian prompt lain ─────────────────────────────────

    [Fact]
    public async Task Chat_AturanSuaraTetapAdaWalauSedangCemburu()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka."));

        var sistem = PesanSistem(provider);

        // Aturan anti-Markdown tidak boleh tergeser oleh konteks cemburu.
        Assert.Contains("ATURAN SUARA", sistem, StringComparison.Ordinal);
        Assert.Contains("tag emosi", sistem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_WaktuDanTanggalTetapDisuntik()
    {
        var provider = await JalankanChatAsync(
            new ChatMessage("user", "Aku suka Kafka."));

        // Konteks cemburu tidak boleh menghapus alat waktu.
        Assert.Contains("Jam & tanggal mesin", PesanSistem(provider), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_MenghitungLogCemburu()
    {
        var provider = new ProviderPerekam();
        var catatan = new List<string>();
        var agen = new AgentService(
            provider,
            "# Uji\n\nKamu Silver Wolf.",
            localPrompt: true,
            onLog: catatan.Add);

        await foreach (var _ in agen.ChatAsync([new ChatMessage("user", "Aku suka Kafka.")]))
        {
        }

        Assert.Contains(catatan, baris => baris.Contains("cemburu: Kafka", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Chat_LogTidakDitulisSaatTidakAdaCemburu()
    {
        var provider = new ProviderPerekam();
        var catatan = new List<string>();
        var agen = new AgentService(
            provider,
            "# Uji\n\nKamu Silver Wolf.",
            localPrompt: true,
            onLog: catatan.Add);

        await foreach (var _ in agen.ChatAsync([new ChatMessage("user", "Halo sayang.")]))
        {
        }

        Assert.DoesNotContain(catatan, baris => baris.Contains("cemburu", StringComparison.Ordinal));
    }
}
