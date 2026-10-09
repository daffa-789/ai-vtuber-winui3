using System.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Menggabungkan beberapa WAV PCM menjadi satu berkas.
///
/// <para>
/// <b>Kenapa ini ada.</b> Sintesis jauh lebih lambat daripada pemutaran: RVC di
/// CPU membutuhkan 6–13 dtk per kalimat, sedangkan audionya hanya 2–3 dtk.
/// Kalau kalimat diputar sambil kalimat berikutnya masih disintesis, pemutar
/// selalu kehabisan bahan dan berhenti di setiap tanda baca — itulah suara yang
/// terdengar "setengah-setengah". Menggabung dulu lalu memutar SATU berkas
/// menghapus jeda itu sekaligus mengurangi jumlah buka-tutup perangkat audio
/// (yang juga salah satu sebab "kadang suaranya tidak keluar").
/// </para>
///
/// <para>
/// Penggabungan hanya sah kalau formatnya identik. Karena seluruh kalimat dalam
/// satu balasan memakai setelan suara yang sama — dan kunci cache memuat seluruh
/// parameter suara — formatnya memang selalu sama. Kalau ternyata berbeda,
/// fungsi ini mengembalikan <c>null</c> dan pemanggil memutar berurutan seperti
/// semula. Sengaja tidak melempar: kehilangan suara lebih buruk daripada jeda.
/// </para>
/// </summary>
internal static class GabungWav
{
    /// <summary>
    /// Gabungkan <paramref name="berkas"/> menjadi satu WAV di
    /// <paramref name="folder"/>. Mengembalikan jalur berkas gabungan, atau
    /// <c>null</c> kalau penggabungan tidak mungkin.
    ///
    /// <para>
    /// Satu berkas masukan dikembalikan apa adanya — tidak disalin, supaya
    /// cache tidak terbuang percuma.
    /// </para>
    /// </summary>
    /// <summary>
    /// Gabungkan beberapa WAV yang sudah ada di memori menjadi satu WAV di
    /// memori. Mengembalikan <c>null</c> kalau penggabungan tidak mungkin
    /// (format tidak seragam, atau ada isi yang bukan WAV).
    ///
    /// <para>
    /// Inilah jalur utama sejak 2026-10-09: audio tidak lagi ditulis ke
    /// <c>%TEMP%</c> hanya untuk digabung. Pemanggil yang mendapat
    /// <c>null</c> memutar klip satu per satu seperti sebelumnya.
    /// </para>
    /// </summary>
    public static byte[]? GabungkanMemori(IReadOnlyList<byte[]> berkas)
    {
        if (berkas.Count == 0)
        {
            return null;
        }

        if (berkas.Count == 1)
        {
            return KlipSuara.BerbentukWav(berkas[0]) ? berkas[0] : null;
        }

        var bagian = new List<Potongan>(berkas.Count);
        foreach (var isi in berkas)
        {
            var potongan = BacaBytes(isi);
            if (potongan is null)
            {
                return null;
            }

            bagian.Add(potongan.Value);
        }

        var format = bagian[0].Format;
        foreach (var potongan in bagian)
        {
            if (!potongan.Format.AsSpan().SequenceEqual(format))
            {
                return null;
            }
        }

        var total = 0;
        foreach (var potongan in bagian)
        {
            total += potongan.Data.Length;
        }

        // 44 bita header + data; format bisa lebih panjang dari 16 (WAVE_FORMAT_EXTENSIBLE).
        var keluaran = new byte[44 + format.Length - 16 + total];
        using var aliran = new MemoryStream(keluaran);
        using var tulis = new BinaryWriter(aliran, Encoding.ASCII);

        tulis.Write(Encoding.ASCII.GetBytes("RIFF"));
        tulis.Write(36 + format.Length + total);
        tulis.Write(Encoding.ASCII.GetBytes("WAVE"));

        tulis.Write(Encoding.ASCII.GetBytes("fmt "));
        tulis.Write(format.Length);
        tulis.Write(format);

        tulis.Write(Encoding.ASCII.GetBytes("data"));
        tulis.Write(total);
        foreach (var potongan in bagian)
        {
            tulis.Write(potongan.Data);
        }

        return keluaran;
    }

