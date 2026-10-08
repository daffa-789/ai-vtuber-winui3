namespace SilverWolf.Core.Domain;

/// <summary>
/// Satu alat yang bisa dipanggil Silver Wolf untuk mengambil fakta dari dunia
/// nyata — padanan konsep <i>function calling</i>, tetapi tanpa bergantung pada
/// kemampuan model GGUF mengeluarkan JSON <c>tool_calls</c>.
///
/// Alasan desain: llama-server hanya meneruskan apa yang dikeluarkan template
/// obrolan model. Model kecil yang dipakai aplikasi ini tidak andal menghasilkan
/// <c>tool_calls</c>, jadi alat tidak menunggu diminta model. Alat bertanda
/// <see cref="Otomatis"/> dijalankan setiap giliran dan hasilnya disuntikkan ke
/// prompt sistem, sehingga jawabannya tetap benar walau modelnya tidak pernah
/// memanggil alat sama sekali.
/// </summary>
public interface IAlat
{
    /// <summary>Nama singkat, dipakai untuk mencari alat di <see cref="DaftarAlat"/>.</summary>
    string Nama { get; }

    /// <summary>Satu kalimat penjelasan untuk manusia yang membaca log.</summary>
    string Deskripsi { get; }

    /// <summary>
    /// True = dijalankan otomatis setiap giliran dan hasilnya masuk ke prompt.
    /// False = hanya jalan bila dipanggil eksplisit lewat <see cref="DaftarAlat.Panggil"/>.
    /// </summary>
    bool Otomatis { get; }

    /// <summary>Hasil yang siap ditempel ke prompt sistem.</summary>
    string Panggil();
}

/// <summary>
/// Registri alat. Sengaja dibuat kebal terhadap alat yang melempar: satu alat
/// yang gagal tidak boleh membatalkan seluruh giliran obrolan.
/// </summary>
public sealed class DaftarAlat
{
    private readonly List<IAlat> _alat = [];

    public DaftarAlat Tambah(IAlat alat)
    {
        ArgumentNullException.ThrowIfNull(alat);
        _alat.Add(alat);
        return this;
    }

    public IReadOnlyList<IAlat> Semua => _alat;

    public IAlat? Cari(string nama) =>
        _alat.FirstOrDefault(a => string.Equals(a.Nama, nama, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Jalankan semua alat otomatis dan gabung hasilnya. Alat yang melempar
    /// dilewati — lebih baik jawaban tanpa tanggal daripada tidak menjawab.
    /// </summary>
    public string JalankanOtomatis()
    {
        var bagian = new List<string>();

        foreach (var alat in _alat.Where(a => a.Otomatis))
        {
            string hasil;
            try
            {
                hasil = alat.Panggil();
            }
            catch (Exception)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(hasil))
            {
                bagian.Add(hasil.Trim());
            }
        }

        return string.Join("\n\n", bagian);
    }

    /// <summary>Panggil satu alat dengan namanya. Null bila tidak terdaftar.</summary>
    public string? Panggil(string nama) => Cari(nama)?.Panggil();
}
