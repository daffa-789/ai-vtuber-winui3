/**
 * Server HTTP di atas `node:http`.
 *
 * Rute:
 *  - /api/health
 *  - /api/chat (POST, streaming teks)
 *  - /api/kizuna (GET, snapshot relasi & afeksi)
 *  - /api/kizuna/touch (POST, headpat & reaksi tsundere)
 *  - /api/autonomous/proactive (POST, inisiatif Neuro-sama)
 *  - berkas statis model/suara di bawah /assets/, /models/, /live2dcubismcore.min.js
 *  - renderer Electron (staticRoot)
 */
import { createReadStream, statSync } from 'node:fs'
import { createServer } from 'node:http'
import { join, resolve, sep } from 'node:path'

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.onnx': 'application/octet-stream',
  '.data': 'application/octet-stream',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
  '.wav': 'audio/wav',
  '.mp3': 'audio/mpeg',
  '.moc3': 'application/octet-stream',
}

const CORS = {
  'access-control-allow-origin': '*',
  'access-control-allow-headers': 'content-type',
  'access-control-allow-methods': 'GET, POST, OPTIONS',
  'cross-origin-opener-policy': 'same-origin',
  'cross-origin-embedder-policy': 'require-corp',
  'cross-origin-resource-policy': 'cross-origin',
}

function tulisJson(res, status, isi) {
  const badan = JSON.stringify(isi)
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    ...CORS,
  })
  res.end(badan)
}

export function rapikanRiwayat(mentah, maksPesan = 64, maksKarakter = 8192) {
  if (!Array.isArray(mentah)) return []
  const hasil = []
  for (const item of mentah) {
    if (!item || typeof item !== 'object') continue
    let isi = typeof item.content === 'string' ? item.content : ''
    if (!isi && Array.isArray(item.parts)) {
      isi = item.parts
        .map(p => (p && typeof p === 'object' ? p.text : undefined))
        .filter(x => x !== undefined)
        .join(' ')
    }
    if (!isi.trim()) continue
    const peran = item.role === 'assistant' || item.role === 'model' ? 'assistant' : 'user'
    hasil.push({ role: peran, content: isi.length > maksKarakter ? isi.slice(0, maksKarakter) : isi })
  }
  return hasil.length > maksPesan ? hasil.slice(hasil.length - maksPesan) : hasil
}

function bacaBadan(req, maks) {
  return new Promise((selesai, gagal) => {
    const potongan = []
    let panjang = 0
    req.on('data', c => {
      panjang += c.length
      if (panjang > maks) {
        gagal(new Error('body terlalu besar'))
        req.destroy()
        return
      }
      potongan.push(c)
    })
    req.on('end', () => selesai(Buffer.concat(potongan).toString('utf8')))
    req.on('error', gagal)
  })
}

async function sajiStatis(res, root, diminta) {
  if (!root || diminta.includes('..')) return false
  const rootAbs = resolve(root)
  let bersih = resolve(join(rootAbs, diminta.replace(/^\/+/, '')))
  if (bersih !== rootAbs && !bersih.startsWith(rootAbs + sep)) return false
  let info
  try {
    info = statSync(bersih)
    if (info.isDirectory()) {
      bersih = join(bersih, 'index.html')
      info = statSync(bersih)
    }
  } catch {
    return false
  }
  if (!info.isFile()) return false

  const titik = bersih.lastIndexOf('.')
  const ext = titik >= 0 ? bersih.slice(titik).toLowerCase() : ''
  const html = ext === '.html'
  res.writeHead(200, {
    'content-type': MIME[ext] ?? 'application/octet-stream',
    'cache-control': html ? 'no-cache' : 'public, max-age=31536000, immutable',
    ...CORS,
  })
  await new Promise(selesai => {
    const aliran = createReadStream(bersih)
    aliran.on('error', () => {
      try {
        res.destroy()
      } catch {
        /* sudah tertutup */
      }
      selesai()
    })
    res.on('finish', () => selesai())
    aliran.pipe(res)
  })
  return true
}

async function health(o, res) {
  const av = await o.provider.available()
  let statusText = 'siap'
  if (!av.ok) {
    statusText = av.loading ? 'memuat' : 'tidak-jalan'
  }
  const model = av.ok ? o.modelName : `${o.modelName}/${statusText} (${av.reason})`
  const memori = o.vault?.available() ? 'memori lokal (silver_wolf_memory/)' : o.vault?.unavailableReason()
  const kizuna = o.kizuna ? o.kizuna.getSnapshot() : null

  tulisJson(res, 200, {
    ok: av.ok,
    loading: Boolean(av.loading),
    model,
    provider: o.konfig.llmProvider,
    statusText,
    cadangan: [],
    key: true,
    tts: 'siap',
    memori,
    kizuna,
    sisi: 'node',
    stt: { hidup: o.konfig.sttHidup, model: o.konfig.sttModel, siap: true },
  })
}

async function alirkanKeKlien(iterator, o, res, headersTambahan = {}) {
  let pertama
  try {
    pertama = await iterator.next()
  } catch (error) {
    return tulisJson(res, 503, { error: error instanceof Error ? error.message : String(error) })
  }
  if (pertama.done) return tulisJson(res, 503, { error: 'provider tidak menghasilkan apa pun' })
  if (pertama.value.err) return tulisJson(res, 503, { error: pertama.value.err.message })

  res.writeHead(200, {
    'content-type': 'text/plain; charset=utf-8',
    'cache-control': 'no-store',
    'x-accel-buffering': 'no',
    'x-model': o.modelName,
    ...headersTambahan,
    ...CORS,
  })
  if (pertama.value.text) res.write(pertama.value.text)
  try {
    while (true) {
      const { done, value } = await iterator.next()
      if (done) break
      if (value.err) {
        console.error('aliran berhenti:', value.err)
        break
      }
      if (value.text) res.write(value.text)
    }
  } catch (error) {
    console.error('aliran gagal:', error)
  }
  res.end()
}

