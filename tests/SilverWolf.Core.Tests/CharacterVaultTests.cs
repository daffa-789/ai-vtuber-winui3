using SilverWolf.Core.Domain;

namespace SilverWolf.Core.Tests;

public class CharacterVaultTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sw-vault-" + Guid.NewGuid().ToString("N"));
    private readonly CharacterVault _vault;

    public CharacterVaultTests()
    {
        Directory.CreateDirectory(_root);
        _vault = new CharacterVault(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // berkas masih dipakai; tidak menggagalkan uji
        }
    }

    [Fact]
    public async Task Fakta_DitulisDanDibacaKembali()
    {
        await _vault.SimpanFaktaAsync(["Suka kopi hitam", "Main Honkai tiap malam"]);

        var fakta = await _vault.BacaFaktaAsync();

        Assert.Equal(["Suka kopi hitam", "Main Honkai tiap malam"], fakta);
    }

    [Fact]
    public async Task Fakta_MengabaikanBarisPanduanDanPenanda()
    {
        await _vault.SimpanFaktaAsync([]);

        var fakta = await _vault.BacaFaktaAsync();

        Assert.Empty(fakta);
        Assert.Contains("_Belum ada fakta tersimpan._", await _vault.BacaAsync("Fakta.md")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mood_SimpanDanBacaUtuh()
    {
        await _vault.SimpanMoodAsync(new Mood
        {
            Valensi = 0.5,
            Energi = 0.7,
            Afinitas = 0.8,
            Pertukaran = 3,
            Alasan = "tag terakhir: senyum",
        });

        var mood = await _vault.BacaMoodAsync();

        Assert.NotNull(mood);
        Assert.Equal(0.5, mood.Valensi, 6);
        Assert.Equal(0.7, mood.Energi, 6);
        Assert.Equal(0.8, mood.Afinitas, 6);
        Assert.Equal(3, mood.Pertukaran);
    }

    [Fact]
    public async Task Mood_BerkasTanpaAngkaKembaliNull()
    {
        await _vault.TulisAsync("Mood.md", "bukan mood");

        Assert.Null(await _vault.BacaMoodAsync());
    }

    [Fact]
    public async Task Mood_BerkasTidakAdaKembaliNull()
    {
        Assert.Null(await _vault.BacaMoodAsync());
    }

    [Fact]
    public async Task CatatHari_MenambahBarisTanpaMenghapusYangLama()
    {
        await _vault.CatatHariAsync("Master: halo | Silver Wolf: hai");
        await _vault.CatatHariAsync("Master: lagi | Silver Wolf: iya");

        var nama = $"Riwayat/{CharacterVault.Tanggal()}.md";
        var isi = await _vault.BacaAsync(nama);

        Assert.NotNull(isi);
        Assert.Contains("- Master: halo | Silver Wolf: hai", isi, StringComparison.Ordinal);
        Assert.Contains("- Master: lagi | Silver Wolf: iya", isi, StringComparison.Ordinal);
    }

    [Fact]
    public void Kerangka_BerisiFrontMatterDanTautan()
    {
        var teks = CharacterVault.Kerangka("uji", "Judul Uji", "isi", ["Mood"]);

        Assert.StartsWith("---\n", teks, StringComparison.Ordinal);
        Assert.Contains("type: memory", teks, StringComparison.Ordinal);
        Assert.Contains("name: \"uji\"", teks, StringComparison.Ordinal);
        Assert.Contains("description: \"Judul Uji\"", teks, StringComparison.Ordinal);
        Assert.Contains("- \"[[silverwolf-persona]]\"", teks, StringComparison.Ordinal);
        Assert.Contains("- \"[[Mood]]\"", teks, StringComparison.Ordinal);
        Assert.Contains("# Judul Uji", teks, StringComparison.Ordinal);
    }

    [Fact]
    public void Tersedia_DanAlasanTidakTersedia()
    {
        Assert.True(_vault.Available());
        Assert.Equal("memori lokal (silver_wolf_memory/)", "memori lokal (silver_wolf_memory/)");

        var hilang = new CharacterVault(Path.Combine(_root, "tidak-ada"));
        Assert.False(hilang.Available());
        Assert.Contains("tidak ada", hilang.UnavailableReason(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tulis_MemakaiBerkasSementaraBukanMenimpaLangsung()
    {
        await _vault.TulisAsync("Uji.md", "isi");

        Assert.Equal("isi", await _vault.BacaAsync("Uji.md"));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
}
