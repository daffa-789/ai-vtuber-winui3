/**
 * Konfigurasi server Node dan pemetaan variabel lingkungan (.env).
 */
import { existsSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export function cariAkarRepo(dari = fileURLToPath(import.meta.url)) {
  let dir = dirname(dari)
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'package.json')) || existsSync(join(dir, '.git'))) {
      return dir
    }
    const naik = dirname(dir)
    if (naik === dir) break
    dir = naik
  }
  return process.cwd()
}

export function temukanPersona(akar) {
  const kandidat = [
    join(akar, 'silver_wolf_memory', 'persona.md'),
    join(akar, 'silver_wolf memory', 'persona.md'),
    join(akar, 'memori-waifu', 'persona.md'),
    join(akar, 'persona.md'),
  ]
  for (const p of kandidat) {
    if (existsSync(p) && statSync(p).isFile()) return p
  }
  return join(akar, 'silver_wolf_memory', 'persona.md')
}

export function potongKomentar(nilai) {
  const mentah = nilai.trim()
  if (mentah.length >= 2 && (mentah[0] === '"' || mentah[0] === "'")) {
    const kutip = mentah[0]
    const akhir = mentah.indexOf(kutip, 1)
    return akhir > 0 ? mentah.slice(1, akhir) : mentah.slice(1)
  }
  for (let i = 0; i < mentah.length; i++) {
    if (mentah[i] === '#' && i > 0 && (mentah[i - 1] === ' ' || mentah[i - 1] === '\t')) {
      return mentah.slice(0, i).replace(/\s+$/, '')
    }
  }
  return mentah
}

export function bacaEnv(isi) {
  const hasil = {}
  for (const barisMentah of isi.split(/\r?\n/)) {
    const baris = barisMentah.trim()
    if (!baris || baris.startsWith('#') || !baris.includes('=')) continue
    const idx = baris.indexOf('=')
    const kunci = baris.slice(0, idx).trim()
    const nilai = baris.slice(idx + 1)
    if (kunci) hasil[kunci] = potongKomentar(nilai)
  }
  return hasil
}

export function bacaEnvAkar(akar) {
  const jalur = join(akar, '.env')
  if (!existsSync(jalur)) return {}
  try {
    return bacaEnv(readFileSync(jalur, 'utf8'))
  } catch {
    return {}
  }
}

export function buatEnvSource(akar) {
  return {
    file: bacaEnvAkar(akar),
    environ: process.env,
    warnings: [],
  }
}

function bersihLocal(nilai) {
  const v = nilai.trim()
  if (v.length >= 2 && v[0] === v[v.length - 1] && (v[0] === '"' || v[0] === "'")) {
    return v.slice(1, -1)
  }
  return v
}

export function nilai(env, kunci, bawaan = '') {
  const dariEnv = env.environ[kunci]
  if (dariEnv !== undefined && dariEnv.trim() !== '') {
    return bersihLocal(dariEnv)
  }
  return env.file[kunci] ?? bawaan
}

export function angka(env, kunci, bawaan) {
  const mentah = String(nilai(env, kunci, String(bawaan))).trim()
  if (mentah === '') return bawaan
  const n = Number(mentah)
  if (!Number.isFinite(n) || !Number.isInteger(n)) {
    env.warnings.push(`${kunci}="${mentah}" bukan bilangan bulat; dipakai bawaan ${bawaan}`)
    return bawaan
  }
  return n
}

export function angkaFloat(env, kunci, bawaan) {
  const mentah = String(nilai(env, kunci, String(bawaan))).trim()
  if (mentah === '') return bawaan
  const n = Number(mentah)
  if (!Number.isFinite(n)) {
    env.warnings.push(`${kunci}="${mentah}" bukan bilangan; dipakai bawaan ${bawaan}`)
    return bawaan
  }
  return n
}

const BENAR = new Set(['true', '1', 'ya', 'on'])
const SALAH = new Set(['false', '0', 'tidak', 'off'])