    public static string? Gabungkan(IReadOnlyList<string> berkas, string folder)
    {
        if (berkas.Count == 0)
        {
            return null;
        }

        if (berkas.Count == 1)
        {
            // Tidak disalin supaya cache tidak terbuang percuma, tetapi
            // keberadaannya tetap dipastikan — pemanggil memperlakukan null
            // sebagai "putar berurutan", bukan "berkasnya pasti ada".
            return File.Exists(berkas[0]) ? berkas[0] : null;
        }

        var bagian = new List<Potongan>(berkas.Count);
        foreach (var jalur in berkas)
        {
            var potongan = Baca(jalur);
            if (potongan is null)
            {
                return null;
            }

            bagian.Add(potongan.Value);
        }

        var format = bagian[0].Format;
        foreach (var potongan in bagian)
        {
            if (!potongan.Format.AsSpan().SequenceEqual(format))
            {
                return null;
            }
        }

        var total = 0;
        foreach (var potongan in bagian)
        {
            total += potongan.Data.Length;
        }

        var keluaran = Path.Combine(folder, $"sw-gabung-{Guid.NewGuid():N}.wav");
        try
        {
            using var aliran = File.Create(keluaran);
            using var tulis = new BinaryWriter(aliran, Encoding.ASCII);

            tulis.Write(Encoding.ASCII.GetBytes("RIFF"));
            // 4 ("WAVE") + 8+len (fmt) + 8 (data header) + data
            tulis.Write(36 + format.Length + total);
            tulis.Write(Encoding.ASCII.GetBytes("WAVE"));

            tulis.Write(Encoding.ASCII.GetBytes("fmt "));
            tulis.Write(format.Length);
            tulis.Write(format);

            tulis.Write(Encoding.ASCII.GetBytes("data"));
            tulis.Write(total);
            foreach (var potongan in bagian)
            {
                tulis.Write(potongan.Data);
            }

            return keluaran;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private readonly record struct Potongan(byte[] Format, byte[] Data);

    /// <summary>Baca chunk <c>fmt </c> dan <c>data</c> dari satu berkas WAV.</summary>
    private static Potongan? Baca(string jalur)
    {
        byte[] isi;
        try
        {
            isi = File.ReadAllBytes(jalur);
        }
        catch (IOException)
        {
            return null;
        }

        return BacaBytes(isi);
    }

    /// <summary>Baca chunk <c>fmt </c> dan <c>data</c> dari isi WAV di memori.</summary>
    private static Potongan? BacaBytes(byte[] isi)
    {
        if (isi.Length < 44
            || !Cocok(isi, 0, "RIFF")
            || !Cocok(isi, 8, "WAVE"))
        {
            return null;
        }

        byte[]? format = null;
        byte[]? data = null;

        var pos = 12;
        while (pos + 8 <= isi.Length)
        {
            var ukuran = BitConverter.ToInt32(isi, pos + 4);
            var mulai = pos + 8;
            if (ukuran < 0 || mulai + (long)ukuran > isi.Length)
            {
                break;
            }

            if (Cocok(isi, pos, "fmt "))
            {
                format = isi[mulai..(mulai + ukuran)];
            }
            else if (Cocok(isi, pos, "data"))
            {
                data = isi[mulai..(mulai + ukuran)];
            }

            // Chunk ganjil diberi satu byte padding.
            pos = mulai + ukuran + (ukuran % 2);
        }

        return format is null || data is null ? null : new Potongan(format, data);
    }

    private static bool Cocok(byte[] isi, int pos, string penanda)
    {
        if (pos + 4 > isi.Length)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (isi[pos + i] != (byte)penanda[i])
            {
                return false;
            }
        }

        return true;
    }
}
