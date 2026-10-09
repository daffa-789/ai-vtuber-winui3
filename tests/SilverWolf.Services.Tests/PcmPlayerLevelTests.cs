using System.Text;
using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

/// <summary>
/// LipSync hanya sebaik sumber amplitudonya. Versi lama memakai gelombang sinus
/// dari waktu berjalan — mulutnya bergerak, tetapi tidak ada hubungannya dengan
/// suara yang keluar. Uji di sini mengunci bahwa nilainya benar-benar mengikuti
/// ISI audio.
/// </summary>
public sealed class PcmPlayerLevelTests
{
    private const int LajuSampel = 40_000;

    /// <summary>
    /// WAV PCM mono 16 bit berisi dua bagian: bagian pertama sekeras
    /// <paramref name="keras"/>, bagian kedua sepelan <paramref name="pelan"/>.
    /// Dipakai untuk membuktikan bahwa amplitudo mengikuti isi, bukan waktu.
    /// </summary>
    private static string BuatWav(float keras, float pelan, int sampelPerBagian)
    {
        var jalur = Path.Combine(Path.GetTempPath(), $"uji-level-{Guid.NewGuid():N}.wav");
        var total = sampelPerBagian * 2;
        var data = new byte[total * 2];

        for (var i = 0; i < total; i++)
        {
            var amplitudo = i < sampelPerBagian ? keras : pelan;
            var nilai = (short)(Math.Sin(i * 2.0 * Math.PI * 440.0 / LajuSampel)
                * amplitudo * short.MaxValue);
            data[i * 2] = (byte)(nilai & 0xFF);
            data[(i * 2) + 1] = (byte)((nilai >> 8) & 0xFF);
        }

        using var aliran = File.Create(jalur);
        using var tulis = new BinaryWriter(aliran, Encoding.ASCII);
        tulis.Write(Encoding.ASCII.GetBytes("RIFF"));
        tulis.Write(36 + data.Length);
        tulis.Write(Encoding.ASCII.GetBytes("WAVE"));
        tulis.Write(Encoding.ASCII.GetBytes("fmt "));
        tulis.Write(16);
        tulis.Write((short)1);
        tulis.Write((short)1);
        tulis.Write(LajuSampel);
        tulis.Write(LajuSampel * 2);
        tulis.Write((short)2);
        tulis.Write((short)16);
        tulis.Write(Encoding.ASCII.GetBytes("data"));
        tulis.Write(data.Length);
        tulis.Write(data);
        return jalur;
    }

    [Fact]
    public void AmplitudoMengikutiIsiAudioBukanWaktuBerjalan()
    {
        // 1 detik keras (0,5) lalu 1 detik pelan (0,05) - perbandingan 10x.
        var jalur = BuatWav(0.5f, 0.05f, LajuSampel);
        try
        {
            var bingkai = PcmPlayer.AnalisisAmplitudo(jalur);

            Assert.NotEmpty(bingkai);
            // 2 detik pada 60 bingkai/detik.
            Assert.InRange(bingkai.Length, 110, 130);

            // Dinormalkan ke puncak = 1, jadi bagian keras mendekati 1...
            Assert.True(bingkai[10] > 0.9f, $"awal harus hampir penuh, dapat {bingkai[10]:F3}");

            // ...dan bagian pelan jelas lebih kecil. Akar kuadrat dari 0,1
            // sekitar 0,32; yang penting jauh di bawah bagian keras.
            Assert.True(bingkai[^10] < 0.5f, $"akhir harus jauh lebih kecil, dapat {bingkai[^10]:F3}");
            Assert.True(bingkai[10] > bingkai[^10] * 2f,
                "bagian keras harus minimal dua kali bagian pelan");
        }
        finally
        {
            File.Delete(jalur);
        }
    }

    [Fact]
    public void AudioHeningTidakMenghasilkanNaNAtauPembagianNol()
    {
        var jalur = BuatWav(0f, 0f, 4_000);
        try
        {
            var bingkai = PcmPlayer.AnalisisAmplitudo(jalur);

            Assert.NotEmpty(bingkai);
            Assert.All(bingkai, nilai =>
            {
                Assert.False(float.IsNaN(nilai), "RMS hening tidak boleh NaN");
                Assert.Equal(0f, nilai);
            });
        }
        finally
        {
            File.Delete(jalur);
        }
    }

    [Fact]
    public void BerkasTidakTerbacaMenghasilkanDaftarKosongBukanMelempar()
    {
        // Suara harus tetap diputar walau analisisnya gagal; mulut cukup diam.
        var kosong = PcmPlayer.AnalisisAmplitudo(
            Path.Combine(Path.GetTempPath(), "tidak-ada-level.wav"));

        Assert.Empty(kosong);
    }
}