export function bool_(env, kunci, bawaan) {
  const mentah = nilai(env, kunci, bawaan ? 'true' : 'false').trim().toLowerCase()
  if (BENAR.has(mentah)) return true
  if (SALAH.has(mentah)) return false
  env.warnings.push(`${kunci}="${mentah}" bukan boolean; dipakai bawaan ${bawaan}`)
  return bawaan
}

export function daftar(env, kunci, bawaan) {
  return nilai(env, kunci, bawaan)
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)
}

export function bacaKonfig(env, akar) {
  const rawProvider = nilai(env, 'VTUBER_LLM_PROVIDER', 'vulkan')
  let llmProvider = 'vulkan'
  if (rawProvider === 'local' || rawProvider === 'llama_cpp') llmProvider = 'local'
  else if (rawProvider === 'ollama') llmProvider = 'ollama'

  const rawTampak = nilai(env, 'VTUBER_TAMPAK', 'pet')
  const tampak = rawTampak === 'browser' ? 'browser' : 'pet'

  const rawPetSembunyi = nilai(env, 'VTUBER_PET_SEMBUNYI', 'layar-penuh')
  const petSembunyi = ['tidak', 'layar-penuh', 'maksimal'].includes(rawPetSembunyi) ? rawPetSembunyi : 'layar-penuh'

  const rawReasoning = nilai(env, 'VTUBER_LOCAL_REASONING', 'off')
  const localReasoning = ['on', 'off', 'auto'].includes(rawReasoning) ? rawReasoning : 'off'

  return {
    akar,
    akarPersona: temukanPersona(akar),
    warnings: env.warnings,
    stub: bool_(env, 'VTUBER_STUB', false),

    // Wujud
    port: angka(env, 'VTUBER_PORT', 8787),
    tampak,
    petSembunyi,
    petTray: bool_(env, 'VTUBER_PET_TRAY', true),
    petHotkey: nilai(env, 'VTUBER_PET_HOTKEY', 'ctrl+shift+s'),

    // Otak
    llmProvider,
    localModelPath: nilai(env, 'VTUBER_LOCAL_MODEL_PATH', 'model/gemma-4-E4B-it-UD-Q4_K_XL.gguf'),
    localModelThreads: angka(env, 'VTUBER_LOCAL_MODEL_THREADS', 4),
    localModelCtx: angka(env, 'VTUBER_LOCAL_MODEL_CTX', 8192),
    localModelAlias: nilai(env, 'VTUBER_LOCAL_MODEL_ALIAS', 'gemma-4b'),
    localMinP: angkaFloat(env, 'VTUBER_LOCAL_MIN_P', 0),
    localTopP: angkaFloat(env, 'VTUBER_LOCAL_TOP_P', 0.95),
    localReasoning,
    llamaServer: nilai(env, 'VTUBER_LLAMA_SERVER', 'bin/llama'),
    vulkanNgl: angka(env, 'VTUBER_VULKAN_NGL', 99),
    vulkanFa: bool_(env, 'VTUBER_VULKAN_FA', true),
    vulkanCtx: angka(env, 'VTUBER_VULKAN_CTX', 8192),
    vulkanPerangkat: nilai(env, 'VTUBER_VULKAN_PERANGKAT', ''),
    vulkanMuatBoot: bool_(env, 'VTUBER_VULKAN_MUAT_BOOT', true),
    vulkanSlotDiam: bool_(env, 'VTUBER_VULKAN_SLOT_DIAM', true),
    ollamaUrl: nilai(env, 'VTUBER_OLLAMA_URL', 'http://127.0.0.1:11434'),
    ollamaModel: nilai(env, 'VTUBER_OLLAMA_MODEL', 'llama3.2:3b'),

    // STT
    sttHidup: bool_(env, 'VTUBER_STT', true),
    sttModel: nilai(env, 'VTUBER_STT_MODEL', 'base'),
    sttModelPath: nilai(env, 'VTUBER_STT_MODEL_PATH', 'assets/whisper'),

    // Server limits
    maksBody: 1024 * 1024,
    maksPesan: 64,
    maksKarakter: 8192,
  }
}
