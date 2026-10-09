using System.Text;

namespace SilverWolf.Services.Tts;

/// <summary>
/// Satu potong audio WAV yang hidup sepenuhnya di <b>memori</b>.
///
/// <para>
/// <b>Kenapa perlu (permintaan Master, 2026-10-09).</b> Rantai suara dulu
/// menuliskan setiap kalimat ke berkas di <c>%TEMP%</c> dan memutarnya dari
/// sana. Berkas-berkas itu menumpuk tanpa batas — setiap kalimat yang pernah
/// diucapkan meninggalkan WAV 40 kHz yang tidak pernah dibuang sendiri, dan
/// folder <c>silverwolf-tts</c> tumbuh terus selama berhari-hari.
/// </para>
///
/// <para>
/// Sekarang: WAV dibuat Python ke berkas sementara, byte-nya dibaca ke sini,
/// lalu berkasnya <b>dihapus segera</b>. Pemutaran, penggabungan, dan analisis
/// amplitudo (LipSync) semuanya terjadi di memori. Sisa berkas apa pun disapu
/// saat aplikasi ditutup — lihat <see cref="TempAudio"/>.
/// </para>
///
/// <para>
/// Isinya WAV utuh (header RIFF + chunk <c>fmt </c> + <c>data</c>), jadi bisa
/// langsung dibuka oleh <see cref="NAudio.Wave.WaveFileReader"/> lewat
/// <see cref="MemoryStream"/>.
/// </para>
/// </summary>
public sealed class KlipSuara
{
    /// <summary>Panjang header WAV terkecil yang masih mungkin (RIFF+fmt+data).</summary>
    private const int Minimal = 44;

    /// <summary>Isi berkas WAV utuh.</summary>
    public byte[] Data { get; }

    /// <summary>Penanda untuk log — bukan jalur berkas, karena berkasnya sudah dihapus.</summary>
    public string Label { get; }

    public KlipSuara(byte[] data, string label = "memori")
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Label = string.IsNullOrWhiteSpace(label) ? "memori" : label;
    }

    /// <summary>Ukuran dalam bita. Dipakai untuk membatasi cache memori.</summary>
    public int Ukuran => Data.Length;

    /// <summary>
    /// Baca seluruh berkas WAV ke memori. Mengembalikan <c>null</c> bila berkas
    /// tidak ada, tidak terbaca, atau bukan WAV — pemanggil boleh menganggapnya
    /// "kalimat ini tidak menghasilkan audio" dan lanjut ke kalimat berikutnya.
    /// </summary>
    public static KlipSuara? DariBerkas(string jalur)
    {
        if (string.IsNullOrWhiteSpace(jalur))
        {
            return null;
        }

        byte[] isi;
        try
        {
            isi = File.ReadAllBytes(jalur);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return BerbentukWav(isi)
            ? new KlipSuara(isi, Path.GetFileName(jalur))
            : null;
    }

    /// <summary>
    /// Pemeriksaan murah: panjang minimal + penanda RIFF/WAVE. Bukan pengganti
    /// <see cref="TtsWorker.WavSah"/>, tetapi cukup untuk menolak berkas kosong
    /// atau terpotong sebelum menyimpannya ke cache memori.
    /// </summary>
    public static bool BerbentukWav(byte[] isi) =>
        isi.Length >= Minimal
        && Encoding.ASCII.GetString(isi, 0, 4) == "RIFF"
        && Encoding.ASCII.GetString(isi, 8, 4) == "WAVE";
}
