# Silver Wolf

Kompanion **desktop** AI — karakter Live2D yang hidup di layar, bicara dengan suara
sendiri, dan mengingat percakapan.

Aplikasi ini **desktop-only**: tidak ada jalur peramban. UI (Vue 3 + Live2D, ditulis
penuh dengan **JSX**) hanya dihidangkan di dalam jendela Electron, dan seluruh logika
server — konfigurasi, persona, memori, inferensi, penyajian aset — berjalan di
**server Node** (`apps/server-node`).

```
Electron (jendela, tray, hotkey)  ──spawn──▶  node apps/server-node/src/main.js
        ▲ renderer Vue JSX + Live2D              ├─ baca .env
        └───────── HTTP 127.0.0.1 ───────────────┤─ jalankan llama-server
                                                 ├─ susun prompt + memori
                                                 └─ sajikan /api/* dan aset
```

> **Status: backend Node + UI Vue JSX (100% JavaScript).** Seluruh logika server berjalan di
> `apps/server-node/` (`node:http`) dan UI ditulis penuh dengan **Vue JSX**
> (`renderer/src/App.jsx`). 100% JavaScript / JSX tanpa TypeScript dan tanpa berkas `.vue`.
> Tidak ada runtime tambahan di luar Node — saat terkemas server dijalankan Node bawaan Electron.

## Struktur

```
apps/
  stage-tamagotchi/       Electron desktop — jendela, tray, hotkey
    src/main/             Proses utama: menjalankan server Node, baca port-nya
    src/preload/          Jembatan konteks-terisolasi
    renderer/             Vue 3 + JSX + UnoCSS + Live2D (satu-satunya wujud UI)
      src/App.jsx         Orkestrator: keadaan bersama + merakit Panggung & Konsol
      src/komponen/       Panggung, Konsol, DaftarPesan, Komposer (semuanya JSX)
      src/ucapan.js       Fungsi murni pemotong tag emosi
  server-node/            Server Node (`node:http`) — backend aplikasi
    src/main.js           Titik masuk: parse argumen, bootstrap, cetak port=
    src/server.js         Rute HTTP: /api/health, /api/chat (aliran), statis
    src/agent.js          Orkestrasi chat + penyimpanan memori
    src/inference.js      Provider OpenAI-compatible (llama-server / Ollama) + stub
    src/llama.js          Pemilihan model GGUF + pengelola proses llama-server
packages/                 Paket JavaScript yang dipakai renderer/server
  core-config/            Skema `.env` + koerser (dipakai server Node)
  core-character/         Persona, prompt, mood, tag wajah, vault memori
  pipelines-audio/        Rantai suara: piper → rvc, cache, WAV
  provider-inference/     Antarmuka Brain / Ears / Mouth / Body
  stage-ui/               Store Pinia
  stage-ui-live2d/        Renderer Live2D + mesin wajah
  audio/                  Tangkap mic + VAD + pemutar
```

Scope paket: `@silverwolf/*`. Paket internal dikonsumsi langsung sebagai modul ES murni
JavaScript — Vite memproses JSX untuk renderer, Node menjalankan modul ES untuk server,
dan esbuild membundelnya untuk versi terkemas.

### Catatan bundel renderer

`App.jsx` mengimpor `bacaPanggung` dari **submodul murni**
(`@silverwolf/stage-ui-live2d/panggung.js`), bukan dari indeks paketnya. Indeks paket
itu menarik PIXI + Cubism, jadi mengimpornya di jalur awal akan menyeret ~1,24 MB ke
bundel pertama. Dengan submodul murni + `import()` dinamis untuk `Live2DRenderer`,
bundel awal terukur turun dari **2.001 kB menjadi 765 kB**, dan PIXI baru diunduh saat
model Live2D benar-benar dimuat.

## Menjalankan

Butuh **Node 20.6+**. Tidak ada runtime lain yang perlu dipasang.

```bash
npm install

npm run dev             # Electron + renderer (dev server Vite) + server Node
npm run server:dev      # hanya server Node (tanpa Electron)
npm run build:win       # installer NSIS
```

Skrip yang tersedia:

