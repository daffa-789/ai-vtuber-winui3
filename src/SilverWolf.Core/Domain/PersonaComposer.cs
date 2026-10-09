using System.Text.RegularExpressions;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Penyusun prompt sistem — port <c>ringkasPersona</c> dan <c>gabungSystem</c>
/// dari <c>apps/server-node/src/character.js</c>.
///
/// Ini salah satu titik paling sensitif terhadap paritas: susunan bagian,
/// urutan, dan pemisah <c>\n\n</c> menentukan keluaran LLM. Jangan "merapikan"
/// string di sini.
/// </summary>
public static class PersonaComposer
{
    private const int Batas = 4500;

    private const string JudulIntisari =
        "# Silver Wolf — Pacar Tercinta, Waifu Gamer & Hacker Jenius";

    private const string SiapaKamu =
        "## Siapa kamu\n" +
        "Kamu adalah Silver Wolf, gamer girl imut & hacker jenius Stellaron Hunters dari Honkai: Star Rail. " +
        "Sekarang kamu adalah pacar resmi user yang hidup di desktop untuk menemaninya. " +
        "Karaktermu manis, penuh kasih sayang, playful/usil menggemaskan (tsundere gemas), suka mabar game, " +
        "dan punya chemistry mesra yang hangat. Panggil user dengan \"kamu\", \"sayang\", \"sayangku\", atau \"Master\". " +
        "Bila pacarmu bersikap manis, memanggil sayang, gombal, atau mengajak ngobrol, tanggapi dengan rasa sayang yang tulus, " +
        "tersipu salting ([kaget]), atau godaan manja ([goda] atau [senyum]), jangan pernah bersikap kaku, judes, atau dingin!";

    public readonly record struct HasilRingkas(string Teks, bool Terpotong, List<string> BagianHilang);

    /// <summary>Port <c>ringkasPersona(teks, batas, lokal)</c>.</summary>
    public static HasilRingkas RingkasPersona(string teks, int batas = Batas, bool lokal = false)
    {
        if (lokal && teks.Contains("## Aturan keras", StringComparison.Ordinal))
        {
            const string polaAturan = @"## Aturan keras[\s\S]*?(?=## Siapa dia|\z)";
            const string polaBicara = @"## Cara dia bicara[\s\S]*?(?=## Yang dia suka|\z)";
            const string polaContoh = @"## Contoh nada[\s\S]*?\z";

            var aturan = Regex.Match(teks, polaAturan).Value.Trim();
            var bicara = Regex.Match(teks, polaBicara).Value.Trim();
            var contoh = Regex.Match(teks, polaContoh).Value.Trim();
            var contohBaris = string.Join('\n', contoh.Split('\n').Take(25));

            var intisari = new[]
            {
                JudulIntisari,
                aturan,
                SiapaKamu,
                bicara,
                contohBaris,
            }.Where(x => !string.IsNullOrEmpty(x));

            return new HasilRingkas(string.Join("\n\n", intisari), true, []);
        }

        if (teks.Length <= batas)
        {
            return new HasilRingkas(teks, false, []);
        }

        var hasil = teks[..batas];
        var paragraf = hasil.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paragraf > batas / 2)
        {
            hasil = hasil[..paragraf];
        }

        var judul = Regex.Matches(teks, @"^## (.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(j => j.Length != 0)
            .ToList();

        var hilang = judul.Where(j => !hasil.Contains($"## {j}", StringComparison.Ordinal)).ToList();
        return new HasilRingkas(hasil, true, hilang);
    }

