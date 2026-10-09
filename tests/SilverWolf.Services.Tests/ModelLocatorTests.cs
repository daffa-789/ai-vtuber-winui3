using SilverWolf.Core.Configuration;
using SilverWolf.Services.Llama;
using Xunit;

namespace SilverWolf.Services.Tests;

/// <summary>
/// Kunci untuk pemilih model: jalur model yang tersimpan harus tetap berlaku
/// setelah folder proyek dipindah, dan pilihan aktif tidak boleh hilang dari
/// daftar sekalipun berkasnya sudah tidak ada.
/// </summary>
public sealed class ModelLocatorTests : IDisposable
{
    private readonly string _akar;

    public ModelLocatorTests()
    {
        _akar = Path.Combine(Path.GetTempPath(), $"uji-model-locator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_akar, "model"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_akar))
            {
                Directory.Delete(_akar, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static AppConfig Konfig(string akar) => new() { Akar = akar };

    private string BuatModel(string nama)
    {
        var jalur = Path.Combine(_akar, "model", nama);
        File.WriteAllBytes(jalur, new byte[] { 0x47, 0x47, 0x55, 0x46 });
        return jalur;
    }

    [Fact]
    public void RelatifMenghasilkanJalurYangPortabel()
    {
        var absolut = BuatModel("satunya.gguf");
        var relatif = ModelLocator.Relatif(Konfig(_akar), absolut);

        // Harus relatif dan memakai pemisah '/', bukan jalur absolut mesin ini.
        Assert.Equal("model/satunya.gguf", relatif);
    }

    [Fact]
    public void AbsolutMengembalikanRelatifKeJalurPenuh()
    {
        var hasil = ModelLocator.Absolut(Konfig(_akar), "model/satunya.gguf");

        Assert.True(Path.IsPathRooted(hasil));
        Assert.Equal(Path.Combine(_akar, "model", "satunya.gguf"), hasil);
    }

    [Fact]
    public void AbsolutTidakMenggandakanJalurYangSudahAbsolut()
    {
        var sudah = Path.Combine(_akar, "model", "x.gguf");
        Assert.Equal(sudah, ModelLocator.Absolut(Konfig(_akar), sudah));
    }

    [Fact]
    public void RelatifDanAbsolutBolakBalik()
    {
        var absolut = BuatModel("bolak-balik.gguf");
        var relatif = ModelLocator.Relatif(Konfig(_akar), absolut);

        Assert.Equal(absolut, ModelLocator.Absolut(Konfig(_akar), relatif));
    }

    [Fact]
    public void RelatifMengembalikanApaAdanyaBilaDiLuarAkar()
    {
        var jalur = Path.Combine(Path.GetTempPath(), "di-luar-akar.gguf");

        // Tidak boleh menghasilkan jalur relatif aneh berisi "..".
        Assert.Equal(jalur, ModelLocator.Relatif(Konfig(_akar), jalur));
    }

    [Fact]
    public void PilihanMenyertakanModelAktifWalauBerkasnyaHilang()
    {
        BuatModel("ada.gguf");

        var k = Konfig(_akar);
        // Model aktif belum ada di disk — dipilih lewat .env dari sesi sebelumnya.
        k.LocalModelPath = "model/sudah-dihapus.gguf";

        var pilihan = ModelLocator.Pilihan(k);

        Assert.Contains(Path.Combine(_akar, "model", "ada.gguf"), pilihan);
        Assert.Contains(Path.Combine(_akar, "model", "sudah-dihapus.gguf"), pilihan);
    }

    [Fact]
    public void PilihanTerurutDanTanpaDuplikat()
    {
        BuatModel("b.gguf");
        BuatModel("a.gguf");
        BuatModel("c.gguf");

        var k = Konfig(_akar);
        k.LocalModelPath = "model/a.gguf";   // sudah ikut terdaftar dari disk

        var pilihan = ModelLocator.Pilihan(k);

        // Tiga berkas, tidak boleh ada yang muncul dua kali karena juga sedang aktif.
        Assert.Equal(3, pilihan.Count);
        Assert.Equal(
            new[] { "a.gguf", "b.gguf", "c.gguf" },
            pilihan.Select(Path.GetFileName));
    }
}
