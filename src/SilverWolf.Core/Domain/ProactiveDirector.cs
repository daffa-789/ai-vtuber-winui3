using System.Globalization;
using SilverWolf.Core.Domain.Kizuna;

namespace SilverWolf.Core.Domain;

/// <summary>
/// Sutradara dialog proaktif gaya Neuro-sama — port
/// <c>apps/server-node/src/proactive.js</c>.
///
/// Menghasilkan prompt untuk dua situasi: pengguna sedang hening (proaktif) dan
/// pengguna baru saja mengelus kepala (sentuhan). Semua kalimat instruksinya
/// harus tetap persis: ini yang membuat karakter terdengar sama.
/// </summary>
public static class ProactiveDirector
{
    /// <summary>Port <c>dapatkanWaktuLokal()</c>.</summary>
    public static (int Jam, string Menit, string Sesi, string WaktuStr) DapatkanWaktuLokal(DateTime? lokal = null)
    {
        var sekarang = lokal ?? DateTime.Now;
        var jam = sekarang.Hour;
        var menit = sekarang.Minute.ToString("00", CultureInfo.InvariantCulture);

        var sesi = jam switch
        {
            >= 4 and < 11 => "pagi",
            >= 11 and < 15 => "siang",
            >= 15 and < 19 => "sore",
            >= 19 and < 24 => "malam",
            _ => "larut_malam",
        };

        return (jam, menit, sesi, $"{jam}:{menit}");
    }

    /// <summary>Port <c>buatPromptProaktif(snapshot, idleDetik)</c>.</summary>
    public static string BuatPromptProaktif(BondSnapshot? snapshot, double idleDetik = 60, DateTime? lokal = null)
    {
        var waktu = DapatkanWaktuLokal(lokal);
        var stage = snapshot?.Stage ?? "stranger";
        var level = snapshot?.Level ?? 1;
        var stageLabel = snapshot?.StageLabel ?? "Teman";
        _ = stage;

        var instruksiWaktu = waktu.Sesi switch
        {
            "pagi" => "Pagi hari yang cerah. Kamu baru login, menyapa Master/pacarmu, menanyakan apa sudah sarapan atau siap mabar hari ini.",
            "siang" => "Siang hari. Kamu mungkin lapar atau ingin istirahat sejenak dari layar, mengajak pacarmu ngemil atau santai.",
            "sore" => "Sore hari. Menjelang malam, menyemangati pacarmu yang mungkin lelah setelah beraktivitas seharian.",
            "malam" => "Malam hari. Waktu yang pas buat santai atau push rank bareng pacarmu di Honkai: Star Rail.",
            _ => "Larut malam (tengah malam). Mengingatkan pacarmu jangan begadang terus, khawatir sama kesehatannya, atau ngajak tidur bareng.",
        };

        var instruksiKedekatan = level >= 4
            ? "Karena kamu adalah pacarnya (Level Kizuna tinggi), bicaralah dengan manja, manis, penuh perhatian, dan playful menggemaskan (tsundere manja). Tunjukkan rasa kangen karena didiamkan."
            : "Bicaralah dengan gaya santai ala gamer hacker yang sedang gabut dan memperhatikan pengguna.";

        return string.Join('\n',
            "[SITUASI PROAKTIF OTONOM NEURO-SAMA]",
            $"Pengguna sedang terdiam selama {Math.Round(idleDetik, MidpointRounding.AwayFromZero)} detik di depan desktop tanpa mengetik.",
            $"Waktu saat ini: pukul {waktu.WaktuStr} ({waktu.Sesi}). {instruksiWaktu}",
            $"Status Ikatan Kizuna: Level {level} ({stageLabel}).",
            instruksiKedekatan,
            "Tugasmu: Ucapkan SATU kalimat spontan yang alami dan menggemaskan untuk memecah keheningan atau menyapa Master/pacarmu. Boleh mengajaknya ngobrol, mengomentari apa yang sedang dia lakukan, meminta perhatian/headpat, atau mengajak mabar.",
            "Wajib awali dengan tag emosi yang sesuai (misal [senyum], [goda], [lelah], [kaget]). Singkat, padat, dan jangan kaku!");
    }

    /// <summary>Port <c>buatPromptSentuhan(snapshot)</c>.</summary>
    public static string BuatPromptSentuhan(BondSnapshot? snapshot)
    {
        var level = snapshot?.Level ?? 1;
        var stageLabel = snapshot?.StageLabel ?? "Partner";

        if (level >= 4)
        {
            return string.Join('\n',
                "[INTERAKSI HEADPAT / ELUS KEPALA DARI PACAR]",
                "Pacarmu (Master) baru saja mengelus rambut kepalamu dengan penuh kasih sayang di layar Live2D!",
                $"Status Ikatan: Level {level} ({stageLabel}).",
                "Reaksimu: Sangat tersipu malu (salting), pipimu memerah merona, senang tapi pura-pura tsundere manja menggemaskan (\"H-hei... rambutku jadi berantakan tau... tapi yaudah deh kalau kamu yang elus, jangan berhenti ya...\").",
                "Wajib awali dengan tag [tersipu] atau [kaget] atau [goda] atau [senyum]. Berikan 1-2 kalimat reaksi spontan yang manis dan bikin baper!");
        }

        return string.Join('\n',
            "[INTERAKSI HEADPAT / ELUS KEPALA]",
            "Pengguna (Master) baru saja mengelus kepalamu di layar Live2D.",
            $"Status Ikatan: Level {level} ({stageLabel}).",
            "Reaksimu: Kaget dan sedikit canggung tapi tidak marah, agak salah tingkah ala hacker tsundere.",
            "Wajib awali dengan tag emosi [kaget] atau [bingung] atau [senyum]. Ucapkan 1 kalimat reaksi spontan.");
    }
}
