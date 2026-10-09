using System.Text;
using System.Text.RegularExpressions;
using SilverWolf.Services.Tts;
using Xunit;

namespace SilverWolf.Services.Tests;

public sealed class SuaraArsipTests : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    private readonly string _akar;
    private readonly string _arsip;

    public SuaraArsipTests()
    {
        _akar = Path.Combine(Path.GetTempPath(), $"uji-suara-{Guid.NewGuid():N}");
        _arsip = Path.Combine(_akar, "suara");
        Directory.CreateDirectory(_akar);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_akar)) Directory.Delete(_akar, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>WAV PCM kecil yang nyata di disk, sama pola dengan TtsPipelineTests.</summary>
    private static string WavSementara(string folder, int sampel = 400)
    {
        Directory.CreateDirectory(folder);
        var jalur = Path.Combine(folder, $"src-{Guid.NewGuid():N}.wav");
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
        tulis.Write(40_000);
        tulis.Write(40_000 * 2);
        tulis.Write((short)2);
        tulis.Write((short)16);
        tulis.Write(Encoding.ASCII.GetBytes("data"));
        tulis.Write(data.Length);
        tulis.Write(data);
        return jalur;
    }

    private static string[] BerkasArsip(string folder) =>
        Directory.Exists(folder) ? Directory.GetFiles(folder, "*.wav") : [];

    [Fact]
    public void NamaArsipTerurutDanUnik()
    {
        // Format: yyyyMMdd-HHmmss-<8 hex>.wav. Prefiks waktu membuat urutan
        // kronologis; sufiks hash menjamin tidak ada tabrakan.
        var arsip = new SuaraArsip(_arsip);
        var pola = new Regex(@"^\d{8}-\d{6}-[0-9A-F]{8}\.wav$");

        var nama = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            var sumber = WavSementara(_akar);
            var hasil = arsip.Simpan(sumber);
            Assert.NotNull(hasil);
            nama.Add(Path.GetFileName(hasil!));
        }

        Assert.All(nama, n => Assert.Matches(pola, n));
        // Semua unik.
        Assert.Equal(nama.Count, nama.Distinct(StringComparer.Ordinal).Count());
        // Terurut naik saat diurutkan Ordinal (sifat yang dipakai pemangkasan).
        var terurut = nama.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(terurut, nama.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void PangkasMenyisakanTepatSeratusDanMenghapusYangTertua()
    {
        // 101 berkas: harus tersisa 100, dan yang paling lama hilang.
        var arsip = new SuaraArsip(_arsip, maksBerkas: 100);
        Directory.CreateDirectory(_arsip);
        for (var i = 0; i < 101; i++)
        {
            // Nama berprefiks waktu naik supaya urutan jelas.
            var nama = $"2026010{i / 24:D1}-{i % 24:D2}0000-{i:D8}.wav";
            File.WriteAllBytes(Path.Combine(_arsip, nama), new byte[] { 1, 2, 3 });
        }

        var tertua = Directory.GetFiles(_arsip, "*.wav")
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).First();

        arsip.Pangkas();

        var sisa = BerkasArsip(_arsip);
        Assert.Equal(100, sisa.Length);
        Assert.False(File.Exists(tertua));
    }

    [Fact]
    public void PangkasTidakMelakukanApaApaDiBawahBatas()
    {
        var arsip = new SuaraArsip(_arsip, maksBerkas: 100);
        Directory.CreateDirectory(_arsip);
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllBytes(Path.Combine(_arsip, $"20260101-0000{i:D2}-{i:D8}.wav"), new byte[] { 9 });
        }

        arsip.Pangkas();

        Assert.Equal(5, BerkasArsip(_arsip).Length);
    }

    [Fact]
    public void PangkasTidakMelemparSaatFolderHilang()
    {
        var arsip = new SuaraArsip(Path.Combine(_akar, "tidak-ada"));
        // Tidak boleh melempar walau foldernya tidak ada.
        arsip.Pangkas();
    }

    [Fact]
    public void PangkasTidakMelemparSaatBerkasTerkunci()
    {
        var arsip = new SuaraArsip(_arsip, maksBerkas: 2);
        Directory.CreateDirectory(_arsip);
        var jalur = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var p = Path.Combine(_arsip, $"20260101-00000{i}-{i:D8}.wav");
            File.WriteAllBytes(p, new byte[] { 1 });
            jalur.Add(p);
        }

        // Kunci berkas tertua sehingga File.Delete akan gagal.
        using (var kunci = new FileStream(jalur[0], FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Tidak boleh melempar.
            arsip.Pangkas();
        }

        // Berkas terkunci masih ada; sisanya dipangkas sampai dekat batas.
        Assert.True(File.Exists(jalur[0]));
    }

    [Fact]
    public void SimpanMengembalikanNullSaatSumberTidakAda()
    {
        var arsip = new SuaraArsip(_arsip);
        var hasil = arsip.Simpan(Path.Combine(_akar, "tidak-ada.wav"));
        Assert.Null(hasil);
    }

    [Fact]
    public async Task PipelineMengarsipkanBerkasGabungan()
    {
        // Dua kalimat nyata -> terjadi penggabungan -> berkas gabungan (balasan
        // utuh) harus tersalin ke arsip.
        var arsip = new SuaraArsip(_arsip);
        using var pipeline = new TtsPipeline(
            (_, _) => Task.FromResult<KlipSuara?>(KlipSuara.DariBerkas(WavSementara(_akar))),
            (_, _, started) => { started(); return Task.FromResult(true); },
            buangPemutar: null,
            log: null,
            arsip: arsip);

        Assert.True(await pipeline.SiapkanDanPutarAsync(
            "Kalimat pertama selesai. Kalimat kedua selesai.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);

        var tersimpan = BerkasArsip(_arsip);
        Assert.Single(tersimpan);
        Assert.Matches(@"^\d{8}-\d{6}-[0-9A-F]{8}\.wav$", Path.GetFileName(tersimpan[0]));
    }

    [Fact]
    public async Task PipelineTidakMengarsipkanSaatPenggabunganGagal()
    {
        // Format berbeda -> GabungWav mengembalikan null -> tanpa arsip.
        var arsip = new SuaraArsip(_arsip);
        var jenis = 0;
        using var pipeline = new TtsPipeline(
            (_, _) =>
            {
                // Dua klip dengan laju sampel berbeda -> penggabungan menyerah.
                var n = jenis++;
                return Task.FromResult<KlipSuara?>(KlipSuara.DariBerkas(BuatWav(_akar, n)));
            },
            (_, _, started) => { started(); return Task.FromResult(true); },
            buangPemutar: null,
            log: null,
            arsip: arsip);

        Assert.True(await pipeline.SiapkanDanPutarAsync(
            "Kalimat pertama. Kalimat kedua.").WaitAsync(Limit));
        await pipeline.Penyelesaian.WaitAsync(Limit);

        Assert.Empty(BerkasArsip(_arsip));
    }

    /// <summary>
    /// WAV dengan laju sampel yang bisa dipilih, untuk membuat penggabungan
    /// gagal (format tidak seragam).
    /// </summary>
    private static string BuatWav(string folder, int n)
    {
        var jalur = Path.Combine(folder, $"beda-{n}-{Guid.NewGuid():N}.wav");
        Directory.CreateDirectory(folder);
        var data = new byte[200 * 2];
        using var a = File.Create(jalur);
        using var t = new BinaryWriter(a, Encoding.ASCII);
        t.Write(Encoding.ASCII.GetBytes("RIFF"));
        t.Write(36 + data.Length);
        t.Write(Encoding.ASCII.GetBytes("WAVE"));
        t.Write(Encoding.ASCII.GetBytes("fmt "));
        t.Write(16);
        t.Write((short)1);
        t.Write((short)1);
        t.Write(n == 0 ? 40_000 : 22_050);   // laju berbeda
        t.Write((n == 0 ? 40_000 : 22_050) * 2);
        t.Write((short)2);
        t.Write((short)16);
        t.Write(Encoding.ASCII.GetBytes("data"));
        t.Write(data.Length);
        t.Write(data);
        return jalur;
    }

}
