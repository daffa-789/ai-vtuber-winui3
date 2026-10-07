/**
 * Titik masuk server Node (JavaScript murni).
 *
 * Membaca .env, menjalankan llama-server bila perlu, menyusun prompt karakter,
 * menginisialisasi Kizuna & Memori Bertingkat (@aituber-onair),
 * lalu menyajikan /api/health, /api/chat, /api/kizuna, /api/autonomous/proactive.
 */
import { existsSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { bacaKonfig, buatEnvSource, cariAkarRepo } from './config.js'
import { CharacterVault, bacaPersona } from './character.js'
import { SilverWolfKizuna } from './kizuna.js'
import { TieredMemoryEngine } from './tiered-memory.js'
import { Agent } from './agent.js'
import { OpenAiCompatibleProvider, StubProvider } from './inference.js'
import { Process, aliasModel, cariModel } from './llama.js'
import { buatServer } from './server.js'

const PERSONA_CADANGAN =
  'Kamu adalah Silver Wolf, hacker Punklorde dari Stellaron Hunters. ' +
  'Kamu memanggil pengguna Master atau Sayang, bicara santai, manis, dan akrab.'

function ambilArgumen(argv) {
  const hasil = {}
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i]
    if (!arg || !arg.startsWith('-')) continue
    const kunci = arg.replace(/^-+/, '')
    const nilai = argv[i + 1]
    if (nilai === undefined || nilai.startsWith('-')) {
      hasil[kunci] = 'true'
      continue
    }
    hasil[kunci] = nilai
    i++
  }
  return hasil
}

function cariAkar(dari) {
  if (existsSync(join(dari, 'package.json')) || existsSync(join(dari, '.git'))) return dari
  try {
    return cariAkarRepo(dari)
  } catch {
    return process.cwd()
  }
}

function pilihProvider(konfig) {
  if (konfig.stub) return { provider: new StubProvider(), namaModel: 'stub' }
  if (konfig.llmProvider === 'ollama') {
    return {
      provider: new OpenAiCompatibleProvider('ollama', konfig.ollamaUrl, konfig.ollamaModel),
      namaModel: `ollama/${konfig.ollamaModel}`,
    }
  }
  const portInferensi = konfig.port !== 0 ? konfig.port + 1 : 18788
  const proses = new Process(konfig, portInferensi)
  const model = cariModel(konfig, pesan => console.error(pesan))
  const alias = aliasModel(konfig, model.path)
  const awalan = konfig.llmProvider === 'vulkan' ? 'vulkan' : 'local'
  return {
    provider: new OpenAiCompatibleProvider('llama-server', `http://127.0.0.1:${portInferensi}`, alias),
    namaModel: `${awalan}/${alias}`,
    llama: proses,
  }
}

async function jalankan(konfig, host, staticRoot, assetRoot, vaultRoot) {
  let persona = PERSONA_CADANGAN
  try {
    persona = await bacaPersona(konfig.akarPersona)
  } catch (error) {
    console.error(`! ${error instanceof Error ? error.message : String(error)}; memakai persona cadangan minimal`)
  }

  const lumbung = new CharacterVault(vaultRoot)
  const memoryEngine = new TieredMemoryEngine(lumbung)
  const kizunaDir = join(vaultRoot, 'kizuna')
  const kizuna = new SilverWolfKizuna(kizunaDir, 'master')
  await kizuna.initialize().catch(err => console.error('! gagal inisialisasi kizuna:', err))

  const { provider, namaModel, llama } = pilihProvider(konfig)
  if (llama) {
    void llama.start().then(({ ok, reason }) => {
      if (!ok) console.error(`! inferensi lokal belum siap: ${reason}`)
    })
  }

  const otak = new Agent({
    provider,
    persona,
    vault: lumbung,
    kizuna,
    memoryEngine,
    localPrompt: konfig.llmProvider !== 'ollama',
    onError: error => console.error('memori:', error),
  })

  const opsi = { konfig, agent: otak, provider, vault: lumbung, kizuna, modelName: namaModel, staticRoot, assetRoot }
  const server = buatServer(opsi)

  const port = await new Promise((selesai, gagal) => {
    server.once('error', gagal)
    server.listen(konfig.port, host, () => {
      const alamat = server.address()
      selesai(alamat.port)
    })
  })

  const memori = lumbung.available() ? 'siap' : lumbung.unavailableReason()
  const snapKizuna = kizuna.getSnapshot()
  console.log(`Silver Wolf server Node siap di http://${host}:${port}`)
  console.log(`  model: ${namaModel} · memori: ${memori} · kizuna: Lv.${snapKizuna.level} ${snapKizuna.stageName} (${snapKizuna.points} XP)`)
  for (const w of konfig.warnings) console.error(`  ! ${w}`)
  console.log(`port=${port}`)
  return { server, llama, kizuna }
}

async function main() {
  const args = ambilArgumen(process.argv.slice(2))
  const akarArg = args.root ?? ''
  const akar = akarArg ? resolve(akarArg) : cariAkar(process.cwd())

  const env = buatEnvSource(akar)
  const konfig = bacaKonfig(env, akar)
  if (args.port !== undefined) {
    const port = Number(args.port)
    if (Number.isFinite(port) && port >= 0) konfig.port = port
  }
  const host = args.host ?? '127.0.0.1'
  const staticRoot = args.static && args.static !== 'true' ? args.static : undefined
  const assetRoot = args.assets && args.assets !== 'true' ? args.assets : join(akar, 'assets')
  const vaultRoot = args.vault && args.vault !== 'true' ? args.vault : join(akar, 'silver_wolf_memory')

  const runtime = await jalankan(konfig, host, staticRoot, assetRoot, vaultRoot)

  let sudahKeluar = false
  const tutup = (kode = 0) => {
    if (sudahKeluar) return
    sudahKeluar = true
    runtime.kizuna?.destroy()
    runtime.llama?.stop()
    runtime.server.close()
    setTimeout(() => process.exit(kode), 400)
  }
  process.on('SIGINT', () => tutup(0))
  process.on('SIGTERM', () => tutup(0))
  process.on('beforeExit', () => {
    runtime.kizuna?.destroy()
    runtime.llama?.stop()
  })
}

main().catch(error => {
  console.error(`server gagal mulai: ${error instanceof Error ? error.stack ?? error.message : String(error)}`)
  process.exit(1)
})
