/**
 * Ukur kapan kalimat PERTAMA siap diucapkan (P0: TTS per-kalimat).
 *
 * Dulu: tunggu seluruh balasan -> baru sintesis. Yang diukur di sini adalah
 * selisih antara "kalimat pertama utuh" dan "aliran selesai". Selisih itulah
 * latency yang dihemat per balasan.
 */
const PORT = Number(process.env.PORT ?? 8081)
const URL_CHAT = `http://127.0.0.1:${PORT}/v1/chat/completions`

const PERSONA =
  'Kamu adalah Silver Wolf, hacker Punklorde dari Stellaron Hunters. ' +
  'Kamu memanggil pengguna Master, bicara santai, ringkas, tidak memakai emoji. ' +
  'WAJIB awali setiap balasan dengan satu tag emosi dalam kurung siku, misalnya [senyum].'

const PERTANYAAN = [
  'Jelaskan kenapa langit berwarna biru, singkat aja.',
  'Apa bedanya RAM dan ROM?',
  'Kasih aku tiga tips biar laptop nggak cepat panas.',
]

const PENUTUP = '.!?…'
const EKOR = '"\'”’)]}»'

/** Salinan kecil dari potongKalimat: kapan kalimat pertama selesai? */
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

async function jalankan(pertanyaan) {
  const mulai = performance.now()
  const response = await fetch(URL_CHAT, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      model: 'MiniCPM5-2B',
      stream: true,
      temperature: 0.7,
      messages: [
        { role: 'system', content: PERSONA },
        { role: 'user', content: pertanyaan },
      ],
    }),
  })
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${await response.text()}`)

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let teks = ''
  let pertama = null
  let token = 0

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const baris = buffer.split('\n')
    buffer = baris.pop() ?? ''
    for (const b of baris) {
      const bersih = b.replace(/^data:\s*/, '').trim()
      if (!bersih || bersih === '[DONE]') continue
      try {
        const json = JSON.parse(bersih)
        const delta = json.choices?.[0]?.delta?.content
        if (!delta) continue
        teks += delta
        token++
        if (!pertama) {
          const hasil = kalimatPertamaSelesai(teks)
          if (hasil) pertama = { detik: (performance.now() - mulai) / 1000, ...hasil }
        }
      } catch { /* potongan SSE belum utuh */ }
    }
  }

  const selesai = (performance.now() - mulai) / 1000
  return { pertanyaan, teks, pertama, selesai, token }
}

const hasil = []
for (const q of PERTANYAAN) hasil.push(await jalankan(q))

console.log('\n=== HASIL ===')
for (const h of hasil) {
  const p = h.pertama
  console.log(`\nT: ${h.pertanyaan}`)
  console.log(`   ${h.token} potongan, aliran selesai ${h.selesai.toFixed(2)} dtk`)
  if (p) {
    console.log(`   kalimat-1 siap @ ${p.detik.toFixed(2)} dtk  <- hemat ${(h.selesai - p.detik).toFixed(2)} dtk`)
    console.log(`   potongan: ${JSON.stringify(p.potongan)}`)
  } else {
    console.log('   (tidak ada penutup kalimat terdeteksi)')
  }
  console.log(`   balasan: ${JSON.stringify(h.teks.slice(0, 160))}`)
}

const sah = hasil.filter(h => h.pertama)
if (sah.length) {
  const rata = sah.reduce((a, h) => a + h.pertama.detik, 0) / sah.length
  const rataSelesai = sah.reduce((a, h) => a + h.selesai, 0) / sah.length
  const toks = sah.reduce((a, h) => a + h.token, 0) / sah.length
  console.log(`\nRATA-RATA: kalimat-1 @ ${rata.toFixed(2)} dtk | selesai @ ${rataSelesai.toFixed(2)} dtk | hemat ${(rataSelesai - rata).toFixed(2)} dtk | ${toks.toFixed(0)} potongan`)
}
