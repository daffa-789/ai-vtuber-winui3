using SilverWolf.Core.Configuration;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji parser .env. Semua kasus di sini diambil dari perilaku
/// <c>apps/server-node/src/config.js</c> yang tidak lazim dan mudah rusak
/// bila diganti pustaka .env pihak ketiga.
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void Parse_MengabaikanKomentarDanBarisKosong()
    {
        var hasil = EnvFile.Parse("# komentar\n\nPORT=8787\n# lagi\n");

        Assert.Single(hasil);
        Assert.Equal("8787", hasil["PORT"]);
    }

    [Fact]
    public void Parse_KomentarHanyaDihitungBilaDidahuluiSpasi()
    {
        var hasil = EnvFile.Parse("A=satu # ini komentar\nB=dua#ini bukan\n");

        Assert.Equal("satu", hasil["A"]);
        Assert.Equal("dua#ini bukan", hasil["B"]);
    }

    [Fact]
    public void Parse_NilaiBerkutipMenangAtasKomentar()
    {
        var hasil = EnvFile.Parse("A=\"isi # dengan pagar\"\n");

        Assert.Equal("isi # dengan pagar", hasil["A"]);
    }

    [Fact]
    public void Parse_KutipTidakBerpasanganDipotongDiAwalSaja()
    {
        var hasil = EnvFile.Parse("A='nilai\n");

        Assert.Equal("nilai", hasil["A"]);
    }

    [Fact]
    public void StripQuotes_HanyaMembuangPasanganLengkap()
    {
        Assert.Equal("nilai", EnvFile.StripQuotes("\"nilai\""));
        Assert.Equal("nilai", EnvFile.StripQuotes("  'nilai'  "));
        Assert.Equal("\"nilai", EnvFile.StripQuotes("\"nilai"));
    }

    [Fact]
    public void EnvSource_LingkunganProsesMenangAtasBerkas()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["MODE"] = "dari-berkas" },
            Environ = new Dictionary<string, string> { ["MODE"] = "dari-proses" },
        };

        Assert.Equal("dari-proses", env.Value("MODE"));
    }

    [Fact]
    public void EnvSource_NilaiProsesKosongJatuhKeBerkas()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["MODE"] = "dari-berkas" },
            Environ = new Dictionary<string, string> { ["MODE"] = "   " },
        };

        Assert.Equal("dari-berkas", env.Value("MODE"));
    }

    [Fact]
    public void EnvSource_AngkaBulatTidakValidMencatatPeringatan()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["PORT"] = "87.5" },
        };

        Assert.Equal(8787, env.Int("PORT", 8787));
        Assert.Contains(env.Warnings, w => w.StartsWith("PORT=\"87.5\" bukan bilangan bulat"));
    }

    [Fact]
    public void EnvSource_AngkaFloatValid()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["TOP_P"] = "0.95" },
        };

        Assert.Equal(0.95, env.Float("TOP_P", 0), 6);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("ya", true)]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("tidak", false)]
    [InlineData("off", false)]
    public void EnvSource_BoolMengenaliKosakataIndonesia(string nilai, bool expected)
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["AKTIF"] = nilai },
        };

        Assert.Equal(expected, env.Bool("AKTIF", !expected));
    }

    [Fact]
    public void EnvSource_BoolTidakValidMencatatPeringatanDanPakaiBawaan()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["AKTIF"] = "mungkin" },
        };

        Assert.True(env.Bool("AKTIF", true));
        Assert.Contains(env.Warnings, w => w.StartsWith("AKTIF=\"mungkin\" bukan boolean"));
    }

    [Fact]
    public void EnvSource_DaftarMembuangEntriKosong()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["SENARAI"] = " satu , , dua ,, " },
        };

        Assert.Equal(["satu", "dua"], env.List("SENARAI", string.Empty));
    }

    [Fact]
    public void BacaKonfig_MemakaiBawaanAplikasi()
    {
        var env = new EnvSource();
        var konfig = ConfigReader.Baca(env, "C:\\akar");

        Assert.Equal(8787, konfig.Port);
        Assert.Equal("vulkan", konfig.LlmProvider);
        Assert.Equal("pet", konfig.Tampak);
        Assert.Equal("layar-penuh", konfig.PetSembunyi);
        Assert.Equal("off", konfig.LocalReasoning);
        Assert.Equal(8192, konfig.LocalModelCtx);
        Assert.Equal(99, konfig.VulkanNgl);
        Assert.Equal(64, konfig.MaksPesan);
        Assert.Equal(8192, konfig.MaksKarakter);
        Assert.Equal(8788, konfig.PortInferensi);
    }

    [Fact]
    public void BacaKonfig_PekerjaTtsHidupSecaraBawaan()
    {
        // Pekerja menetap harus hidup secara bawaan: tanpa itu setiap kalimat
        // memuat ulang seluruh model RVC (~20 dtk) dan suara tidak pernah
        // keluar karena antrean menembus batas waktu.
        var konfig = ConfigReader.Baca(new EnvSource(), "C:\\akar");

        Assert.True(konfig.TtsPekerja);
    }

    [Theory]
    [InlineData("ya", true)]
    [InlineData("tidak", false)]
    [InlineData("", true)]
    public void BacaKonfig_PekerjaTtsBisaDimatikan(string nilai, bool expected)
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["VTUBER_TTS_PEKERJA"] = nilai },
        };

        Assert.Equal(expected, ConfigReader.Baca(env, "C:\\akar").TtsPekerja);
    }

    [Fact]
    public void BacaKonfig_BatasSiapPekerjaTerpisahDariBatasKalimat()
    {
        // Dua batas ini sengaja dipisah. Mencampurnya pernah membuat model yang
        // sehat dimatikan di tengah pemuatan, dan gejalanya menyerupai crash.
        var env = new EnvSource
        {
            File = new Dictionary<string, string>
            {
                ["VTUBER_TTS_BATAS_DETIK"] = "75",
                ["VTUBER_TTS_PEKERJA_SIAP_DETIK"] = "180",
            },
        };

        var konfig = ConfigReader.Baca(env, "C:\\akar");

        Assert.Equal(75, konfig.TtsBatasDetik);
        Assert.Equal(180, konfig.TtsPekerjaSiapDetik);
        Assert.True(konfig.TtsPekerjaSiapDetik > konfig.TtsBatasDetik);
    }

    [Theory]
    [InlineData("local", "local")]
    [InlineData("llama_cpp", "local")]
    [InlineData("ollama", "ollama")]
    [InlineData("vulkan", "vulkan")]
    [InlineData("aneh", "vulkan")]
    public void BacaKonfig_MenormalisasiProvider(string mentah, string expected)
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["VTUBER_LLM_PROVIDER"] = mentah },
        };

        Assert.Equal(expected, ConfigReader.Baca(env, "C:\\akar").LlmProvider);
    }

    [Fact]
    public void BacaKonfig_NilaiTampakDiLuarDaftarJatuhKePet()
    {
        var env = new EnvSource
        {
            File = new Dictionary<string, string> { ["VTUBER_TAMPAK"] = "aneh" },
        };

        Assert.Equal("pet", ConfigReader.Baca(env, "C:\\akar").Tampak);
    }
}