| Skrip | Isi |
|---|---|
| `server:dev` | Jalankan server Node dari sumber (`node apps/server-node/src/main.js`) |
| `server:bundle` | Bundel server Node dengan esbuild menjadi `out/server/main.js` untuk dipaketkan |
| `dev` / `dev:tamagotchi` | Electron dalam mode pengembangan |
| `build` | `server:bundle` lalu bundel `out/` (main, preload, renderer) |
| `build:win` | `build` lalu installer NSIS |

Proses utama Electron menjalankan server Node dengan `-port 0`, lalu membaca baris
`port=<angka>` yang dicetak server ke stdout — jadi tidak ada port yang ditebak
atau bentrok dengan server yang sudah berjalan.

Saat pengembangan, Electron menjalankan server Node langsung dari sumber JavaScript
`apps/server-node/src/main.js`. Saat terkemas, **tidak ada runtime tambahan yang ikut
installer**: `process.execPath` dipanggil dengan `ELECTRON_RUN_AS_NODE=1`, sehingga
Node bawaan Electron yang menjalankan bundel `resources/server/main.js`.

Untuk menguji tanpa model besar, isi `VTUBER_STUB=ya` (atau biarkan aplikasi
terkemas tanpa model GGUF: ia otomatis masuk mode tiruan).

> **Catatan lingkungan (Windows terkunci):** `npm install` di mesin ini pernah
> melewati dependensi opsional platform (dulu dari paket `bun`), sehingga postinstall
> paket itu gagal. Bun sudah tidak dipakai lagi, jadi masalah itu tidak berlaku.
>
> Skrip root memakai npm, bukan `turbo run`, karena turbo tidak bisa spawn proses anak
> di lingkungan ini (`ERROR_PIPE_BUSY`). `turbo.json` tetap ada dan varian `npm run turbo:*`
> disediakan untuk CI / mesin normal.

## Model & aset

Semua model berukuran besar **tidak ikut git**. Tata letak:

| Lokasi | Isi | Catatan |
|---|---|---|
| `assets/` | Rumah baru aset: `voices/`, `encoders/`, `piper/`, `whisper/`, `llm/` | gitignored |
| `public/models/silverwolf/` | Model Live2D (`.moc3`, tekstur, ekspresi, gerakan) | gitignored — lisensi Live2D |
| `public/live2dcubismcore.min.js` | Cubism Core | gitignored — lisensi Live2D |
| `model/` | LLM GGUF (MiniCPM5-2B Q4_K_M, 1 Okt 2026) | gitignored |
| `bin/llama/` | llama.cpp build Vulkan (`llama-server.exe`) | gitignored |
| `silver_wolf_memory/` | Persona + memori karakter | gitignored — **berisi fakta pribadi** |

### Ganti model GGUF

Model dipilih lewat `VTUBER_LOCAL_MODEL_PATH`. Dua flag llama-server **wajib**
untuk MiniCPM5 (dan untuk varian *thinking* Qwen3):

| Flag | Kunci `.env` | Alasan |
|---|---|---|
| `--min-p 0` | `VTUBER_LOCAL_MIN_P=0` | bawaan llama.cpp `0,05` membuat MiniCPM5 mengulang kalimat |
| `-rea off` | `VTUBER_LOCAL_REASONING=off` | mode berpikir mengawali balasan dengan `<think>`, merusak pembacaan tag emosi |

Keduanya sudah di-default-kan di `packages/core-config` dan di `.env`. Bila ingin
mengaktifkan mode berpikir untuk tugas penalaran, setel `VTUBER_LOCAL_REASONING=on`
**dan** naikkan `max_tokens` di `apps/server-node/src/server.ts` — batas 512 saat ini
akan habis di dalam blok `<think>`.

> MiniCPM5 tercatat hanya untuk EN + ZH. Persona dan prompt proyek ini berbahasa
> Indonesia, jadi kualitasnya di bawah Llama-3.2-3B. Qwen3 disiapkan sebagai
> pembanding.

### Konversi model RVC

`rvc-onnx-web` mengubah checkpoint PyTorch `.pth` menjadi ONNX, murni JavaScript:

```bash
npm run voice:convert  # .pth → assets/voices/silverwolf/model.onnx
npm run spike:rvc      # periksa nama/dimensi tensor semua model ONNX
```

Peringatan: hanya **RVC v2**; korelasi audio akhir **~78%** karena ada operator
`RandomNormalLike` yang sengaja acak — timbre yang sedikit berbeda itu normal.
Berkas `.index` FAISS tidak ditangani oleh konverter ini.

