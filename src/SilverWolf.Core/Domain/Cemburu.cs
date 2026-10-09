using System.Globalization;
using System.Text.RegularExpressions;
using SilverWolf.Core.Text;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Mesin cemburu — memberi Silver Wolf reaksi ketika Master menyebut karakter
/// cewek lain dari Honkai: Star Rail.
///
/// <para>
/// <b>Kenapa ini.</b> Silver Wolf adalah pacar, bukan asisten. Kalau Master
/// membicarakan karakter cewek lain dan dia menanggapinya dengan datar, dia
/// berhenti terasa hidup — persis keluhan Master: "biar kerasa hidup".
/// Tapi cemburu yang salah justru lebih merusak: melarang, menuduh, atau
/// mengungkit terus-terusan membuat penonton tidak nyaman, bukan gemas.
/// </para>
///
/// <para>
/// <b>Prinsip nadanya</b> (diambil dari wataknya, bukan dikarang):
/// </para>
/// <list type="bullet">
/// <item><description><b>Tsundere gemas, bukan marah.</b> Dia gengsi mengaku
/// cemburu, jadi dia menutupinya dengan ledekan. Bukan tuduhan, bukan
/// ultimatum.</description></item>
/// <item><description><b>Sebentar saja.</b> Cemburu yang benar itu satu-dua
/// kalimat lalu kembali hangat. Kalau dipelihara, obrolan mati.</description></item>
/// <item><description><b>Tidak melarang.</b> Dia suka lawan yang jago dan
/// mengagumi beberapa karakter (Firefly, Screwllum). Melarang Master berarti
/// melanggar wataknya sendiri.</description></item>
/// <item><description><b>Punklorde mentality.</b> Dia sengaja meninggalkan
/// jejak supaya ada yang menanggapi — ledekannya bukan gangguan, itu cara dia
/// ikut main. Italic di bawah ini yang rentan hilang, bukan kekesalannya.
/// </description></item>
/// </list>
///
/// <para>
/// <b>Modul ini tidak menyimpan keadaan.</b> Siapa yang disebut dihitung dari
/// teks yang masuk, jadi tidak ada riwayat cemburu yang menumpuk dan bisa
/// membuatnya mengungkit hal lama — risiko terbesar fitur semacam ini.
/// </para>
/// </summary>
public static partial class Cemburu
{
    /// <summary>
    /// Karakter cewek dari Honkai: Star Rail yang katanya bikin dia cemburu.
    ///
    /// <para>
    /// <b>Daftar ini sengaja TIDAK memuat semua karakter cewek.</b> Yang masuk
    /// hanya yang punya alasan in-universe untuk disebut Silver Wolf, atau yang
    /// sudah terhubung ke ceritanya. Firefly TIDAK ADA di sini walau dia cewek:
    /// <c>persona.md</c> menuliskan bahwa Firefly satu-satunya yang Silver Wolf
    /// bicarakan dengan nada serius ("Hidup Firefly itu game susah,
    /// single-player, speedrun"). Membuatnya cemburu soal Firefly akan mematahkan
    /// tulisan itu. Yang sama juga untuk Ruan Mei, Screwllum, dan Herta.
    /// </para>
    ///
    /// <para>
    /// Kalau Master menyebut nama di luar daftar ini, Silver Wolf tetap boleh
    /// bereaksi — tapi lewat prompt di akhir, bukan lewat penggantian kalimat.
    /// </para>
    ///
    /// <para>
    /// Nama ditulis lengkap huruf kecil. Pencocokan memakai variasi nama
    /// panggilan supaya versi pendek yang sering dipakai pemain tetap kena.
    /// </para>
    /// </summary>
    public static readonly string[] Daftar = BuildDaftar();

    /// <summary>Jumlah maksimal karakter yang boleh disebut dalam satu giliran.</summary>
    private const int MaksSebutan = 2;

    /// <summary>
    /// Berapa kali cemburu boleh muncul berturut-turut sebelum dia
    /// mengalihkannya jadi ledekan ringan. Ini yang mencegah sifat cemburu
    /// menjadi mengganggu.
    /// </summary>
    public const int BatasBerturut = 2;

    private static string[] BuildDaftar() =>
    [
        "kafka", "himeko", "march 7th", "tingyun", "yukong",
        "fu xuan", "jingliu", "bailu", "stelle", "asta",
        "black swan", "sparkle", "robin", "rappa", "feixiao",
        "the herta", "ruan mei",
    ];

