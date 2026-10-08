using SilverWolf.Core.Domain;

namespace SilverWolf.Core.Tests;

/// <summary>
/// Uji alat waktu. Tanggal lahir acuan: <b>16 Oktober 2005</b> — tanggal
/// sesungguhnya yang Master berikan, dipakai apa adanya supaya ujinya
/// menggambarkan pemakaian nyata, bukan angka buatan.
/// </summary>
public class TimeToolTests
{
    private static readonly DateOnly LahirMaster = new(2005, 10, 16);

    private static MasterProfile ProfilMaster() => new() { Lahir = LahirMaster, Panggilan = "Master" };

    // ── Hitungan tanggal ────────────────────────────────────────────────────

    [Fact]
    public void Umur_MasihDuaPuluhSebelumHariUlangTahun()
    {
        // 9 Okt 2026 — ulang tahun baru 7 hari lagi, jadi belum genap 21.
        Assert.Equal(20, ProfilMaster().Umur(new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void Umur_GenapTepatDiHariUlangTahun()
    {
        Assert.Equal(21, ProfilMaster().Umur(new DateOnly(2026, 10, 16)));
    }

    [Fact]
    public void Umur_SudahBertambahSehariSetelahUlangTahun()
    {
        Assert.Equal(21, ProfilMaster().Umur(new DateOnly(2026, 10, 17)));
    }

    [Fact]
    public void HariMenujuUlangTahun_MenghitungSisaHari()
    {
        Assert.Equal(7, ProfilMaster().HariMenujuUlangTahun(new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void HariMenujuUlangTahun_NolPadaHariItuSendiri()
    {
        Assert.Equal(0, ProfilMaster().HariMenujuUlangTahun(new DateOnly(2026, 10, 16)));
    }

    [Fact]
    public void UlangTahunBerikutnya_LompatKeTahunDepanBilaSudahLewat()
    {
        // 1 Nov 2026 — ulang tahun 2026 sudah lewat, jadi berikutnya 2027.
        Assert.Equal(new DateOnly(2027, 10, 16), ProfilMaster().UlangTahunBerikutnya(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void UmurBerikutnya_DihitungDariTahunUlangTahunMendatang()
    {
        Assert.Equal(22, ProfilMaster().UmurBerikutnya(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Hitungan_NullBilaTanggalLahirBelumAda()
    {
        var kosong = new MasterProfile();

        Assert.Null(kosong.Umur(new DateOnly(2026, 10, 9)));
        Assert.Null(kosong.HariMenujuUlangTahun(new DateOnly(2026, 10, 9)));
        Assert.Null(kosong.UlangTahunBerikutnya(new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void UlangTahun_29FebruariTidakMelemparDiTahunBiasa()
    {
        var kabisat = new MasterProfile { Lahir = new DateOnly(2004, 2, 29) };

        // 2027 bukan tahun kabisat — tanggalnya digeser ke 28, bukan melempar.
        Assert.Equal(new DateOnly(2027, 2, 28), kabisat.UlangTahunBerikutnya(new DateOnly(2026, 3, 1)));
    }

    // ── Keluaran alat ───────────────────────────────────────────────────────

    [Fact]
    public void Panggil_MenyebutTanggalDanJamMesin()
    {
        var alat = new AlatWaktu(ProfilMaster());

        var hasil = alat.Panggil(new DateTimeOffset(2026, 10, 9, 3, 4, 0, TimeSpan.FromHours(7)));

        Assert.Contains("Jumat, 9 Oktober 2026", hasil, StringComparison.Ordinal);
        Assert.Contains("Jam: 03:04", hasil, StringComparison.Ordinal);
        Assert.Contains("dini hari", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void Panggil_MenyebutHitungMundurUlangTahun()
    {
        var alat = new AlatWaktu(ProfilMaster());

        var hasil = alat.Panggil(new DateTimeOffset(2026, 10, 9, 20, 0, 0, TimeSpan.FromHours(7)));

        Assert.Contains("Ulang tahun Master: 16 Oktober", hasil, StringComparison.Ordinal);
        Assert.Contains("7 hari lagi", hasil, StringComparison.Ordinal);
        Assert.Contains("genap 21 tahun", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void Panggil_SatuHariDisebutBesokBukanSatuHari()
    {
        var alat = new AlatWaktu(ProfilMaster());

        var hasil = alat.Panggil(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(7)));

        Assert.Contains("besok", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void Panggil_HariIniUlangTahunnyaSendiri()
    {
        var alat = new AlatWaktu(ProfilMaster());

        var hasil = alat.Panggil(new DateTimeOffset(2026, 10, 16, 0, 30, 0, TimeSpan.FromHours(7)));

        Assert.Contains("ulang tahun Master yang ke-21", hasil, StringComparison.Ordinal);
        Assert.Contains("Ucapkan selamat", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void Panggil_MengakuiBelumTahuBilaProfilKosong()
    {
        var hasil = new AlatWaktu().Panggil(new DateTimeOffset(2026, 10, 9, 3, 4, 0, TimeSpan.FromHours(7)));

        Assert.Contains("belum tercatat", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void Panggil_MenegaskanBahwaAngkanyaBukanTebakan()
    {
        var hasil = new AlatWaktu(ProfilMaster()).Panggil();

        // Kalimat ini yang mencegah model mengarang tanggal sendiri.
        Assert.Contains("jangan menebak tanggal", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void BagianHari_BatasnyaSesuaiRitmeHarian()
    {
        Assert.Equal("dini hari (lewat tengah malam)", AlatWaktu.BagianHari(new TimeOnly(0, 0)));
        Assert.Equal("menjelang subuh", AlatWaktu.BagianHari(new TimeOnly(4, 30)));
        Assert.Equal("pagi", AlatWaktu.BagianHari(new TimeOnly(6, 0)));
        Assert.Equal("siang", AlatWaktu.BagianHari(new TimeOnly(11, 0)));
        Assert.Equal("sore", AlatWaktu.BagianHari(new TimeOnly(15, 0)));
        Assert.Equal("malam", AlatWaktu.BagianHari(new TimeOnly(18, 0)));
        Assert.Equal("malam larut (sudah waktunya istirahat)", AlatWaktu.BagianHari(new TimeOnly(23, 59)));
    }

    // ── Registri alat ───────────────────────────────────────────────────────

    [Fact]
    public void DaftarAlat_MenggabungkanSemuaAlatOtomatis()
    {
        var hasil = new DaftarAlat()
            .Tambah(new AlatWaktu(ProfilMaster(), () => new DateTimeOffset(2026, 10, 9, 3, 4, 0, TimeSpan.Zero)))
            .JalankanOtomatis();

        Assert.Contains("## Jam & tanggal mesin", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void DaftarAlat_AlatYangMelemparDilewatiBukanMenggagalkanGiliran()
    {
        var hasil = new DaftarAlat()
            .Tambah(new AlatRusak())
            .Tambah(new AlatWaktu(ProfilMaster(), () => new DateTimeOffset(2026, 10, 9, 3, 4, 0, TimeSpan.Zero)))
            .JalankanOtomatis();

        Assert.Contains("## Jam & tanggal mesin", hasil, StringComparison.Ordinal);
        Assert.DoesNotContain("rusak", hasil, StringComparison.Ordinal);
    }

    [Fact]
    public void DaftarAlat_AlatTidakOtomatisTidakIkut()
    {
        var hasil = new DaftarAlat().Tambah(new AlatDiam()).JalankanOtomatis();

        Assert.Equal(string.Empty, hasil);
    }

    private sealed class AlatRusak : IAlat
    {
        public string Nama => "rusak";
        public string Deskripsi => "Selalu melempar.";
        public bool Otomatis => true;
        public string Panggil() => throw new InvalidOperationException("alat rusak");
    }

    private sealed class AlatDiam : IAlat
    {
        public string Nama => "diam";
        public string Deskripsi => "Hanya jalan bila dipanggil.";
        public bool Otomatis => false;
        public string Panggil() => "seharusnya tidak muncul";
    }
}
