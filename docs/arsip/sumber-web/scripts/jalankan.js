/**
 * Peluncur aplikasi desktop Silver Wolf.
 *
 * Menjalankan Electron langsung ke bundel yang sudah dibangun (`out/main/index.js`),
 * tanpa membangun ulang. Dua hal yang dirawat di sini:
 *
 * 1. `ELECTRON_RUN_AS_NODE` dibuang. Beberapa shell mewarisinya (umum bila shell
 *    dibuka dari dalam aplikasi Electron lain); kalau dibiarkan, `electron.exe`
 *    berjalan sebagai Node biasa dan gagal dengan:
 *      "The requested module 'electron' does not provide an export named 'BrowserWindow'"
 * 2. Argumen diteruskan apa adanya, jadi `node scripts/jalankan.js --sw-software`
 *    bisa dipakai untuk memaksa rendering software.
 *
 * Pakai: npm run start  |  npm run start -- --sw-software
 */
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const masuk = join(root, 'out', 'main', 'index.js')

if (!existsSync(masuk)) {
  console.error(`Bundel belum ada: ${masuk}\nJalankan "npm run build" lebih dulu.`)
  process.exit(1)
}

const require = createRequire(import.meta.url)
const electron = require('electron')

// Lihat catatan (1) di atas.
delete process.env.ELECTRON_RUN_AS_NODE

const argumen = [masuk, ...process.argv.slice(2)]
console.log(`Menjalankan Silver Wolf: ${electron} ${argumen.join(' ')}`)

const anak = spawn(electron, argumen, { cwd: root, stdio: 'inherit', env: process.env })
anak.on('exit', (kode, sinyal) => {
  if (sinyal) console.log(`\nElectron dihentikan oleh sinyal ${sinyal}`)
  process.exit(kode ?? 0)
})
anak.on('error', (error) => {
  console.error(`Gagal menjalankan Electron: ${error.message}`)
  process.exit(1)
})