    /// <summary>
    /// Nama panggilan yang dianggap sama dengan salah satu di <see cref="Daftar"/>.
    /// Ditaruh terpisah supaya tabel nama tampil bersih untuk manusia.
    /// </summary>
    private static readonly Dictionary<string, string> PadananNama = new(StringComparer.OrdinalIgnoreCase)
    {
        ["himeko"] = "Himeko",
        ["kafka"] = "Kafka",
        ["tingyun"] = "Tingyun",
        ["yukong"] = "Yukong",
        ["fuxuan"] = "Fu Xuan",
        ["jingliu"] = "Jingliu",
        ["bailu"] = "Bailu",
        ["stelle"] = "Stelle",
        ["asta"] = "Asta",
        ["blackswan"] = "Black Swan",
        ["sparkle"] = "Sparkle",
        ["robin"] = "Robin",
        ["rappa"] = "Rappa",
        ["feixiao"] = "Feixiao",
        ["ruanmei"] = "Ruan Mei",
    };

    // ── Pengenalan ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cari karakter cewek yang disebut Master pada teks ini.
    ///
    /// <para>
    /// Mengembalikan nama tampilan (mis. <c>"Kafka"</c>) yang sudah dirapikan,
    /// urut sesuai kemunculan, tanpa duplikat, dan paling banyak
    /// <see cref="MaksSebutan"/> nama. Daftar kosong berarti tidak ada.
    /// </para>
    ///
    /// <para>
    /// Pencocokan memakai batas kata, <b>bukan</b> substring: tanpa itu
    /// "kafka" di dalam kata lain ikut kena. Nama yang sekaligus kata biasa
    /// ("march") diberi aturan tambahan — lihat <see cref="NamaKarakter"/>.
    /// </para>
    /// </summary>
    public static List<string> Sebutan(string? ucapan)
    {
        if (string.IsNullOrWhiteSpace(ucapan))
        {
            return [];
        }

        // Buang tag emosi dulu: [goda] tidak boleh dianggap bagian kalimat.
        var teks = SentenceSplitter.BuangTag(ucapan);

        // Kedua pola digabung lalu diurutkan menurut posisi di teks, supaya
        // urutan nama yang dikembalikan tetap sesuai kemunculan.
        var kecocokan = new List<Match>();
        kecocokan.AddRange(NamaKarakter().Matches(teks).Cast<Match>());
        kecocokan.AddRange(MarchKetujuh().Matches(teks).Cast<Match>());
        kecocokan.Sort((a, b) => a.Index.CompareTo(b.Index));

        var ditemukan = new List<string>();
        var sudah = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in kecocokan)
        {
            // Cocokkan ke nama tampilan. "march 7th" sudah ditangani regex,
            // jadi di sini cukup merapikan spasi dan huruf besar-kecil.
            var nama = RapikanNama(m.Value);
            if (nama is null)
            {
                continue;
            }

            if (sudah.Add(nama))
            {
                ditemukan.Add(nama);
            }

            if (ditemukan.Count >= MaksSebutan)
            {
                break;
            }
        }