**Fakta terverifikasi** (dari `npm run spike:rvc`):

| Model | Input | Output |
|---|---|---|
| Generator RVC (105 MB) | `phone`, `phone_lengths`, `pitch`, `nsff0`, `sid` | `audio`, `sr` (=40000) |
| RMVPE f0 (345 MB) | `input` (mel spectrogram 16 kHz) | `output` |

Masih kurang: **`vec-768-layer-12.onnx`** (ContentVec). RVC v2 memerlukan
ContentVec 768-dim layer-12 — **bukan** `hubert-base-ls960`.

## Peta jalan

| Fase | Isi | Status |
|---|---|---|
| 0 | Scaffold (npm, turbo, uno, JSX) | **selesai** |
| 1 | Inti backend (server Node) | **selesai** |
| 2 | Stage MVP (Vue 3 + Live2D, chat + TTS + mic) | **selesai** |
| 3 | Rantai TTS (piper ONNX + espeak-ng WASM + cache) | **selesai** |
| 4 | Pipeline RVC (ContentVec → F0 → generator, onnxruntime-web) | **selesai** — aset model diperlukan |
| 5 | Aplikasi Electron Windows (tray, hotkey, installer NSIS) | **selesai** |
| 6 | Fokus desktop-only: jalur peramban dibuang | **selesai** |
| 7 | Backend **Node** + UI **Vue JSX** (100% JavaScript) | **selesai** |
| 8 | Verifikasi paritas + penutupan | |

Seluruh kode di repositori ini adalah 100% JavaScript / JSX (0 TypeScript) — tidak ada Python maupun
Go di working tree. Sisa non-JS hanya binary pihak ketiga (`bin/llama/`, llama.cpp) dan
berkas model/data (`.onnx`, `.gguf`, `.pt`, `.bin`, `.moc3`).

## Lisensi & penggunaan

Model Live2D dan Cubism Core tunduk pada lisensi Live2D Inc. dan tidak boleh diedarkan
sebagai berkas lepas. Teknologi kloning suara hanya untuk proyek kreatif dengan izin —
jangan untuk peniruan identitas, penipuan, atau pelecehan.

## Desktop Windows, suara, dan mikrofon

Aplikasi desktop berada di `apps/stage-tamagotchi`. Ia menyediakan jendela native,
tray, close-to-tray, dan hotkey global **Ctrl+Shift+S**. Data pribadi dan model tidak
ditanam di installer. Pada Windows, klik tray → **Buka folder model & konfigurasi**,
lalu isi tata letak berikut di folder tersebut:

```text
.env
assets/
  piper/id_ID-news_tts-medium.onnx
  piper/id_ID-news_tts-medium.onnx.json
  encoders/vec-768-layer-12.onnx
  voices/silverwolf/model.onnx
  live2d/live2dcubismcore.min.js
  live2d/silverwolf/silverwolf.model3.json (+ tekstur/ekspresi/gerakan)
model/MiniCPM5-2B-Q4_K_M.gguf
bin/llama/llama-server.exe (+ DLL Vulkan)
silver_wolf_memory/persona.md
```

- **STT:** Whisper ONNX melalui `@huggingface/transformers`; model diunduh dan di-cache
  saat pemakaian pertama, lalu inferensi berjalan lokal.
- **TTS:** Piper + eSpeak-ng WASM melalui `@mintplex-labs/piper-tts-web`, memakai model
  Indonesia lokal di atas.
- **RVC:** ContentVec 768 + ekstraksi F0 lokal + generator RVC v2 melalui
  `onnxruntime-web`. Bila model RVC tidak ada/gagal, audio Piper tetap diputar.

Saat dipaketkan, installer hanya perlu membawa **bundel server** di
`resources/server/main.js` — tidak ada runtime tambahan, karena server dijalankan
Node bawaan Electron. Bila bundel itu hilang, proses utama menolak mulai dengan pesan
yang menyuruh menjalankan `npm run server:bundle`.

```bash
npm run assets:verify
npm run dev
npm run build:win
```

Perintah terakhir menghasilkan `apps/stage-tamagotchi/release/Silver-Wolf-Setup-0.1.0.exe`.
Workflow `.github/workflows/build-windows.yml` menjalankan `npm ci`, lalu
membangun installer pada runner Windows dan mengunggahnya sebagai artifact.