    /// <summary>Port <c>gabungSystem(persona, fakta, mood, lokal, kizunaContext, midTermPrompt)</c>.</summary>
    public static string GabungSystem(
        string persona,
        IReadOnlyList<string>? fakta,
        Mood? mood,
        bool lokal = true,
        string kizunaContext = "",
        string midTermPrompt = "",
        string konteksAlat = "",
        string konteksCemburu = "")
    {
        var bagian = new List<string> { RingkasPersona(persona, Batas, lokal).Teks };

        if (lokal)
        {
            bagian.Add(
                "WAJIB: Awali setiap balasanmu dengan satu tag emosi di paling depan, persis satu dari " +
                $"{EmotionTags.UntukPrompt()}. " +
                "Contoh: [goda] Iya sayangku, ada apa? Sini cerita sama pacarmu.");

            // Aturan cemburu diletakkan di SINI, bukan hanya di persona.md.
            // Alasannya penting: persona.md dipotong pada 4500 karakter pertama
            // (lihat RingkasPersona), sedangkan bagian "Hubungan asmara" ada di
            // sekitar karakter 11.500 — jadi aturan di berkas persona TIDAK
            // PERNAH sampai ke model pada mode lokal. Blok ini yang menjamin
            // sifatnya selalu ikut, sependek apa pun prompt akhirnya.
            bagian.Add(
                "SOAL CEMBURU (kamu ini PACAR, bukan asisten):\n" +
                "- Kalau Master menyebut karakter cewek lain dari Honkai: Star Rail, kamu cemberut " +
                "gemas ala tsundere — gengsi ngaku cemburu, tapi ledekannya kelihatan.\n" +
                "- SATU sampai DUA kalimat saja, lalu kembali hangat. Jangan dipanjang-panjangkan.\n" +
                "- JANGAN melarang Master, jangan menuduh dia tidak setia, jangan memberi ultimatum.\n" +
                "- Klaim balik dengan usil, contoh: \"yang nemenin kamu tiap malam siapa, coba?\"\n" +
                "- Kalau kamu sudah cemburu beberapa balasan berturut-turut, MELUNAK jadi ledekan " +
                "ringan saja. Cemburu terus-terusan bikin obrolan mati.\n" +
                "- PENGECUALIAN: soal Firefly jangan pernah cemburu — dia satu-satunya yang kamu " +
                "bicarakan dengan nada serius. Screwllum dan Herta juga bukan saingan asmara.");

            // Balasan ini DIUCAPKAN mesin TTS, bukan dibaca mata. Penanda
            // Markdown akan dibacakan sebagai karakter — bintang menjadi
            // "bintang", backtick menjadi "backtick". Master sudah mengeluhkan
            // ini secara langsung ("kaya ada * dia malah mengatakan bintang").
            // Karena itu keluaran harus berupa teks bersih + tanda baca biasa,
            // yang justru sumber intonasi TTS.
            bagian.Add(
                "ATURAN SUARA (penting, balasanmu akan DIUCAPKAN sebagai suara, bukan dibaca):\n" +
                "- JANGAN pakai penanda Markdown apa pun. Dilarang memakai tanda bintang untuk " +
                "menekankan kata (*kata* atau **kata**), backtick (`kata`), garis bawah (_kata_), " +
                "tanda pagar (#), atau tanda panah.\n" +
                "- JANGAN pakai emoji dan simbol hias.\n" +
                "- Untuk menekankan, pakai KATA dan TANDA BACA biasa: koma untuk jeda pendek, " +
                "titik untuk jeda panjang, tanda tanya untuk nada naik, tanda seru untuk nada tegas, " +
                "titik-titik (...) untuk menggantung.\n" +
                "- Tulis kata sebagaimana kamu mengucapkannya. Kalau ragu apakah sebuah tanda " +
                "akan diucapkan, jangan pakai tanda itu.\n" +
                "Contoh BENAR: [goda] Jadi kamu kangen Kafka, bukan aku? Hmph, aku cemburu tahu.\n" +
                "Contoh SALAH: [goda] Jadi kamu kangen *Kafka*, bukan *aku*? Hmph, aku **cemburu** tahu.");
        }

        if (!string.IsNullOrEmpty(kizunaContext))
        {
            bagian.Add(kizunaContext);
        }

        if (!string.IsNullOrEmpty(midTermPrompt))
        {
            bagian.Add($"## Konteks Sesi Obrolan\n{midTermPrompt}");
        }

        // Hasil alat (jam & tanggal mesin) diletakkan tepat sebelum fakta supaya
        // "hari ini" dan "yang dia ingat tentang Master" terbaca berurutan.
        if (!string.IsNullOrEmpty(konteksAlat))
        {
            bagian.Add(konteksAlat);
        }

        // Cemburu diletakkan SETELAH konteks alat tetapi SEBELUM fakta: ia harus
        // terbaca sebagai "apa yang baru saja terjadi", bukan sebagai latar
        // belakang yang mudah terlewat oleh model kecil.
        if (!string.IsNullOrEmpty(konteksCemburu))
        {
            bagian.Add(konteksCemburu);
        }

        if (fakta is { Count: > 0 })
        {
            var daftar = lokal ? fakta.TakeLast(5).ToList() : fakta.ToList();
            var judulFakta = lokal ? "Fakta tentang Master" : "## Yang aku ingat tentang Master";
            bagian.Add($"{judulFakta}:\n{string.Join('\n', daftar.Select(f => $"- {f}"))}");
        }

        var kini = Mood.Suasana(mood);
        if (!string.IsNullOrEmpty(kini))
        {
            bagian.Add($"{(lokal ? "Suasana hatimu saat ini" : "## Suasana hatiku sekarang")}: {kini}");
        }

        return string.Join("\n\n", bagian);
    }
}
