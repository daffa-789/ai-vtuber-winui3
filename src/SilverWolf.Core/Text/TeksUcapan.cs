using System.Text.RegularExpressions;

namespace SilverWolf.Core.Text;

/// <summary>
/// Normalisasi teks SEBELUM disintesis menjadi suara.
///
/// <para>
/// <b>Kenapa ini ada.</b> LLM secara alami menulis penekanan ala Markdown —
/// <c>*partner*</c>, <c>**penting**</c>, <c>`kode`</c> — karena mayoritas
/// data latihnya berbentuk Markdown. Mesin TTS tidak tahu itu tanda format:
/// ia membacanya sebagai karakter, sehingga tanda bintang diucapkan
/// "bintang" dan teks terdengar kacau. Keluhan Master: "kaya ada * dia malah
/// mengatakan bintang".
/// </para>
///
/// <para>
/// <c>EmotionParser</c> hanya membersihkan tag <c>[emosi]</c>; tanda Markdown
/// lolos begitu saja. Kelas ini menutup celah itu.
/// </para>
///
/// <para>
/// <b>Prinsip:</b> yang dibuang hanya <i>penanda format</i>, bukan isinya.
/// <c>*partner*</c> → <c>partner</c> (bukan hilang), <c>**penting**</c> →
/// <c>penting</c>. Ini penting: membuang isinya akan membuat kalimat kehilangan
/// makna.
/// </para>
///
/// <para>
/// <b>Tanda baca yang MEMANG dibaca tetap dipertahankan</b> — titik, koma,
/// tanya, seru — karena justru itulah yang memberi intonasi pada TTS.
/// Yang dibuang hanya simbol yang tidak punya makna ucapan.
/// </para>
/// </summary>
public static partial class TeksUcapan
{
    /// <summary>Tebal + miring: **teks** dan *teks* → teks.</summary>
    [GeneratedRegex(@"\*{1,3}([^*\n]+)\*{1,3}")]
    private static partial Regex TebalMiring();

    /// <summary>Miring gaya garis bawah: _teks_ → teks (hanya di batas kata).</summary>
    [GeneratedRegex(@"(?<=^|[\s(])_([^_\n]+)_(?=$|[\s.,!?;)])")]
    private static partial Regex MiringGarisBawah();

    /// <summary>Kode inline: `teks` → teks.</summary>
    [GeneratedRegex(@"`{1,3}([^`\n]+)`{1,3}")]
    private static partial Regex Kode();

    /// <summary>Judul Markdown di awal baris: "### Judul" → "Judul".</summary>
    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex Judul();

    /// <summary>Butir daftar: "- item", "* item", "+ item" → "item".</summary>
    [GeneratedRegex(@"^\s{0,4}[-*+]\s+", RegexOptions.Multiline)]
    private static partial Regex ButirDaftar();

    /// <summary>Daftar bernomor: "1. item" → "item" (nomornya tidak diucapkan).</summary>
    [GeneratedRegex(@"^\s{0,4}\d+[.)]\s+", RegexOptions.Multiline)]
    private static partial Regex DaftarNomor();

    /// <summary>Tautan Markdown: [judul](url) → judul.</summary>
    [GeneratedRegex(@"\[([^\]\n]+)\]\([^)\n]*\)")]
    private static partial Regex Tautan();

    /// <summary>
    /// Sisa tanda bintang yang tidak berpasangan (mis. "*" tunggal atau "**"
    /// di ujung kalimat karena balasan terpotong streaming).
    /// </summary>
    [GeneratedRegex(@"\*+")]
    private static partial Regex BintangSisa();

    /// <summary>Sisa garis bawah yang tidak berpasangan.</summary>
    [GeneratedRegex(@"(?<=^|[\s(])_+|_+(?=$|[\s.,!?;)])")]
    private static partial Regex GarisBawahSisa();

    /// <summary>Sisa backtick yang tidak berpasangan.</summary>
    [GeneratedRegex(@"`+")]
    private static partial Regex BacktickSisa();

    /// <summary>Tanda pagar sisa di awal baris (mis. "### " yang tidak lengkap).</summary>
    [GeneratedRegex(@"(?m)^\s{0,3}#+\s*")]
    private static partial Regex PagarSisa();

    /// <summary>
    /// Em dash dan en dash → koma. TTS sering membacanya "strip" atau diam
    /// terlalu lama; koma memberi jeda yang benar tanpa suara aneh.
    /// </summary>
    [GeneratedRegex(@"\s*[—–]\s*")]
    private static partial Regex PisahPanjang();