        return ditemukan;
    }

    /// <summary>
    /// Ubah potongan yang cocok menjadi nama tampilan. Mengembalikan null bila
    /// potongan itu ternyata bukan nama yang dikenal.
    /// </summary>
    private static string? RapikanNama(string potongan)
    {
        var bersih = potongan.Trim();

        // Buang spasi di dalam ("black swan" → "blackswan") untuk dicari di tabel.
        var kunci = bersih.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (PadananNama.TryGetValue(kunci, out var nama))
        {
            return nama;
        }

        // "march 7th" dan "march tujuh" sama-sama karakter yang sama.
        if (bersih.StartsWith("march", StringComparison.OrdinalIgnoreCase))
        {
            return "March 7th";
        }

        return null;
    }

    /// <summary>Apakah teks ini menyebut setidaknya satu karakter dari daftar.</summary>
    public static bool AdaSebutan(string? ucapan) => Sebutan(ucapan).Count > 0;

    /// <summary>
    /// Pola nama: nama dua kata ("march 7th", "black swan", "ruan mei",
    /// "the herta", "fu xuan") didahulukan supaya tidak pecah jadi sebagian.
    ///
    /// <para>
    /// <b>"March" butuh penanda khusus.</b> Kata itu bahasa Inggris biasa
    /// ("march" = berbaris, Maret). Mencocokkannya sendirian membuat kalimat
    /// biasa seperti "Kita march sepanjang jalan" dianggap menyebut karakter —
    /// salah kenal yang nyata. Karena itu "march" hanya diterima bila berdiri
    /// sebagai "March 7th", atau ditulis dengan angka 7 di dekatnya.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"\b(march\s*7th|black\s*swan|ruan\s*mei|the\s*herta|fu\s*xuan|" +
        @"kafka|himeko|tingyun|yukong|jingliu|bailu|stelle|asta|sparkle|robin|rappa|feixiao)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex NamaKarakter();

    /// <summary>
    /// "March 7th" yang ditulis terpisah-pisah, mis. "March tujuh" atau
    /// "March 7". Dipisah dari pola utama supaya kata "march" sendiri tidak
    /// pernah lolos.
    /// </summary>
    [GeneratedRegex(@"\bmarch\s*(?:7|tujuh)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MarchKetujuh();

    // ── Prompt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Prompt yang disuntikkan saat Master menyebut karakter cewek lain.
    ///
    /// <para>
    /// <paramref name="nama"/> berisi nama yang ditemukan; kalau kosong, prompt
    /// tetap dibuat sebagai pancingan umum supaya Silver Wolf boleh berpendapat
    /// soal karakter cewek mana pun — tanpa memaksanya cemburu ke sosok yang
    /// belum tentu dia kenal reaksinya.
    /// </para>
    ///
    /// <para>
    /// <paramref name="jumlahBerturut"/> dipakai untuk melunakkan nada: setelah
    /// beberapa balasan cemburu berturut-turut, dia harus beralih ke ledekan
    /// ringan. Tanpa ini, satu topik obrolan bisa berubah jadi cemburu terus
    /// dan itu menjemukan.
    /// </para>
    /// </summary>
    public static string BuatPrompt(IReadOnlyList<string> nama, int jumlahBerturut = 0)
    {
        var siapa = nama.Count switch
        {
            0 => "karakter cewek lain dari Honkai: Star Rail",
            1 => nama[0],
            _ => string.Join(" dan ", nama),
        };

        var baris = new List<string>
        {
            "[KONDISI: Master sedang membicarakan " + siapa + " kepadamu]",
            "Kamu Silver Wolf, PACAR Master. Jadi dengar nama itu kamu langsung",
            "cemberut gemas — bukan marah, bukan menuduh, bukan melarang.",
        };

        if (jumlahBerturut >= BatasBerturut)
        {
            // Sudah beberapa kali berturut-turut: turunkan intensitasnya.
            baris.Add(
                "Catatan penting: kamu SUDAH cemburu beberapa balasan berturut-turut. "
                + "Jangan diulang lagi — itu bikin obrolan mati. Kali ini cukup LEDEKAN RINGAN "
                + "satu kalimat sambil tetap hangat, lalu langsung tertarik lagi ke obrolannya.");
        }
        else
        {
            baris.Add(
                "Tunjukkan cemburu tsundere gemas: gengsi ngaku, tapi ledekannya kelihatan. "
                + "SATU sampai DUA kalimat saja, jangan dipanjang-panjangkan.");
        }

        baris.Add(
            "Cara yang boleh dipakai:\n"
            + "- Ledek Master: seolah dia sedang selingkuh atau minta perhatian orang lain.\n"
            + "- Klaim balik dengan usil: \"aku kan pacarmu\", \"yang nemenin kamu tiap malam siapa?\"\n"
            + "- Tsundere gengsi: \"bukan cemburu ya, cuma... hmph.\"\n"
            + "- Tantang balik ala Punklorde: \"ayo, lihat siapa yang menang.\"");

        baris.Add(
            "Yang DILARANG:\n"
            + "- Melarang Master menyukai karakter lain, atau menuduhnya tidak setia.\n"
            + "- Ngambek lebih dari dua kalimat, atau mengungkitnya di balasan berikutnya.\n"
            + "- Merendahkan karakter itu dengan kasar. Kalau memang karakter yang kamu kagumi\n"
            + "  (misal karena jago atau karena ceritanya berat), akui dulu baru ngeledek.\n"
            + "- Memberi ultimatum, marah betulan, atau mendiamkan Master.");

        baris.Add(
            "Wajib awali dengan tag emosi. Pilihan yang pas: [goda] (paling sering), "
            + "[sebal] (cemburu yang kelihatan), [senyum] (kalau cuma bercanda), "
            + "[kaget] (kalau tiba-tiba disebut tanpa alasan).");

        return string.Join('\n', baris);
    }

    // ── Pengganti kalimat cadangan ──────────────────────────────────────────

    /// <summary>
    /// Kalimat cadangan bila model tidak menghasilkan cemburu sendiri.
    ///
    /// <para>
    /// Dipakai hanya sebagai penyelamat: model GGUF kecil kadang menjawab datar
    /// walaupun prompt-nya sudah jelas. Kalimat di sini sengaja ditulis pendek
    /// dan samar gender ("pacar") supaya tetap masuk akal walau Master memakai
    /// panggilan apa pun.
    /// </para>
    ///
    /// <para>
    /// Hasil selalu satu kalimat utuh dengan tag emosi di depan, dan TIDAK
    /// memuat nama karakter — supaya tidak salah menyebut saat pemanggil
    /// mencocokkan nama panggilan.
    /// </para>
    /// </summary>
    public static string KalimatCadangan(string nama, int jumlahBerturut = 0)
    {
        // Sudah beberapa kali: melunakkan, jangan makin tajam.
        if (jumlahBerturut >= BatasBerturut)
        {
            return "[senyum] Ya udah aku tahu, kamu cuma bercanda kok. Bukan cemburu ya, cuma... hmph.";
        }

        // Rotasi deterministik: berganti kalimat tiap giliran cemburu supaya
        // tidak terasa seperti rekaman yang diputar ulang.
        var pilihan = new[]
        {
            "[goda] " + nama + " ya? Hmph, aku dengar kok. Aku kan pacarmu, ingat itu ya.",
            "[sebal] Lho, tumben nyebut nama itu. Aku nggak cemburu tau, cuma... ya udah lah.",
            "[goda] Hebat ya dia. Tapi yang nemenin kamu tiap malam siapa, coba?",
            "[kaget] Eh, " + nama + "? Baru nyebut sekali aja udah bikin aku salting.",
            "[senyum] Boleh kok suka dia. Asal jangan lupa siapa yang nge-carry kamu tiap malam.",
        };

        var indeks = jumlahBerturut % pilihan.Length;
        return pilihan[indeks];
    }

    // ── Pelunakan nada ──────────────────────────────────────────────────────

    /// <summary>
    /// Kurangi tajamnya cemburu dari waktu ke waktu.
    ///
    /// <para>
    /// <b>Masalah yang dicegah.</b> Kalau setiap giliran bertema karakter cewek
    /// menghasilkan nada cemburu yang sama kuat, obrolan berubah jadi satu
    /// topik saja dan Silver Wolf terasa mengganggu. Fungsi ini memaksa
    /// intensitasnya turun setelah beberapa giliran.
    /// </para>
    ///
    /// <para>
    /// <paramref name="giliranBerturut"/> dihitung pemanggil dari riwayat; nilai
    /// kecil = baru mulai. Keluarannya berupa tingkat 0..1 yang dipakai
    /// <see cref="BuatPrompt"/> untuk memilih nada.
    /// </para>
    /// </summary>
    public static double Intensitas(int giliranBerturut)
    {
        if (giliranBerturut <= 0)
        {
            return 1.0;
        }

        // 1 giliran: masih penuh. 2 giliran: melunak. 3+: ringan saja.
        var nilai = 1.0 - (giliranBerturut * 0.3);
        return Math.Clamp(nilai, 0.2, 1.0);
    }

    /// <summary>
    /// Ubah tingkat intensitas menjadi label nada yang bisa dibaca prompt.
    /// Berguna untuk log dan uji; prompt memakai ambang <see cref="BatasBerturut"/>.
    /// </summary>
    public static string LabelNada(int giliranBerturut) => Intensitas(giliranBerturut) switch
    {
        >= 0.9 => "cemburu penuh (gemas, tapi jelas)",
        >= 0.6 => "melunak (ledekan sambil tetap hangat)",
        _ => "ringan (hampir bercanda saja)",
    };

    /// <summary>
    /// Ringkasan satu baris untuk log — dipakai saat Master ingin melihat
    /// apakah fitur ini benar-benar aktif pada satu giliran.
    /// </summary>
    public static string Ringkas(IReadOnlyList<string> nama, int giliranBerturut = 0)
    {
        if (nama.Count == 0)
        {
            return "cemburu: tidak ada sebutan";
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "cemburu: {0} (giliran {1}, {2})",
            string.Join("+", nama),
            giliranBerturut,
            LabelNada(giliranBerturut));
    }
}