async function chat(o, req, res) {
  let teks
  try {
    teks = await bacaBadan(req, o.konfig.maksBody)
  } catch (error) {
    const pesan = error instanceof Error ? error.message : String(error)
    return tulisJson(res, pesan === 'body terlalu besar' ? 413 : 400, { error: pesan })
  }

  let parsed
  try {
    parsed = JSON.parse(teks)
  } catch {
    return tulisJson(res, 400, { error: 'body harus JSON: { messages: [{role, content}] }' })
  }
  const pesan = rapikanRiwayat(parsed?.messages, o.konfig.maksPesan, o.konfig.maksKarakter)
  if (pesan.length === 0) return tulisJson(res, 400, { error: 'riwayat kosong' })

  const kendali = new AbortController()
  res.on('close', () => {
    if (!res.writableEnded) kendali.abort()
  })

  const iterator = o.agent.chat(pesan, { maxTokens: 512, temperature: 0.7 }, kendali.signal)[Symbol.asyncIterator]()
  await alirkanKeKlien(iterator, o, res)
}

async function touch(o, req, res) {
  const kendali = new AbortController()
  res.on('close', () => {
    if (!res.writableEnded) kendali.abort()
  })

  const iterator = o.agent.chatTouch({}, kendali.signal)[Symbol.asyncIterator]()
  const snapshot = o.kizuna ? o.kizuna.getSnapshot() : null
  const headers = snapshot ? { 'x-kizuna': encodeURIComponent(JSON.stringify(snapshot)) } : {}
  await alirkanKeKlien(iterator, o, res, headers)
}

async function proactive(o, req, res) {
  let idleDetik = 60
  if (req.method === 'POST') {
    try {
      const teks = await bacaBadan(req, 1024)
      if (teks) {
        const body = JSON.parse(teks)
        if (Number.isFinite(body.idle)) idleDetik = Number(body.idle)
      }
    } catch {}
  }

  const kendali = new AbortController()
  res.on('close', () => {
    if (!res.writableEnded) kendali.abort()
  })

  const iterator = o.agent.chatProactive(idleDetik, {}, kendali.signal)[Symbol.asyncIterator]()
  const snapshot = o.kizuna ? o.kizuna.getSnapshot() : null
  const headers = snapshot ? { 'x-kizuna': encodeURIComponent(JSON.stringify(snapshot)) } : {}
  await alirkanKeKlien(iterator, o, res, headers)
}

export function buatPenangan(o) {
  return function tangani(req, res) {
    void (async () => {
      try {
        if (req.method === 'OPTIONS') {
          res.writeHead(204, CORS)
          res.end()
          return
        }
        const p = new URL(req.url ?? '/', 'http://localhost').pathname
        const metode = req.method ?? 'GET'

        if (p === '/api/health' && metode === 'GET') return await health(o, res)
        if (p === '/api/chat' && metode === 'GET') return tulisJson(res, 405, { error: 'gunakan POST untuk /api/chat' })
        if (p === '/api/chat' && metode === 'POST') return await chat(o, req, res)

        // Endpoint Kizuna (@aituber-onair/kizuna)
        if (p === '/api/kizuna' && metode === 'GET') {
          return tulisJson(res, 200, {
            ok: true,
            kizuna: o.kizuna ? o.kizuna.getSnapshot() : null,
          })
        }
        if (p === '/api/kizuna/touch' && (metode === 'POST' || metode === 'GET')) {
          return await touch(o, req, res)
        }

        // Endpoint Proaktif Otonom Neuro-sama
        if (p === '/api/autonomous/proactive' && (metode === 'POST' || metode === 'GET')) {
          return await proactive(o, req, res)
        }

        if (metode === 'GET' && o.assetRoot && p.startsWith('/assets/')) {
          if (await sajiStatis(res, o.assetRoot, p.slice('/assets'.length))) return
        }
        if (metode === 'GET' && p.startsWith('/models/')) {
          if (o.assetRoot && (await sajiStatis(res, join(o.assetRoot, 'live2d'), p.slice('/models'.length)))) return
          if (await sajiStatis(res, join(o.konfig.akar, 'public', 'models'), p.slice('/models'.length))) return
        }
        if (metode === 'GET' && p === '/live2dcubismcore.min.js') {
          if (o.assetRoot && (await sajiStatis(res, join(o.assetRoot, 'live2d'), '/live2dcubismcore.min.js'))) return
          if (await sajiStatis(res, join(o.konfig.akar, 'public'), '/live2dcubismcore.min.js')) return
        }
        if (metode === 'GET' && p.startsWith('/onnx/')) {
          if (await sajiStatis(res, join(o.konfig.akar, 'public', 'onnx'), p.slice('/onnx'.length))) return
        }
        if (metode === 'GET' && p.startsWith('/piper/')) {
          if (await sajiStatis(res, join(o.konfig.akar, 'public', 'piper'), p.slice('/piper'.length))) return
        }
        if (metode === 'GET' && o.staticRoot && !p.startsWith('/api/')) {
          if (await sajiStatis(res, o.staticRoot, p)) return
          if (await sajiStatis(res, o.staticRoot, '/index.html')) return
        }
        tulisJson(res, 404, { error: 'tidak ditemukan' })
      } catch (error) {
        if (!res.headersSent) {
          tulisJson(res, 503, { error: error instanceof Error ? error.message : String(error) })
        } else {
          try {
            res.destroy()
          } catch {
            /* sudah tertutup */
          }
        }
      }
    })()
  }
}

export function buatServer(o) {
  return createServer(buatPenangan(o))
}