    /// <summary>Bullet dan simbol hias yang tidak punya bunyi.</summary>
    [GeneratedRegex(@"[•▪▫◦‣·※→←↑↓]")]
    private static partial Regex SimbolHias();

    /// <summary>Spasi ganda dan spasi sebelum tanda baca dirapikan.</summary>
    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex SpasiGanda();

    /// <summary>Spasi yang menggantung sebelum tanda baca: "halo ," → "halo,".</summary>
    [GeneratedRegex(@"\s+([.,!?;:])")]
    private static partial Regex SpasiSebelumTanda();

    /// <summary>
    /// Bersihkan penanda Markdown supaya teks enak diucapkan mesin TTS.
    ///
    /// <para>
    /// Urutannya penting: teks berpasangan (<c>**tebal**</c>) dibersihkan lebih
    /// dulu, baru sisa simbol tunggal. Kalau dibalik, pola berpasangan tidak
    /// akan pernah cocok karena bintangnya sudah dibuang lebih dulu.
    /// </para>
    /// </summary>
    public static string Bersihkan(string? teks)
    {
        if (string.IsNullOrWhiteSpace(teks))
        {
            return string.Empty;
        }

        var hasil = teks;

        // 1. Bentuk berpasangan — ambil isinya, buang penandanya.
        hasil = TebalMiring().Replace(hasil, "$1");
        hasil = Kode().Replace(hasil, "$1");
        hasil = MiringGarisBawah().Replace(hasil, "$1");

        // 2. Struktur baris — judul, butir, penomoran, tautan.
        hasil = Judul().Replace(hasil, string.Empty);
        hasil = ButirDaftar().Replace(hasil, string.Empty);
        hasil = DaftarNomor().Replace(hasil, string.Empty);
        hasil = Tautan().Replace(hasil, "$1");

        // 3. Sisa simbol tunggal. Dijalankan SETELAH langkah 1 supaya tanda
        //    berpasangan yang belum sempat cocok (balasan terpotong) tetap
        //    ikut bersih.
        hasil = BintangSisa().Replace(hasil, string.Empty);
        hasil = GarisBawahSisa().Replace(hasil, string.Empty);
        hasil = BacktickSisa().Replace(hasil, string.Empty);
        hasil = PagarSisa().Replace(hasil, string.Empty);

        // 4. Simbol hias dan tanda pisah panjang.
        hasil = PisahPanjang().Replace(hasil, ", ");
        hasil = SimbolHias().Replace(hasil, string.Empty);

        // 5. Rapikan spasi terakhir.
        hasil = SpasiGanda().Replace(hasil, " ");
        hasil = SpasiSebelumTanda().Replace(hasil, "$1");

        return hasil.Trim();
    }

    /// <summary>
    /// Apakah teks masih punya penanda Markdown yang perlu dibersihkan.
    /// Dipakai uji dan diagnostik; tidak dipanggil jalur produksi.
    /// </summary>
    public static bool AdaMarkdown(string? teks) =>
        !string.IsNullOrEmpty(teks)
        && (teks.Contains('*') || teks.Contains('`')
            || teks.Contains('—') || teks.Contains('–')
            || teks.Contains('•') || teks.Contains('#'));

    /// <summary>
    /// Pecah teks menjadi kalimat lalu bersihkan masing-masing.
    ///
    /// <para>
    /// Dipakai jalur suara supaya pembersihan terjadi SETELAH pemotongan
    /// kalimat. Urutan ini disengaja: kalau dibersihkan lebih dulu, tanda
    /// bintang yang menempel di akhir kalimat (<c>*partner*.</c>) bisa membuat
    /// pemotong kalimat salah mengenali batas.
    /// </para>
    /// </summary>
    public static List<string> BersihkanPerKalimat(string? teks, int minimal = 12)
    {
        var potong = SentenceSplitter.PotongKalimat(teks, minimal);
        if (!string.IsNullOrWhiteSpace(potong.Sisa))
        {
            potong.Kalimat.Add(potong.Sisa);
        }

        var hasil = new List<string>(potong.Kalimat.Count);
        foreach (var kalimat in potong.Kalimat)
        {
            var bersih = Bersihkan(kalimat);
            if (bersih.Length != 0)
            {
                hasil.Add(bersih);
            }
        }

        return hasil;
    }
}
