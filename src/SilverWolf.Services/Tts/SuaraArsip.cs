using System.Security.Cryptography;
using System.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Menyimpan salinan WAV balasan ke arsip permanen dan memangkasnya bergilir.
///
/// <para>
/// <b>Kenapa salinan, bukan pindah.</b> Berkas gabungan hidup di
/// <c>%TEMP%</c> dan dihapus setelah diputar (lihat <see cref="TtsPipeline"/>).
/// Arsip harus tetap ada sesudahnya, jadi isinya DISALIN sebelum yang asli
/// dibuang. Pemutaran tetap memakai berkas sementara — arsip hanya untuk
/// riwayat.
/// </para>
///
/// <para>
/// <b>Batas jumlah, bukan batas umur.</b> Menyimpan N berkas terbaru membuat
/// ukuran arsip terbatas dan bisa diprediksi tanpa perlu jam dinding. Umur
/// ditentukan dari URUTAN NAMA berkas (prefiks waktu membuat urutan itu
/// kronologis), bukan dari timestamp filesystem — timestamp bisa berubah saat
/// disalin atau dipulihkan, sedangkan nama tidak.
/// </para>
/// </summary>
internal sealed class SuaraArsip
{
    /// <summary>Bawaan: simpan 100 balasan terbaru.</summary>
    public const int MaksBawaan = 100;

    private readonly string _folder;
    private readonly int _maks;
    private readonly Action<string>? _log;

    /// <param name="folderSuara">Folder arsip. Dibuat saat menyimpan bila belum ada.</param>
    /// <param name="maksBerkas">Jumlah berkas terbaru yang dipertahankan.</param>
    /// <param name="log">Pencatat opsional; dipanggil dari utas pemanggil.</param>
    public SuaraArsip(string folderSuara, int maksBerkas = MaksBawaan, Action<string>? log = null)
    {
        _folder = folderSuara;
        _maks = maksBerkas < 1 ? 1 : maksBerkas;
        _log = log;
    }

    /// <summary>
    /// Salin <paramref name="sumberWav"/> ke arsip, lalu pangkas. Mengembalikan
    /// jalur arsip, atau <c>null</c> bila penyalinan gagal — kegagalan arsip
    /// tidak boleh menghentikan pemutaran.
    /// </summary>
    public string? Simpan(string sumberWav)
    {
        try
        {
            return SimpanBytes(File.ReadAllBytes(sumberWav), sumberWav);
        }
        catch (IOException galat)
        {
            _log?.Invoke($"[tts] arsip suara gagal: {galat.Message}");
            return null;
        }
        catch (UnauthorizedAccessException galat)
        {
            _log?.Invoke($"[tts] arsip suara ditolak: {galat.Message}");
            return null;
        }
    }

    /// <summary>
    /// Salin WAV yang sudah ada di memori ke arsip. Ini jalur yang dipakai
    /// pipeline sejak audio tidak lagi ditulis ke disk.
    /// </summary>
    public string? Simpan(KlipSuara klip) =>
        klip is null ? null : SimpanBytes(klip.Data, klip.Label);

    /// <summary>
    /// Tulis <paramref name="isi"/> ke arsip, lalu pangkas. Mengembalikan jalur
    /// arsip, atau <c>null</c> bila penulisan gagal — kegagalan arsip tidak
    /// boleh menghentikan pemutaran.
    /// </summary>
    private string? SimpanBytes(byte[] isi, string penanda)
    {
        try
        {
            Directory.CreateDirectory(_folder);

            var tujuan = NamaUnik(penanda);
            File.WriteAllBytes(tujuan, isi);

            // Pangkas SETELAH salinan baru ada di disk. Berkas arsip adalah
            // berkas BARU yang berbeda dari yang diputar, jadi pemangkasan
            // tidak mungkin menghapus audio yang sedang diputar.
            Pangkas();
            return tujuan;
        }
        catch (IOException galat)
        {
            _log?.Invoke($"[tts] arsip suara gagal: {galat.Message}");
            return null;
        }
        catch (UnauthorizedAccessException galat)
        {
            _log?.Invoke($"[tts] arsip suara ditolak: {galat.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sisakan paling banyak <c>_maks</c> berkas <c>.wav</c>, hapus yang paling
    /// lama lebih dulu. Urutan ditentukan dari nama berkas. Tidak pernah
    /// melempar — kegagalan memangkas tidak boleh merusak pemutaran.
    /// </summary>
    public void Pangkas()
    {
        try
        {
            // Enumerasi bisa melempar bila foldernya hilang atau tidak bisa
            // diakses; semuanya ditelan seperti ModelLocator/GabungWav.
            var berkas = Directory.GetFiles(_folder, "*.wav");
            if (berkas.Length <= _maks)
            {
                return;
            }

            Array.Sort(berkas, (a, b) => string.CompareOrdinal(
                Path.GetFileName(a), Path.GetFileName(b)));

            var kelebihan = berkas.Length - _maks;
            for (var i = 0; i < kelebihan; i++)
            {
                try
                {
                    File.Delete(berkas[i]);
                }
                catch (IOException)
                {
                    // Berkas mungkin masih terkunci; lewati saja.
                }
                catch (UnauthorizedAccessException)
                {
                    // Berkas read-only atau dilindungi; lewati saja.
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// <c>yyyyMMdd-HHmmss-&lt;8 hex&gt;.wav</c>. Prefiks waktu membuat nama
    /// terurut kronologis; sufiks hash mencegah tabrakan saat beberapa balasan
    /// lahir pada detik yang sama.
    /// </summary>
    private string NamaUnik(string sumberWav)
    {
        var cap = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        // Beberapa percobaan untuk kasus sangat jarang: tabrakan nama pada
        // detik yang sama dengan sumber yang sama.
        for (var i = 0; i < 8; i++)
        {
            var acak = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(sumberWav + "|" + Guid.NewGuid().ToString("N"))))[..8];
            var tujuan = Path.Combine(_folder, $"{cap}-{acak}.wav");
            if (!File.Exists(tujuan))
            {
                return tujuan;
            }
        }

        // Praktis tidak pernah tercapai; Guid menjamin keunikan.
        return Path.Combine(_folder, $"{cap}-{Guid.NewGuid():N}"[..24] + ".wav");
    }
}
