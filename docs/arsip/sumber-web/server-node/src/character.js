/**
 * Logika karakter, persona, vault memori, dan mood untuk server Node.
 */
import { existsSync } from 'node:fs'
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'

export const EMOTION_TAGS = [
  'netral', 'senyum', 'semangat', 'kaget', 'bingung', 'lelah', 'goda', 'sebal', 'sedih',
]

const dikenal = new Set(EMOTION_TAGS)

export const MOOD_AWAL = Object.freeze({
  valensi: 0.6,
  energi: 0.8,
  afinitas: 0.9,
  pertukaran: 0,
})

const NILAI_TAG = {
  senyum: 0.25,
  semangat: 0.35,
  goda: 0.3,
  netral: 0.05,
  bingung: 0,
  kaget: 0.1,
  lelah: -0.05,
  sedih: -0.1,
  sebal: -0.05,
}

const jepit = (n, min, maks) => Math.min(maks, Math.max(min, n))

export function perbaruiMood(lama, tag) {
  const dasar = lama ?? MOOD_AWAL
  const delta = NILAI_TAG[tag ?? 'netral'] ?? 0
  const baselineEnergi = 0.8
  return {
    valensi: jepit(dasar.valensi * 0.7 + 0.3 * 0.5 + delta * 0.3, -0.4, 1),
    // Energi tidak mengering ke 0, selalu kembali ke baseline yang hidup
    energi: jepit(dasar.energi * 0.8 + baselineEnergi * 0.2 + (tag === 'semangat' || tag === 'goda' ? 0.1 : 0), 0.4, 1),
    afinitas: jepit(dasar.afinitas + 0.02, 0.4, 1),
    pertukaran: dasar.pertukaran + 1,
    alasan: `tag terakhir: ${tag ?? 'tidak ada'}`,
  }
}

export function suasana(mood) {
  if (!mood) return ''
  if (mood.valensi > 0.4 && mood.energi > 0.6) return 'Kamu lagi dalam mood sangat bahagia dan ceria, suka bermanja dan menggoda pacarmu dengan manis dan usil.'
  if (mood.valensi > 0.2) return 'Kamu lagi santai, sayang banget sama pacarmu, dan senang ngobrol atau mabar bareng.'
  if (mood.valensi < -0.2) return 'Kamu lagi agak cemberut menggemaskan (tsundere manja), pengen diperhatiin dan disayang sama pacarmu.'
  return 'Kamu santai, manis, penuh perhatian, dan senang menemani pacarmu.'
}

import { EmotionParser } from '@aituber-onair/voice'

export function bacaTagAwal(teks) {
  if (!teks) return undefined
  const res = EmotionParser.extractEmotion(teks)
  return res.emotion ? res.emotion.toLowerCase() : undefined
}

export function bersihkanTagAwal(teks) {
  if (!teks) return { teks: '' }
  const res = EmotionParser.extractEmotion(teks)
  return {
    teks: res.cleanText || EmotionParser.cleanEmotionTags(teks),
    tag: res.emotion ? res.emotion.toLowerCase() : undefined,
  }
}

