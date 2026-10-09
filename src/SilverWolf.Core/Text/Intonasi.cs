namespace SilverWolf.Core.Text;

/// <summary>
/// Pemetaan emosi → parameter intonasi untuk mesin TTS.
///
/// <para>
/// <b>Kenapa ini ada.</b> Piper memakai satu <c>--length-scale</c> untuk
/// seluruh kalimat: makin besar nilainya, makin lambat bicaranya. Kalau
/// nilainya tetap, semua emosi terdengar sama datar — Master meminta
/// "buat dia agar ada intonasi dan sebagainya".
/// </para>
///
/// <para>
/// <b>Yang BUKAN tugas kelas ini.</b> Nada naik-turun di tengah kalimat
/// (pitch contour) tidak bisa diatur Piper. Yang bisa diatur hanya
/// <i>tempo</i>. Jadi pendekatan yang dipakai:
/// </para>
/// <list type="bullet">
/// <item><description><b>Tempo per emosi</b> — semangat lebih cepat, lelah
/// lebih lambat. Ini yang paling terasa di telinga.</description></item>
/// <item><description><b>Tanda baca yang dipertahankan</b> — koma, titik,
/// tanya, seru, dan elipsis sudah memberi jeda dan kontur alami. Karena itu
/// <see cref="TeksUcapan"/> sengaja TIDAK membuangnya.</description></item>
/// <item><description><b>Sisipan jeda</b> — elipsis (<c>...</c>) membuat Piper
/// benar-benar berhenti sejenak, yang terbaca sebagai menggantung/ragu.</description></item>
/// </list>
///
/// <para>
/// Nilai <c>length-scale</c>: 1,0 = kecepatan normal model; &lt;1,0 lebih cepat;
/// &gt;1,0 lebih lambat. Bawaan proyek 0,9 (sedikit cepat, cocok untuk
/// karakter muda).
/// </para>
/// </summary>
public static class Intonasi
{
    /// <summary>Tempo bawaan bila emosi tidak dikenali.</summary>
    public const double TempoBawaan = 0.9;

    /// <summary>
    /// Rentang aman. Di luar ini Piper mulai terdengar cacat: terlalu cepat
    /// membuat konsonan bertumbukan, terlalu lambat terdengar mengantuk.
    /// </summary>
    public const double TempoMinimum = 0.75;
    public const double TempoMaksimum = 1.15;

    /// <summary>
    /// Ambil <c>length-scale</c> untuk sebuah tag emosi.
    ///
    /// <para>
    /// Tag datang dari LLM dalam huruf kecil, tetapi tetap dibandingkan
    /// case-insensitive karena balasan bisa saja memakai huruf besar.
    /// </para>
    /// </summary>
    public static double Tempo(string? emosi) => (emosi ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        // Bergairah, bersemangat, menggebu — bicara lebih cepat dan ringan.
        "semangat" => 0.80,

        // Terkejut — refleks orang kaget memang lebih cepat.
        "kaget" => 0.83,

        // Usil, menggoda, manja — sedikit lebih cepat, terdengar playful.
        "goda" => 0.86,

        // Senang, ramah — sedikit lebih cepat dari normal.
        "senyum" => 0.88,

        // Netral — mengikuti nilai bawaan proyek.
        "netral" => 0.90,

        // Bingung — melambat, seolah berpikir.
        "bingung" => 0.99,

        // Kesal, jengkel, cemberut — melambat dan menekan.
        "sebal" => 1.02,

        // Sedih — paling lambat, keluar kata per kata.
        "sedih" => 1.10,

        // Lelah, mengantuk — paling lambat dari semuanya.
        "lelah" => 1.15,

        _ => TempoBawaan,
    };

    /// <summary>
    /// Apakah emosi ini dikenal sistem. Dipakai untuk memutuskan apakah nilai
    /// tempo boleh dipakai atau harus jatuh ke bawaan.
    /// </summary>
    public static bool Dikenali(string? emosi) =>
        Domain.EmotionTags.Dikenali(emosi?.Trim().ToLowerInvariant());

    /// <summary>
    /// Sisipkan jeda yang eksplisit pada tanda baca supaya Piper benar-benar
    /// berhenti, bukan sekadar membaca cepat.
    ///
    /// <para>
    /// <b>Kenapa perlu.</b> Piper memperlakukan koma dan titik dengan jeda
    /// yang sangat pendek dan seragam, sehingga kalimat panjang terdengar
    /// mengalir rata tanpa napas. Menambah spasi di sekitar tanda baca
    /// memberi jeda yang lebih jelas tanpa menambah kata yang salah diucapkan.
    /// </para>
    ///
    /// <para>
    /// Hanya diterapkan pada tanda baca yang MEMANG ada di teks — bukan
    /// menyisipkan tanda baru, karena itu akan mengubah makna kalimat.
    /// </para>
    /// </summary>
    public static string BeriJeda(string? teks, string? emosi)
    {
        if (string.IsNullOrEmpty(teks))
        {
            return string.Empty;
        }

        var hasil = teks;

        // Elipsis dan tanda pisah sudah menjadi penanda jeda kuat; biarkan
        // apa adanya, Piper sudah menanganinya dengan baik.
        // Yang ditambah hanya jeda setelah koma pada kalimat panjang, karena
        // kalimat panjanglah yang terdengar mengalir rata.
        var kataPanjang = hasil.Length > 60;

        if (kataPanjang)
        {
            // Titik dan tanda tanya/seru: satu spasi ekstra memberi napas.
            hasil = hasil
                .Replace(". ", ".  ")
                .Replace("? ", "?  ")
                .Replace("! ", "!  ");
        }

        // Emosi lambat (sedih, lelah): tambah jeda di koma supaya makin
        // terasa berat dan ragu.
        var tempo = Tempo(emosi);
        if (tempo >= 1.0)
        {
            hasil = hasil.Replace(", ", ",  ");
        }

        return hasil;
    }
}
