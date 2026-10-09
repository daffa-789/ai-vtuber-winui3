using System.Text;
using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

/// <summary>
/// Penggabungan WAV adalah inti perbaikan "ngomong setengah-setengah": seluruh
/// kalimat disintesis dulu, digabung, baru diputar. Kalau penggabungan gagal
/// diam-diam dan salah, suaranya rusak — jadi batasnya diuji di sini.
/// </summary>
public sealed class GabungWavTests
{
    private static string Buat(int lajuSampel, int sampel)
    {
        var jalur = Path.Combine(Path.GetTempPath(), $"uji-gabung-{Guid.NewGuid():N}.wav");
        var data = new byte[sampel * 2];
        using var aliran = File.Create(jalur);
        using var tulis = new BinaryWriter(aliran, Encoding.ASCII);
        tulis.Write(Encoding.ASCII.GetBytes("RIFF"));
        tulis.Write(36 + data.Length);
        tulis.Write(Encoding.ASCII.GetBytes("WAVE"));
        tulis.Write(Encoding.ASCII.GetBytes("fmt "));
        tulis.Write(16);
        tulis.Write((short)1);
        tulis.Write((short)1);
        tulis.Write(lajuSampel);
        tulis.Write(lajuSampel * 2);
        tulis.Write((short)2);
        tulis.Write((short)16);
        tulis.Write(Encoding.ASCII.GetBytes("data"));
        tulis.Write(data.Length);
        tulis.Write(data);
        return jalur;
    }

    private static int LajuSampelDari(string jalur) =>
        BitConverter.ToInt32(File.ReadAllBytes(jalur), 24);

    private static int UkuranDataDari(string jalur)
    {
        // "data" selalu di offset 36 untuk WAV PCM 16-bit kanonik yang kita tulis.
        var isi = File.ReadAllBytes(jalur);
        Assert.Equal("data", Encoding.ASCII.GetString(isi, 36, 4));
        return BitConverter.ToInt32(isi, 40);
    }

    [Fact]
    public void MenggabungDuaBerkasMenjumlahkanIsiDanMempertahankanFormat()
    {
        var a = Buat(40_000, 100);
        var b = Buat(40_000, 250);
        try
        {
            var hasil = GabungWav.Gabungkan(new[] { a, b }, Path.GetTempPath());

            Assert.NotNull(hasil);
            Assert.NotEqual(a, hasil);
            Assert.Equal(40_000, LajuSampelDari(hasil!));
            Assert.Equal((100 + 250) * 2, UkuranDataDari(hasil!));
            Assert.True(File.Exists(hasil!));
            File.Delete(hasil!);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }

    [Fact]
    public void SatuBerkasDikembalikanApaAdanyaTanpaDisalin()
    {
        var a = Buat(40_000, 100);
        try
        {
            // Menyalin berarti membuang cache yang sudah dibayar mahal.
            Assert.Equal(a, GabungWav.Gabungkan(new[] { a }, Path.GetTempPath()));
        }
        finally
        {
            File.Delete(a);
        }
    }

    [Fact]
    public void FormatBerbedaDitolakSupayaTidakMenghasilkanSuaraRusak()
    {
        var a = Buat(40_000, 100);
        var b = Buat(22_050, 100);
        try
        {
            // null = pemanggil memutar berurutan. Lebih baik ada jeda daripada
            // ada suara yang rusak.
            Assert.Null(GabungWav.Gabungkan(new[] { a, b }, Path.GetTempPath()));
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }

    [Fact]
    public void BerkasHilangDitolakTanpaMelempar()
    {
        Assert.Null(GabungWav.Gabungkan(
            new[] { Path.Combine(Path.GetTempPath(), "tidak-ada.wav") },
            Path.GetTempPath()));
    }

    [Fact]
    public void DaftarKosongMenghasilkanNull()
    {
        Assert.Null(GabungWav.Gabungkan(Array.Empty<string>(), Path.GetTempPath()));
    }
}
