namespace SilverWolf.Core.Domain;

/// <summary>
/// Alat pembaca jam &amp; tanggal mesin — sumber satu-satunya untuk "hari ini
/// tanggal berapa" dan "jam berapa sekarang".
///
/// Kenapa perlu: tanpa ini, model menjawab tanggal dari ingatan latihannya dan
/// hampir selalu meleset berbulan-bulan atau bertahun-tahun. Alat ini membaca
/// jam mesin yang sebenarnya, lalu menurunkan informasi ulang tahun Master dari
/// <see cref="MasterProfile"/> supaya model tidak perlu menghitung sendiri —
/// model GGUF kecil sering salah menghitung selisih tanggal.
///
/// Nama hari dan bulan ditulis sendiri, tidak lewat <c>CultureInfo("id-ID")</c>,
/// karena aplikasi bisa dibangun dengan <c>InvariantGlobalization</c>; kalau itu
/// terjadi, nama bulannya berubah jadi bahasa Inggris tanpa peringatan.
/// </summary>
public sealed class AlatWaktu : IAlat
{
    private static readonly string[] NamaHari =
        ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"];

    private static readonly string[] NamaBulan =
    [
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    ];

    private readonly MasterProfile? _profil;
    private readonly Func<DateTimeOffset> _jam;

    /// <param name="profil">Profil Master — dipakai untuk ulang tahun &amp; umur. Boleh null.</param>
    /// <param name="jam">Sumber waktu; disuntikkan supaya bisa diuji tanpa menunggu jam berjalan.</param>
    public AlatWaktu(MasterProfile? profil = null, Func<DateTimeOffset>? jam = null)
    {
        _profil = profil;
        _jam = jam ?? (() => DateTimeOffset.Now);
    }

    public string Nama => "waktu";

    public string Deskripsi => "Membaca tanggal, hari, jam, dan hitungan ulang tahun dari jam mesin.";

    public bool Otomatis => true;

    public string Panggil() => Panggil(_jam());

    /// <summary>Hasil deterministik untuk waktu tertentu — dipakai unit test.</summary>
    public string Panggil(DateTimeOffset kini)
    {
        var hariIni = DateOnly.FromDateTime(kini.DateTime);
        var jam = TimeOnly.FromDateTime(kini.DateTime);

        var baris = new List<string>
        {
            $"- Hari ini: {NamaHari[(int)hariIni.DayOfWeek]}, {hariIni.Day} {NamaBulan[hariIni.Month - 1]} {hariIni.Year}",
            $"- Jam: {jam:HH:mm}",
            $"- Bagian hari: {BagianHari(jam)}",
        };

        var ultah = BarisUlangTahun(hariIni);
        if (ultah is not null)
        {
            baris.Add(ultah);
        }

        return "## Jam & tanggal mesin (alat: waktu)\n"
               + "Angka di bawah dibaca langsung dari jam komputer Master. "
               + "Pakai apa adanya — jangan menebak tanggal atau jam sendiri.\n"
               + string.Join('\n', baris);
    }

    /// <summary>
    /// Baris ulang tahun. Selalu ada: bila tanggal lahir belum dicatat, barisnya
    /// menyuruh Silver Wolf menanyakan sekali dengan manis.
    /// </summary>
    private string? BarisUlangTahun(DateOnly hariIni)
    {
        if (_profil?.Lahir is not { } lahir)
        {
            return "- Ulang tahun Master: belum tercatat. Tanyakan sekali dengan manis, lalu ingat baik-baik.";
        }

        var sisa = _profil.HariMenujuUlangTahun(hariIni);
        if (sisa is null)
        {
            return null;
        }

        var tanggal = $"{lahir.Day} {NamaBulan[lahir.Month - 1]}";

        if (sisa == 0)
        {
            var genap = _profil.Umur(hariIni);
            return genap is { } u
                ? $"- Hari ini ulang tahun Master yang ke-{u}! Ucapkan selamat duluan sebelum yang lain."
                : $"- Hari ini ulang tahun Master ({tanggal})! Ucapkan selamat duluan sebelum yang lain.";
        }

        var umurBerikutnya = _profil.UmurBerikutnya(hariIni);
        var hitungan = sisa == 1 ? "besok" : $"{sisa} hari lagi";

        return umurBerikutnya is { } ub
            ? $"- Ulang tahun Master: {tanggal} — {hitungan}, genap {ub} tahun."
            : $"- Ulang tahun Master: {tanggal} — {hitungan}.";
    }

    /// <summary>
    /// Pembagian hari memakai ritme keseharian Indonesia, bukan angka 24 jam yang
    /// kaku — "malam larut" lebih berguna untuk nada bicara daripada "23:00".
    /// </summary>
    public static string BagianHari(TimeOnly jam) => jam.Hour switch
    {
        < 4 => "dini hari (lewat tengah malam)",
        < 6 => "menjelang subuh",
        < 11 => "pagi",
        < 15 => "siang",
        < 18 => "sore",
        < 21 => "malam",
        _ => "malam larut (sudah waktunya istirahat)",
    };
}