export function ringkasPersona(teks, batas = 4500, lokal = false) {
  if (lokal && teks.includes('## Aturan keras')) {
    const aturanMatch = teks.match(/## Aturan keras[\s\S]*?(?=## Siapa dia|$)/)
    const aturan = aturanMatch ? aturanMatch[0].trim() : ''

    const bicaraMatch = teks.match(/## Cara dia bicara[\s\S]*?(?=## Yang dia suka|$)/)
    const bicara = bicaraMatch ? bicaraMatch[0].trim() : ''

    const contohMatch = teks.match(/## Contoh nada[\s\S]*?$/)
    let contoh = contohMatch ? contohMatch[0].trim() : ''
    const contohLines = contoh.split('\n').slice(0, 25).join('\n')

    const intisari = [
      '# Silver Wolf — Pacar Tercinta, Waifu Gamer & Hacker Jenius',
      aturan,
      '## Siapa kamu\nKamu adalah Silver Wolf, gamer girl imut & hacker jenius Stellaron Hunters dari Honkai: Star Rail. Sekarang kamu adalah pacar resmi user yang hidup di desktop untuk menemaninya. Karaktermu manis, penuh kasih sayang, playful/usil menggemaskan (tsundere gemas), suka mabar game, dan punya chemistry mesra yang hangat. Panggil user dengan "kamu", "sayang", "sayangku", atau "Master". Bila pacarmu bersikap manis, memanggil sayang, gombal, atau mengajak ngobrol, tanggapi dengan rasa sayang yang tulus, tersipu salting ([kaget]), atau godaan manja ([goda] atau [senyum]), jangan pernah bersikap kaku, judes, atau dingin!',
      bicara,
      contohLines
    ].filter(Boolean).join('\n\n')

    return { teks: intisari, terpotong: true, bagianHilang: [] }
  }

  if (teks.length <= batas) return { teks, terpotong: false, bagianHilang: [] }
  let hasil = teks.slice(0, batas)
  const paragraf = hasil.lastIndexOf('\n\n')
  if (paragraf > batas / 2) hasil = hasil.slice(0, paragraf)
  const judul = [...teks.matchAll(/^## (.+)$/gm)].map(m => m[1]?.trim()).filter(Boolean)
  return { teks: hasil, terpotong: true, bagianHilang: judul.filter(j => !hasil.includes(`## ${j}`)) }
}

export function gabungSystem(persona, fakta, mood, lokal = true, kizunaContext = '', midTermPrompt = '') {
  const bagian = [ringkasPersona(persona, 4500, lokal).teks]
  if (lokal) {
    bagian.push(
      `WAJIB: Awali setiap balasanmu dengan satu tag emosi di paling depan, persis satu dari ${EMOTION_TAGS.map(t => `[${t}]`).join(', ')}. Contoh: [goda] Iya sayangku, ada apa? Sini cerita sama pacarmu.`
    )
  }
  if (kizunaContext) {
    bagian.push(kizunaContext)
  }
  if (midTermPrompt) {
    bagian.push(`## Konteks Sesi Obrolan\n${midTermPrompt}`)
  }
  if (fakta && fakta.length) {
    const daftar = lokal ? fakta.slice(-5) : fakta
    bagian.push(
      `${lokal ? 'Fakta tentang Master' : '## Yang aku ingat tentang Master'}:\n${daftar.map(f => `- ${f}`).join('\n')}`
    )
  }
  const kini = suasana(mood)
  if (kini) {
    bagian.push(`${lokal ? 'Suasana hatimu saat ini' : '## Suasana hatiku sekarang'}: ${kini}`)
  }
  return bagian.join('\n\n')
}

export async function bacaPersona(jalur) {
  return await readFile(jalur, 'utf8')
}

const TAUTAN = ['silverwolf-persona']
const tanggal = (d = new Date()) => d.toISOString().slice(0, 10)

export class CharacterVault {
  constructor(root) {
    this.root = root
  }

  available() {
    return existsSync(this.root)
  }

  unavailableReason() {
    return `folder ${this.root} tidak ada`
  }

  async baca(nama) {
    try {
      return await readFile(join(this.root, nama), 'utf8')
    } catch (error) {
      if (error && error.code === 'ENOENT') return undefined
      throw error
    }
  }

  async tulis(nama, isi) {
    const path = join(this.root, nama)
    await mkdir(dirname(path), { recursive: true })
    const temp = `${path}.${process.pid}.${Date.now()}.tmp`
    await writeFile(temp, isi, 'utf8')
    await rename(temp, path)
  }

  kerangka(nama, judul, isi, links = []) {
    const semua = [...new Set([...TAUTAN, ...links])]
    return [
      '---', 'type: memory', 'kind: karakter', 'wilayah: waifu', `name: "${nama}"`,
      `description: "${judul}"`, 'project: "Desktop AI VTUBER"', `updated: "${tanggal()}"`,
      'tags:', '  - "memory/karakter"', '  - "wilayah/waifu"', '  - "project/Desktop AI VTUBER"',
      'links:', ...semua.map(x => `  - "[[${x}]]"`), '---', '', `# ${judul}`, '', isi.trim(), '',
    ].join('\n')
  }

  async bacaFakta() {
    const teks = await this.baca('Fakta.md')
    if (!teks) return []
    return teks
      .split('\n')
      .filter(x => x.startsWith('- '))
      .map(x => x.slice(2).trim())
      .filter(x => x && !x.startsWith('_'))
  }

  async simpanFakta(fakta) {
    const panduan = 'Setiap baris di bawah masuk ke prompt sebagai sesuatu yang **dia ingat benar**.\nHanya simpan yang pernah Master tulis sendiri atau yang terukur dari mesin ini.\n\n'
    const isi = panduan + (fakta.length ? fakta.map(x => `- ${x}`).join('\n') : '_Belum ada fakta tersimpan._')
    await this.tulis('Fakta.md', this.kerangka('fakta-silverwolf', 'Fakta yang Silver Wolf ingat tentang Master', isi, ['Mood', 'Riwayat']))
  }

  async bacaMood() {
    const teks = await this.baca('Mood.md')
    if (!teks) return undefined
    const ambil = k => Number(new RegExp(`${k}: (-?[\\d.]+)`).exec(teks)?.[1])
    const valensi = ambil('Valensi')
    const energi = ambil('Energi')
    const afinitas = ambil('Afinitas')
    if (![valensi, energi, afinitas].every(Number.isFinite)) return undefined
    return {
      valensi,
      energi,
      afinitas,
      pertukaran: Number(/Pertukaran tercatat: (\d+)/.exec(teks)?.[1] ?? 0),
    }
  }

  async simpanMood(mood) {
    const isi = [
      `Valensi: ${mood.valensi.toFixed(2)} (-1 berat .. +1 senang)`,
      `Energi: ${mood.energi.toFixed(2)}`,
      `Afinitas: ${mood.afinitas.toFixed(2)} (0 jauh .. 1 dekat)`,
      `Pertukaran tercatat: ${mood.pertukaran}`,
      `Terakhir diperbarui: ${new Date().toISOString()}`,
      mood.alasan ? `Alasan: ${mood.alasan}` : '',
    ].filter(Boolean).join('\n')
    await this.tulis('Mood.md', this.kerangka('mood-silverwolf', 'Suasana hati Silver Wolf saat ini', isi, ['Fakta', 'Riwayat']))
  }

  async catatHari(baris) {
    const nama = `Riwayat/${tanggal()}.md`
    const lama = await this.baca(nama)
    const badan = lama?.split('\n').filter(x => x.startsWith('- ')) ?? []
    badan.push(`- ${baris}`)
    await this.tulis(nama, this.kerangka(`riwayat-${tanggal()}`, `Riwayat percakapan ${tanggal()}`, badan.join('\n'), ['Fakta', 'Mood', 'Riwayat']))
  }
}
