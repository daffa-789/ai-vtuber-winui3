/**
 * Uji kepatuhan tag wajah + kualitas bahasa MiniCPM5-2B dengan persona asli.
 *
 * Alasan: balasan uji sebelumnya menaruh tag di TENGAH kalimat
 * ("Wajib [senyum]."), sehingga tag itu ikut dibacakan. Uji ini mengukur
 * seberapa sering tag keluar di awal (kontrak persona) dan berapa lama
 * kalimat pertama siap.
 */
import { readFile } from 'node:fs/promises'

const PORT = Number(process.env.PORT ?? 8081)
const TANYA = [
  'Jelaskan kenapa langit berwarna biru, singkat aja.',
  'Apa bedanya RAM dan ROM?',
  'Kasih gw tiga tips biar laptop nggak cepat panas.',
  'Repo ini pakai framework apa?',
  'Ada bug di build, dari mana gw mulai ngecek?',
  'Bikin gw satu regex buat validasi email.',
  'Menurut lu, refactor dulu atau tambah fitur dulu?',
  'Ringkas: apa itu aether editing?',
]

const persona = await readFile('silver_wolf_memory/persona.md', 'utf8')
const sistem =
  persona +
  '\n\n---\nATURAN OUTPUT (wajib): balasan HARUS diawali satu tag wajah ' +
  'dalam kurung siku, contoh [senyum], lalu spasi, baru kalimatnya. ' +
  'Jangan pernah menaruh tag di tengah atau akhir kalimat.'

const TAG_AWAL = /^[\s`'"]*\[([a-zA-Z][^\n[\]{}]{0,25})\]/
const PENUTUP = '.!?…'
const EKOR = '"\'”’)]}»'

function kalimatPertamaSelesai(teks) {
  for (let i = 0; i < teks.length; i++) {
    if (!PENUTUP.includes(teks[i])) continue
    if (/\d/.test(teks[i - 1] ?? '') && (teks[i + 1] === undefined || /\d/.test(teks[i + 1]))) continue
    let j = i + 1
    while (j < teks.length && EKOR.includes(teks[j])) j++
    const berikut = teks[j]
    if (berikut !== undefined && !/[\s\n]/.test(berikut)) continue
    const potongan = teks.slice(0, j).trim()
    if (potongan.length >= 12 || /\s/.test(potongan)) return { pada: j, potongan }
  }
  return null
}

async function tanya(q) {
  const mulai = performance.now()
  const r = await fetch(`http://127.0.0.1:${PORT}/v1/chat/completions`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      model: 'MiniCPM5-2B',
      stream: true,
      temperature: 0.7,
      min_p: 0,
      top_p: 0.95,
      messages: [
        { role: 'system', content: sistem },
        { role: 'user', content: q },
      ],
    }),
  })
  if (!r.ok) throw new Error(`HTTP ${r.status}: ${await r.text()}`)
  const reader = r.body.getReader()
  const dec = new TextDecoder()
  let buf = '', teks = '', pertama = null
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buf += dec.decode(value, { stream: true })
    const baris = buf.split('\n'); buf = baris.pop() ?? ''
    for (const b of baris) {
      const s = b.replace(/^data:\s*/, '').trim()
      if (!s || s === '[DONE]') continue
      try {
        const d = JSON.parse(s).choices?.[0]?.delta?.content
        if (!d) continue
        teks += d
        if (!pertama) { const h = kalimatPertamaSelesai(teks); if (h) pertama = { detik: (performance.now() - mulai) / 1000, ...h } }
      } catch {}
    }
  }
  const selesai = (performance.now() - mulai) / 1000
  const awaI = TAG_AWAL.exec(teks.trim())?.[1]?.toLowerCase() ?? null
  // Tag muncul di mana saja?
  const semua = [...teks.matchAll(/\[([a-zA-Z]{3,12})\]/g)].map(m => m[1].toLowerCase())
  const tengah = semua.length > 0 && awaI !== semua[0]
  return { q, teks: teks.trim(), awaI, semua, tengah, pertama, selesai }
}

const hasil = []
for (const q of TANYA) { const h = await tanya(q); hasil.push(h); console.log(`${h.awaI ? 'OK ' : 'MISS'} ${h.tengah ? 'TENGAH!' : '      '} ${h.q}`) }

console.log('\n=== RINGKASAN ===')
const awalOk = hasil.filter(h => h.awal).length
console.log(`tag di AWAL   : ${awalOk}/${hasil.length}`)
console.log(`tag di TENGAH : ${hasil.filter(h => h.tengah).length}/${hasil.length}`)
const p = hasil.filter(h => h.pertama)
console.log(`kalimat-1 rata: ${(p.reduce((a, h) => a + h.pertama.detik, 0) / p.length).toFixed(2)} dtk`)
console.log(`selesai   rata: ${(p.reduce((a, h) => a + h.selesai, 0) / p.length).toFixed(2)} dtk`)

console.log('\n=== CONTOH BALASAN ===')
for (const h of hasil.slice(0, 4)) console.log(`\n[${h.q}]\n${h.teks.slice(0, 220)}`)
