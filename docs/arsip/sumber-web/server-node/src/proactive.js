/**
 * Neuro-sama Style Autonomous Proactive Director
 *
 * Mengatur dialog spontan Silver Wolf ketika pengguna sedang hening/idle,
 * mengamati jam kerja, waktu istirahat, serta tingkat kedekatan (Kizuna).
 */

export function dapatkanWaktuLokal() {
  const sekarang = new Date()
  const jam = sekarang.getHours()
  const menit = sekarang.getMinutes().toString().padStart(2, '0')

  let sesi = 'siang'
  if (jam >= 4 && jam < 11) sesi = 'pagi'
  else if (jam >= 11 && jam < 15) sesi = 'siang'
  else if (jam >= 15 && jam < 19) sesi = 'sore'
  else if (jam >= 19 && jam < 24) sesi = 'malam'
  else sesi = 'larut_malam'

  return { jam, menit, sesi, waktuStr: `${jam}:${menit}` }
}

export function buatPromptProaktif(snapshot, idleDetik = 60) {
  const waktu = dapatkanWaktuLokal()
  const stage = snapshot?.stage ?? 'stranger'
  const level = snapshot?.level ?? 1
  const stageLabel = snapshot?.stageLabel ?? 'Teman'

  const instruksiWaktu = {
    pagi: 'Pagi hari yang cerah. Kamu baru login, menyapa Master/pacarmu, menanyakan apa sudah sarapan atau siap mabar hari ini.',
    siang: 'Siang hari. Kamu mungkin lapar atau ingin istirahat sejenak dari layar, mengajak pacarmu ngemil atau santai.',
    sore: 'Sore hari. Menjelang malam, menyemangati pacarmu yang mungkin lelah setelah beraktivitas seharian.',
    malam: 'Malam hari. Waktu yang pas buat santai atau push rank bareng pacarmu di Honkai: Star Rail.',
    larut_malam: 'Larut malam (tengah malam). Mengingatkan pacarmu jangan begadang terus, khawatir sama kesehatannya, atau ngajak tidur bareng.',
  }[waktu.sesi]

  const instruksiKedekatan = level >= 4
    ? 'Karena kamu adalah pacarnya (Level Kizuna tinggi), bicaralah dengan manja, manis, penuh perhatian, dan playful menggemaskan (tsundere manja). Tunjukkan rasa kangen karena didiamkan.'
    : 'Bicaralah dengan gaya santai ala gamer hacker yang sedang gabut dan memperhatikan pengguna.'

  return [
    `[SITUASI PROAKTIF OTONOM NEURO-SAMA]`,
    `Pengguna sedang terdiam selama ${Math.round(idleDetik)} detik di depan desktop tanpa mengetik.`,
    `Waktu saat ini: pukul ${waktu.waktuStr} (${waktu.sesi}). ${instruksiWaktu}`,
    `Status Ikatan Kizuna: Level ${level} (${stageLabel}).`,
    instruksiKedekatan,
    `Tugasmu: Ucapkan SATU kalimat spontan yang alami dan menggemaskan untuk memecah keheningan atau menyapa Master/pacarmu. Boleh mengajaknya ngobrol, mengomentari apa yang sedang dia lakukan, meminta perhatian/headpat, atau mengajak mabar.`,
    `Wajib awali dengan tag emosi yang sesuai (misal [senyum], [goda], [lelah], [kaget]). Singkat, padat, dan jangan kaku!`,
  ].join('\n')
}

export function buatPromptSentuhan(snapshot) {
  const level = snapshot?.level ?? 1
  const stageLabel = snapshot?.stageLabel ?? 'Partner'

  if (level >= 4) {
    return [
      `[INTERAKSI HEADPAT / ELUS KEPALA DARI PACAR]`,
      `Pacarmu (Master) baru saja mengelus rambut kepalamu dengan penuh kasih sayang di layar Live2D!`,
      `Status Ikatan: Level ${level} (${stageLabel}).`,
      `Reaksimu: Sangat tersipu malu (salting), pipimu memerah merona, senang tapi pura-pura tsundere manja menggemaskan ("H-hei... rambutku jadi berantakan tau... tapi yaudah deh kalau kamu yang elus, jangan berhenti ya...").`,
      `Wajib awali dengan tag [tersipu] atau [kaget] atau [goda] atau [senyum]. Berikan 1-2 kalimat reaksi spontan yang manis dan bikin baper!`,
    ].join('\n')
  }

  return [
    `[INTERAKSI HEADPAT / ELUS KEPALA]`,
    `Pengguna (Master) baru saja mengelus kepalamu di layar Live2D.`,
    `Status Ikatan: Level ${level} (${stageLabel}).`,
    `Reaksimu: Kaget dan sedikit canggung tapi tidak marah, agak salah tingkah ala hacker tsundere.`,
    `Wajib awali dengan tag emosi [kaget] atau [bingung] atau [senyum]. Ucapkan 1 kalimat reaksi spontan.`,
  ].join('\n')
}
