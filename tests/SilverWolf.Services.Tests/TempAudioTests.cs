using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

public sealed class TempAudioTests : IDisposable
{
    private readonly string _temp;

    public TempAudioTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), $"uji-temp-audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string Buat(string nama)
    {
        var jalur = Path.Combine(_temp, nama);
        Directory.CreateDirectory(Path.GetDirectoryName(jalur)!);
        File.WriteAllBytes(jalur, new byte[] { 1, 2, 3 });
        return jalur;
    }

    [Fact]
    public void MembersihkanHanyaPolaYangDimaksud()
    {
        // Siapkan campuran: yang menjadi sasaran dan yang harus dibiarkan.
        var cache = Path.Combine(_temp, "silverwolf-tts");
        var cacheWav = Buat(Path.Combine("silverwolf-tts", "abc.wav"));
        var tts = Buat("sw-tts-deadbeef.wav");
        var gabung = Buat("sw-gabung-cafe.wav");
        var pekerja = Buat(Path.Combine("sw-pekerja-xyz", "scratch.txt"));

        var lainTxt = Buat("lain.txt");
        var lainWav = Buat("other.wav");
        var mirip = Buat("sw-lainnya.wav");   // prefiks beda, harus dibiarkan

        TempAudio.Bersihkan(_temp, log: null);

        Assert.False(Directory.Exists(cache));
        Assert.False(File.Exists(cacheWav));
        Assert.False(File.Exists(tts));
        Assert.False(File.Exists(gabung));
        Assert.False(Directory.Exists(Path.Combine(_temp, "sw-pekerja-xyz")));

        Assert.True(File.Exists(lainTxt));
        Assert.True(File.Exists(lainWav));
        Assert.True(File.Exists(mirip));
        Assert.True(File.Exists(pekerja) == false); // sudah ikut terhapus dengan foldernya
    }

    [Fact]
    public void IdempotenDanTidakMelemparSaatFolderHilang()
    {
        // Panggilan kedua pada akar yang kosong harus aman.
        TempAudio.Bersihkan(_temp, log: null);
        TempAudio.Bersihkan(_temp, log: null);
        Assert.True(Directory.Exists(_temp));
    }

    [Fact]
    public void TidakMelemparSaatAkarKosong()
    {
        TempAudio.Bersihkan("", log: null);
    }

    /// <summary>
    /// Uji jalur yang sebenarnya dipakai saat penutupan: cache berbasis berkas
    /// yang diisi sesi lama harus benar-benar habis. Ini yang menjawab
    /// "jangan sampai penuh memorinya" — tanpa langkah ini, %TEMP%\silverwolf-tts
    /// hanya bertambah selama berhari-hari.
    /// </summary>
    [Fact]
    public async Task MengecekJalurSepertiSaatPenutupan()
    {
        // Pipeline dengan cache memori: berkas sementara harus hilang sesudah
        // klip dibaca, dan tidak ada sw-gabung-*.wav yang tertinggal.
        var tts = Buat("sw-tts-sisa.wav");
        var gabung = Buat("sw-gabung-sisa.wav");
        var cacheWav = Buat(Path.Combine("silverwolf-tts", "lama.wav"));

        TempAudio.Bersihkan(_temp, log: null);

        Assert.False(File.Exists(tts));
        Assert.False(File.Exists(gabung));
        Assert.False(Directory.Exists(Path.Combine(_temp, "silverwolf-tts")));

        await Task.CompletedTask;
    }
}
