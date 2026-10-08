using SilverWolf.Core.Inference;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Cacat §8.6: aplikasi mencari <c>{"status":"loading model"}</c>, padahal
/// llama-server mengirim <c>{"error":{"message":"Loading model",...}}</c>.
/// Akibatnya UI menampilkan OFFLINE padahal model sedang dimuat.
/// </summary>
public class HealthProbeTests
{
    [Fact]
    public void SedangMemuat_BentukLlamaServerDikenali()
    {
        // Badan asli llama-server saat memuat GGUF ke VRAM — bentuk inilah yang
        // dulu lolos dan membuat UI berbohong "OFFLINE".
        const string badan =
            """{"error":{"message":"Loading model","type":"unavailable_error","code":503}}""";

        Assert.True(HealthProbe.MenandakanSedangMemuat(503, badan));
    }

    [Fact]
    public void SedangMemuat_BentukStatusLamaTetapDiterima()
    {
        // Bentuk yang diharapkan kode lama. Tidak pernah terlihat di llama-server
        // asli, tetapi tetap dikenali supaya tidak ada regresi bila muncul lagi.
        Assert.True(HealthProbe.MenandakanSedangMemuat(503, """{"status":"loading model"}"""));
    }

    [Fact]
    public void SedangMemuat_BesarKecilHurufTidakMenentukan()
    {
        Assert.True(HealthProbe.MenandakanSedangMemuat(503, """{"error":{"message":"LOADING MODEL"}}"""));
    }

    [Fact]
    public void SedangMemuat_KodeSelain503TidakDianggapMemuat()
    {
        // 200 = siap; 404/500 = benar-benar gagal. Keduanya BUKAN "tunggu".
        const string badan = """{"error":{"message":"Loading model"}}""";

        Assert.False(HealthProbe.MenandakanSedangMemuat(200, badan));
        Assert.False(HealthProbe.MenandakanSedangMemuat(404, badan));
        Assert.False(HealthProbe.MenandakanSedangMemuat(500, badan));
    }

    [Fact]
    public void SedangMemuat_BadanKosongAtauBukanJsonTidakDianggapMemuat()
    {
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, null));
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, string.Empty));
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, "   "));
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, "<html>503</html>"));
    }

    [Fact]
    public void SedangMemuat_GalatLainTidakDisamarkanJadiMemuat()
    {
        // 503 dengan pesan galat lain bukan "memuat" — supaya kegagalan nyata
        // tetap terlihat sebagai tidak-jalan, bukan tersamar jadi menunggu.
        Assert.False(HealthProbe.MenandakanSedangMemuat(
            503, """{"error":{"message":"no slot available","code":503}}"""));

        // Akar bukan objek (mis. array atau string telanjang) juga bukan "memuat".
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, "[1,2,3]"));
        Assert.False(HealthProbe.MenandakanSedangMemuat(503, "\"loading model\""));
    }
}
