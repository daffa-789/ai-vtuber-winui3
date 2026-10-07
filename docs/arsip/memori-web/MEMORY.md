# MEMORY.md — Catatan Proyek "Silver Wolf" (Desktop AI VTUBER)

## Arsitektur (per 2026-10-04)

- **Backend: Node**, bukan Bun, bukan Go. Ada di `apps/server-node/` (`node:http`).
  Bun **pernah** dicoba (2026-10-04) lalu dibatalkan atas permintaan user —
  jangan pindahkan kembali ke Bun tanpa diminta. Go sidecar sudah dihapus permanen.
- **UI: Vue 3 + JSX**, bukan SFC. Tidak ada berkas `.vue` di repo ini.
  `apps/stage-tamagotchi/renderer/src/App.tsx` + `src/komponen/`.
- **Electron** tetap jadi host (jendela, tray, hotkey Ctrl+Shift+S). Proses utama
  men-spawn Node dan membaca baris `port=<angka>` dari stdout.
  - dev: Node sistem + `--import tsx` (baca sumber TS langsung).
  - terkemas: `process.execPath` + `ELECTRON_RUN_AS_NODE=1` → **Node bawaan Electron**,
    jadi tidak ada runtime tambahan di installer.
- Paket `@silverwolf/core-config` dan `@silverwolf/core-character` adalah **sumber asli**
  yang dipakai bersama oleh server Node dan renderer. Jangan menulis ulang logikanya.

## Konvensi yang wajib diikuti

- **Semua komentar dan nama domain berbahasa Indonesia** (`bacaKonfig`, `lumbung`,
  `otak`, `dipakai`, dsb). Ikuti, jangan terjemahkan ke Inggris.
- Paket internal dikonsumsi **sebagai sumber TS** — tanpa langkah build. Renderer lewat
  alias Vite; server Bun lewat `paths` di `apps/server-bun/tsconfig.json`.
- **Jangan percaya `npm run typecheck` sebagai bukti.** Akar `tsconfig.json` berisi
  `"files": []`; periksa per-app: `typecheck:server|main|renderer`.
- Jalankan `vue-tsc` (bukan `tsc`) untuk renderer.

## Jebakan terkonfirmasi

- Jalankan TypeScript server lewat `node --import tsx` (butuh Node ≥ 20.6; mesin ini
  Node 22.22.2). Bundel untuk paket: esbuild `--format=esm --outfile=…**.mjs**` —
  kalau `.js`, folder `resources/server/` tanpa `package.json` membuatnya dibaca
  sebagai CommonJS dan ESM-nya rusak.
- `npm install bun` pernah gagal di mesin ini (npm melewati dependensi opsional
  platform `@oven/bun-windows-x64`). Sudah tidak relevan — Bun tidak dipakai lagi.
- `taskkill //F //IM bun.exe` bisa menolak; bunuh lewat PID.
- `wmic.exe`/`schtasks.exe` diblokir kebijakan keamanan mesin ini.
- Venv Python terisolasi: `C:\Users\Daffa\.workbuddy-ai\binaries\python\envs\default`.
- Tool PowerShell agen sering **tidak menangkap stdout** → tulis hasil ke berkas temp
  (`Set-Content`) lalu baca dengan tool Read.
- Perintah `npm install` pernah diblokir security policy saat bash gagal fork; jalankan
  binary node/tsc langsung dengan path absolut sebagai gantinya.

## Bahasa & aset (sensus 2026-10-04)

Seluruh **kode** proyek adalah TypeScript/JavaScript/JSX: 48 `.ts`, 5 `.tsx`,
15 `.mjs`, 2 `.js`. **Nol** Python, nol Go, nol bahasa lain di working tree.

Sisa non-JS hanya:
- `bin/llama/` — 21 `.exe` + 30 `.dll` (llama.cpp praterkompilasi, C++; dependensi pihak
  ketiga, bukan kode kita).
- Model/data: `.onnx`, `.gguf`, `.pt`, `.pth`, `.bin`, `.moc3`, `.index`.

`_cadangan/` (22 MB, arsip runtime Python lama) **sudah dihapus** 2026-10-04 atas
permintaan user. Empat berkas terlacak git (zip Python, tar.gz skrip, git bundle,
`.ts-terakhir`) masih bisa dipulihkan dari riwayat commit sebelum penghapusan; dua
berkas `residu-2026-10-01/` tidak terlacak dan hilang permanen (isinya hanya 13 baris
percakapan uji + snapshot `Mood.md` usang).
