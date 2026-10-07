/**
 * Pengelola proses `llama-server` dan pemilihan model GGUF.
 */
import { readdirSync, statSync } from 'node:fs'
import { spawn } from 'node:child_process'
import { basename, extname, join } from 'node:path'

function cari(root, cocok, kedalaman) {
  let info
  try {
    info = statSync(root)
  } catch {
    return undefined
  }
  if (!info.isDirectory()) return cocok(basename(root)) ? root : undefined
  let entri
  try {
    entri = readdirSync(root, { withFileTypes: true })
  } catch {
    return undefined
  }
  for (const item of entri) {
    const path = join(root, item.name)
    if (!item.isDirectory() && cocok(item.name)) return path
    if (item.isDirectory() && kedalaman > 0) {
      const ketemu = cari(path, cocok, kedalaman - 1)
      if (ketemu) return ketemu
    }
  }
  return undefined
}

function cariSemua(root, cocok, kedalaman) {
  let info
  try {
    info = statSync(root)
  } catch {
    return []
  }
  if (!info.isDirectory()) return []
  let entri
  try {
    entri = readdirSync(root, { withFileTypes: true })
  } catch {
    return []
  }
  const hasil = []
  for (const item of entri) {
    const path = join(root, item.name)
    if (!item.isDirectory() && cocok(item.name)) hasil.push(path)
    else if (item.isDirectory() && kedalaman > 0) hasil.push(...cariSemua(path, cocok, kedalaman - 1))
  }
  return hasil
}

const gguf = nama => extname(nama).toLowerCase() === '.gguf'

export function daftarModel(k) {
  const akar = []
  if (k.localModelPath) {
    const eksplisit = join(k.akar, k.localModelPath)
    try {
      if (statSync(eksplisit).isFile()) akar.push(eksplisit)
    } catch {
      /* jalur eksplisit tidak ada */
    }
  }
  for (const dir of ['model', join('assets', 'llm')]) {
    for (const f of cariSemua(join(k.akar, dir), gguf, 2)) {
      if (!akar.includes(f)) akar.push(f)
    }
  }
  return akar.sort((a, b) => basename(a).toLowerCase().localeCompare(basename(b).toLowerCase()))
}

export function cariModel(k, log) {
  if (k.localModelPath) {
    const eksplisit = join(k.akar, k.localModelPath)
    try {
      if (statSync(eksplisit).isFile()) return { path: eksplisit, ok: true }
    } catch {
      /* lanjut ke pemindaian */
    }
  }
  const semua = daftarModel(k)
  if (semua.length === 0) return { path: '', ok: false }
  if (semua.length === 1) return { path: semua[0], ok: true }
  log?.(
    `! ${semua.length} model GGUF ditemukan dan VTUBER_LOCAL_MODEL_PATH tidak menunjuk berkas: ` +
      `memilih "${basename(semua[0])}". Setel VTUBER_LOCAL_MODEL_PATH agar tidak menebak. ` +
      `Kandidat: ${semua.map(f => basename(f)).join(', ')}`
  )
  return { path: semua[0], ok: true }
}

export function aliasModel(k, model) {
  const eksplisit = k.localModelAlias ? k.localModelAlias.trim() : ''
  if (eksplisit) return eksplisit
  let berkas = model
  if (!berkas) berkas = cariModel(k).path
  if (!berkas) return 'gguf'
  return basename(berkas, extname(berkas))
}

export function cariLlamaServer(k) {
  const root = join(k.akar, k.llamaServer)
  const nama = process.platform === 'win32' ? 'llama-server.exe' : 'llama-server'
  return cari(root, n => n.toLowerCase() === nama.toLowerCase(), 3)
}

function pipaBerawalan(aliran, target, awalan) {
  let sisa = ''
  aliran.setEncoding('utf8')
  aliran.on('data', potongan => {
    sisa += potongan
    const baris = sisa.split(/\r?\n/)
    sisa = baris.pop() ?? ''
    for (const b of baris) if (b.trim()) target(`${awalan}${b}`)
  })
  aliran.on('end', () => {
    if (sisa.trim()) target(`${awalan}${sisa}`)
  })
  aliran.on('error', () => {
    /* aliran anak ditutup */
  })
}

const tunggu = ms => new Promise(selesai => setTimeout(selesai, ms))

export class Process {
  constructor(config, port) {
    this.config = config
    this.port = port
    this.proc = undefined
  }

  async start(signal) {
    const binary = cariLlamaServer(this.config)
    if (!binary) return { ok: false, reason: `llama-server tidak ditemukan di ${this.config.llamaServer}` }
    const model = cariModel(this.config, pesan => console.error(pesan))
    if (!model.ok) return { ok: false, reason: 'model GGUF tidak ditemukan' }

    const vulkan = this.config.llmProvider === 'vulkan'
    const ctx = vulkan ? this.config.vulkanCtx : this.config.localModelCtx
    const ngl = vulkan ? this.config.vulkanNgl : 0
    const args = [
      '-m', model.path,
      '-a', aliasModel(this.config, model.path),
      '--host', '127.0.0.1',
      '--port', String(this.port),
      '-c', String(ctx),
      '-t', String(this.config.localModelThreads),
      '-ngl', String(ngl),
      '-np', '1',
      '-b', '2048',
      '-ub', '512',
    ]
    if (vulkan) {
      args.push('-ctk', 'q8_0', '-ctv', 'q8_0')
      if (this.config.vulkanFa) args.push('--flash-attn', 'on')
    }
    args.push(
      '--jinja',
      '--min-p', String(this.config.localMinP),
      '--top-p', String(this.config.localTopP),
      '-rea', this.config.localReasoning
    )

    try {
      const proc = spawn(binary, args, {
        cwd: this.config.akar,
        stdio: ['ignore', 'pipe', 'pipe'],
        windowsHide: true,
      })
      this.proc = proc
      if (proc.stdout) pipaBerawalan(proc.stdout, baris => console.log(baris), '[llama] ')
      if (proc.stderr) pipaBerawalan(proc.stderr, baris => console.error(baris), '[llama] ')
    } catch (error) {
      return { ok: false, reason: error instanceof Error ? error.message : String(error) }
    }

    let alasan = 'waktu tunggu llama-server habis'
    for (let i = 0; i < 120; i++) {
      if (signal?.aborted) {
        this.stop()
        return { ok: false, reason: 'dibatalkan' }
      }
      if (this.proc.exitCode !== null) {
        return { ok: false, reason: `llama-server berhenti (kode ${this.proc.exitCode})` }
      }
      try {
        const res = await fetch(`http://127.0.0.1:${this.port}/health`, { signal: AbortSignal.timeout(1000) })
        if (res.status === 200) return { ok: true, reason: 'siap' }
        alasan = `health HTTP ${res.status}`
      } catch {
        /* belum hidup */
      }
      await tunggu(250)
    }
    this.stop()
    return { ok: false, reason: alasan }
  }

  stop() {
    const proc = this.proc
    this.proc = undefined
    if (!proc || proc.exitCode !== null) return
    try {
      proc.kill('SIGTERM')
    } catch {
      /* sudah mati */
    }
    const paksa = setTimeout(() => {
      try {
        proc.kill('SIGKILL')
      } catch {
        /* sudah mati */
      }
    }, 1500)
    paksa.unref?.()
    proc.once('exit', () => clearTimeout(paksa))
  }
}
