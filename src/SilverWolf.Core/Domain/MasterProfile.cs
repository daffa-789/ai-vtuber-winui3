using System.Globalization;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Profil Master — fakta tentang Master yang butuh <b>hitungan</b>, bukan
/// sekadar baris yang ditempel ke prompt.
///
/// Kenapa terpisah dari <c>Fakta.md</c>: baris fakta hanya teks bebas, sehingga
/// model harus menghitung sendiri umur dan sisa hari menuju ulang tahun. Model
/// GGUF kecil hampir selalu salah menghitung tanggal. Dengan menyimpan tanggal
/// lahir terstruktur, hitungannya dikerjakan C# dan hasilnya yang diberikan ke
/// model.
///
/// ⚠️ Tanggal disimpan sebagai ISO <c>yyyy-MM-dd</c>. Jangan ubah formatnya:
/// berkas ini sudah ditulis ke mesin pengguna dan dibaca balik oleh aplikasi.
/// </summary>
public sealed class MasterProfile
{
    /// <summary>Nama berkas di dalam <c>silver_wolf_memory/</c>.</summary>
    public const string Berkas = "Profil.md";

    public const string KunciLahir = "Tanggal lahir";
    public const string KunciPanggilan = "Nama panggilan";

    private const string FormatTanggal = "yyyy-MM-dd";

    /// <summary>Tanggal lahir Master. Null bila belum dicatat.</summary>
    public DateOnly? Lahir { get; set; }

    /// <summary>Sebutan yang dipakai Silver Wolf untuk Master. Null = pakai bawaan persona.</summary>
    public string? Panggilan { get; set; }

    // ── Baca / tulis berkas ─────────────────────────────────────────────────

    /// <summary>
    /// Baca profil dari isi <c>Profil.md</c>. Mengembalikan <c>null</c> bila
    /// teksnya kosong sehingga pemanggil bisa membedakan "belum ada berkas" dan
    /// "berkas ada tetapi tidak memuat tanggal lahir".
    /// </summary>
    public static MasterProfile? Parse(string? teks)
    {
        if (string.IsNullOrWhiteSpace(teks))
        {
            return null;
        }

        var hasil = new MasterProfile();
        var punyaIsi = false;

        foreach (var baris in teks.Split('\n'))
        {
            var bersih = baris.Trim();

            // Lewati front-matter, judul, dan daftar — hanya baris "Kunci: nilai".
            if (bersih.Length == 0 || bersih.StartsWith('#') || bersih.StartsWith('-') || bersih.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            var titikDua = bersih.IndexOf(':');
            if (titikDua <= 0)
            {
                continue;
            }

            var kunci = bersih[..titikDua].Trim();
            var nilai = bersih[(titikDua + 1)..].Trim();

            if (string.Equals(kunci, KunciLahir, StringComparison.OrdinalIgnoreCase))
            {
                if (DateOnly.TryParseExact(nilai, FormatTanggal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lahir))
                {
                    hasil.Lahir = lahir;
                    punyaIsi = true;
                }
            }
            else if (string.Equals(kunci, KunciPanggilan, StringComparison.OrdinalIgnoreCase))
            {
                if (nilai.Length != 0 && !string.Equals(nilai, "-", StringComparison.Ordinal))
                {
                    hasil.Panggilan = nilai;
                    punyaIsi = true;
                }
            }
        }

        return punyaIsi ? hasil : null;
    }

    /// <summary>Balikan <see cref="Parse"/>: isi <c>Profil.md</c> siap tulis.</summary>
    public string KeTeks()
    {
        var baris = new List<string>
        {
            $"{KunciLahir}: {(Lahir is { } l ? l.ToString(FormatTanggal, CultureInfo.InvariantCulture) : "-")}",
            $"{KunciPanggilan}: {Panggilan ?? "-"}",
        };

        return string.Join('\n', baris) + "\n";
    }

    // ── Hitungan tanggal ────────────────────────────────────────────────────

    /// <summary>Umur Master pada <paramref name="hariIni"/>. Null bila tanggal lahir belum ada.</summary>
    public int? Umur(DateOnly hariIni)
    {
        if (Lahir is not { } lahir)
        {
            return null;
        }

        var umur = hariIni.Year - lahir.Year;
        if (TanggalUlangTahun(hariIni.Year, lahir) > hariIni)
        {
            umur--;
        }

        return umur < 0 ? null : umur;
    }

    /// <summary>
    /// Tanggal ulang tahun yang paling dekat — <b>hari ini sendiri</b> bila
    /// bertepatan. Null bila tanggal lahir belum ada.
    /// </summary>
    public DateOnly? UlangTahunBerikutnya(DateOnly hariIni)
    {
        if (Lahir is not { } lahir)
        {
            return null;
        }

        var kandidat = TanggalUlangTahun(hariIni.Year, lahir);
        return kandidat < hariIni ? TanggalUlangTahun(hariIni.Year + 1, lahir) : kandidat;
    }

    /// <summary>Sisa hari menuju ulang tahun. 0 = hari ini.</summary>
    public int? HariMenujuUlangTahun(DateOnly hariIni) =>
        UlangTahunBerikutnya(hariIni) is { } ulang ? ulang.DayNumber - hariIni.DayNumber : null;

    public bool BerulangTahunHariIni(DateOnly hariIni) => HariMenujuUlangTahun(hariIni) == 0;

    /// <summary>Umur yang akan dicapai pada ulang tahun berikutnya.</summary>
    public int? UmurBerikutnya(DateOnly hariIni) =>
        Lahir is { } lahir && UlangTahunBerikutnya(hariIni) is { } ulang
            ? ulang.Year - lahir.Year
            : null;

    /// <summary>
    /// Tanggal ulang tahun pada tahun tertentu. Hari 29 Februari digeser ke 28
    /// bila tahunnya tidak kabisat — <c>DateOnly</c> melempar untuk tanggal yang
    /// tidak ada.
    /// </summary>
    private static DateOnly TanggalUlangTahun(int tahun, DateOnly lahir) =>
        new(tahun, lahir.Month, Math.Min(lahir.Day, DateTime.DaysInMonth(tahun, lahir.Month)));
}
