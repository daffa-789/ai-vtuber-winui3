# Analisis Bug & Masalah — Silver Wolf (AI VTUBER)

Tanggal: 1 Okt 2026 · Kondisi awal: `npx tsc --noEmit` **lolos**, `model/` berisi
`Llama-3.2-3B-Instruct-Q4_K_M.gguf`, `MiniCPM5-2B-GGUF` sedang diunduh.

> **Update 21:05 — MiniCPM5 terpasang & diuji.** Lihat bagian MiniCPM5 di bawah.
>
> **Update 21:25 — bug zoom DIPERBAIKI** dan konfigurasi `VITE_*` disambungkan.
> Detail ada di bawah bagian P0. Ringkas: `packages/stage-ui-live2d/src/panggung.ts`
> (baru), `stage-ui-live2d/src/index.ts`, `apps/stage-web/src/App.vue`,
> `apps/stage-web/vite.config.ts`, `apps/stage-tamagotchi/electron.vite.config.ts`
> (`envDir`), `tsconfig.base.json` (`vite/client`). Verifikasi: `tsc --noEmit`
> lolos, **34 tes lolos** (4 di antaranya baru, khusus regresi zoom), build
> renderer sukses, dan bundel diperiksa tidak membocorkan `VTUBER_*`.
>
> Yang **belum** dikerjakan: mode pet Electron, lip-sync, `setPose`, mesin
> keadaan `VITE_KEADAAN`.

Ringkasan: kode tersusun rapi dan lolos pengecekan tipe, tetapi **lapisan
konfigurasi dan lapisan render tidak tersambung**. Hampir semua perilaku yang
Anda atur di `.env` tidak pernah dibaca oleh kode yang menggambar karakter atau
menjalankan model. Bug zoom yang Anda keluhkan adalah satu gejala dari masalah
yang sama.

---

## P0 — Karakter terlalu ke-zoom (bug utama)

Berkas: `packages/stage-ui-live2d/src/index.ts:15`

```ts
const fit = () => {
  const scale = Math.min(this.app.screen.width / model.width,
                         this.app.screen.height / model.height) * 0.92
  model.scale.set(scale)
  model.x = (this.app.screen.width - model.width) / 2
  model.y = this.app.screen.height - model.height
}
fit(); window.addEventListener('resize', fit)
```

Ada **tiga** kesalahan yang menumpuk di enam baris ini:

**1. `model.width` bergantung pada skala yang sedang dihitung (umpan balik).**
`Live2DModel` tidak meng-override `width`; ia memakai getter PIXI v6
(`node_modules/pixi.js/dist/browser/pixi.mjs:9018`):

```js
get: function () { return this.scale.x * this.getLocalBounds().width }
```

Jadi `model.width = skala_sekarang × lebar_asli`. Akibatnya `fit()` **tidak
idempoten**:

```
s1 = K                (benar; K = 0,92 × rasio pas)
s2 = K / s1 = 1       (kembali ke ukuran asli model → zoom gila)
s3 = K / s2 = K       (benar lagi)
s4 = 1 …
```

Setiap kali jendela di-resize, karakter berpindah antara "pas" dan **ukuran
native model** (biasanya 2–4× lebih besar dari jendela).

**2. `fit()` dijalankan sebelum PIXI selesai mengukur kanvas.**
`resizeTo` PIXI dijalankan **tertunda ke rAF berikutnya** (`queueResize()`),
sedangkan `fit()` dipanggil segera setelah `load()`. Kanvas belum punya atribut
ukuran (`.stage canvas` hanya diatur lewat CSS `inset:0;width:100%`), jadi
`app.screen` saat itu masih **300×150** (bawaan elemen `<canvas>`).

```
K300 = 0,92 × min(300/w0, 150/h0)      ← sangat kecil
```

Lalu pada resize pertama, `model.width` sudah ikut mengecil sehingga
`skala = K / K300` → bisa **10× lebih besar** dari seharusnya. Ini yang paling
mungkin Anda lihat sehari-hari: karakter kepotong besar sekali.

**3. PIXI tidak pernah memanggil ulang `fit()` saat `resizeTo` mengubah ukuran.**
`Application` memantau `window.resize` sendiri, tetapi `renderer.on('resize')`
tidak dipasang. Jadi perubahan ukuran kanvas karena layout (bukan karena
jendela) tidak pernah memperbaiki skala.

**Perbaikan yang benar — SUDAH DITERAPKAN (21:25)**

```ts
// packages/stage-ui-live2d/src/panggung.ts (fungsi murni, bisa diuji)
export function hitungSkala(lebar, tinggi, dasarLebar, dasarTinggi, zoom) {
  if (!lebar || !tinggi || !dasarLebar || !dasarTinggi) return 0
  return Math.min(lebar / dasarLebar, tinggi / dasarTinggi) * zoom
}

// packages/stage-ui-live2d/src/index.ts
private paskan(): void {
  const dasar = this.model?.internalModel        // ukuran TETAP, tidak ikut skala
  const { width: W, height: H } = this.app.screen
  const scale = hitungSkala(W, H, dasar.width, dasar.height, this.opsi.zoom)
  this.model.scale.set(scale)
  this.model.anchor.set(this.opsi.x, this.opsi.jangkar)  // pivot, bukan hitung manual
  this.model.x = W * this.opsi.x
  this.model.y = H * this.opsi.jangkar
}
```

dan kanvas kini benar-benar dipantau:

```ts
this.pas = () => this.paskan()
this.app.renderer.on('resize', this.pas)      // yang hilang: perubahan layout
window.addEventListener('resize', this.pas)
// destroy(): removeEventListener + renderer.off -- listener lama bocor
```

**Yang ikut berubah:**

- Skala memakai `min()` (*contain*) — karakter **tidak pernah terpotong**;
  kepala tidak akan kepotong meski `VITE_AVATAR_ZOOM` dinaikkan di atas 1.
- Konfigurasi hidup: `VITE_AVATAR_ZOOM` (0.96), `VITE_AVATAR_X` (0.5),
  `VITE_AVATAR_JANGKAR` (1), `VITE_RENDER_SKALA_MAKS` (2) kini dibaca lewat
  `bacaPanggung()` + `envDir` di kedua config Vite. Sebelumnya `0.92` diketik
  langsung di sumber.
- `VITE_MODEL_URL` dipakai (tidak lagi di-hardcode di `App.vue`).
- `VITE_RVC_TRANSPOSE=9` ditambahkan ke `.env` dan dipakai `App.vue`
  (sebelumnya `transpose: 10` diketik langsung, bertentangan dengan catatan
  ukur F0 Anda).
- `resolution` PIXI mengikuti `devicePixelRatio` yang dijepit
  `VITE_RENDER_SKALA_MAKS` — gambar lebih tajam di layar ber-DPI tinggi.

**Regresi dijaga** oleh `packages/stage-ui-live2d/test/panggung.spec.ts` (4 tes):
idempotensi (dipanggil dua kali hasilnya sama), jaminan *contain* untuk tiga
rasio kotak berbeda, kasus batas nol, dan bawaan `bacaPanggung()`.

**Keterbatasan yang perlu diketahui:** pada aplikasi Electron **terpasang**,
nilai `VITE_*` dibekukan saat build — pengguna yang mengedit `.env` di
`userData` tidak akan memengaruhi renderer yang sudah terbundel. Untuk mode pet
native nanti, konfigurasi sebaiknya dikirim lewat preload. Belum dikerjakan.

---

## P0 — Seluruh blok `VITE_*` di `.env` adalah konfigurasi mati

`.env` Anda berisi ~40 baris pengaturan avatar yang **tidak dibaca siapa pun**.
Bukti:

- `envWeb()` di `packages/core-config/src/web-env.ts` **tidak pernah dipanggil**
  (dan bahkan tidak di-re-export dari `core-config/src/index.ts`, jadi tidak
  bisa diakses lewat nama paket).
- Tidak ada satu pun `import.meta.env.VITE_*` atau `window.__VTUBER_ENV__` di
  `apps/` maupun `packages/`.
- Yang tersisa hanyalah `parseAvatar()` di `core-config`, yang mengisi
  `Konfig.wajah/pose/gerak` — lalu tidak dipakai di mana pun.

Yang langsung terasa:

| Kunci `.env` | Nilai | Yang terjadi di kode |
|---|---|---|
| `VITE_MODEL_URL` | `/models/silverwolf/...` | di-hardcode di `App.vue:23` |
| `VITE_AVATAR_ZOOM` | `0.96` | diganti konstanta `0.92` |
| `VITE_AVATAR_X` | `0.5` | tidak ada; selalu tengah |
| `VITE_AVATAR_JANGKAR` | `1` | tidak ada; `y = tinggi − tinggi_model` |
| `VITE_RENDER_SKALA` / `_MAKS` / `_HALUS` | auto / 2 / true | PIXI dibuat tanpa `resolution` |
| `VITE_PANGGUNG_*` | `min(90vh,100%)` | ukuran panggung diatur CSS statis |
| `VITE_WAJAH_*` (9 ekspresi) | ada | hanya dipakai via `store.expression` → `model.expression(nama)` — kebetulan cocok karena nama berkas sama |
| `VITE_POSE_*` (kacamata, jaket, tangan 1–4, tanda air) | ada | **`setPose()` adalah fungsi kosong** (`stage-ui-live2d/src/index.ts:19`) |
| `VITE_GERAK_*` (berubah, siklus, tidur) | ada | hanya `startMotion('isyarat', 2, 1)` di `App.vue:23`; indeks 2 di-hardcode |
| `VITE_KEADAAN*` (mesin diam/bicara/tidur) | ada | **tidak ada implementasi** — karakter diam total setelah gerakan pembuka |
| `VITE_KEDIP` / `NAPAS` / `IKUTI_KURSOR` | true | `autoInteract:false` dan tidak ada `focus()` — mata tidak mengikuti kursor |
| `VITE_IRAMA_*` (hemat GPU) | true | tidak ada |
| `VITE_TEKSTUR` | 8192 | tidak ada |
| `VTUBER_RVC_TRANSPOSE` | `9` | **`App.vue:34` memakai `transpose: 10`** |

Dua baris terakhir itu contoh paling jelas: Anda menyetel angka dengan teliti di
`.env` (lengkap dengan catatan hasil ukur F0), lalu kodenya memakai angka lain
yang diketik langsung di sumber.

---

## P1 — Mode "pet" (jendela melayang) belum dipindahkan ke TypeScript

`.env` meminta `VTUBER_TAMPAK=pet`, `VTUBER_PET_SEMBUNYI=layar-penuh`,
`VTUBER_PET_TRAY=ya`, `VTUBER_PET_HOTKEY=ctrl+shift+s`. README bahkan
mengklaim *"jendela pet transparan, always-on-top, tembus klik"*.

Kenyaataannya di `apps/stage-tamagotchi/src/main/index.ts`:

```ts
window = new BrowserWindow({ width: 1180, height: 760, ... show: false,
  backgroundColor: '#080b12', title: 'Silver Wolf', ... })
```

- tidak ada `transparent`, `frame: false`, `alwaysOnTop`, `setIgnoreMouseEvents`
- `config.tampak`, `config.petSembunyi`, `config.petTray`, `config.petHotkey`
  **tidak direferensikan di mana pun** (sudah saya grep — nol hasil di luar
  `core-config`)
- hotkey di-hardcode `CommandOrControl+Shift+S` (baris 48)
- yang jalan: jendela biasa 1180×760 + tray + close-to-tray

Jadi "pet Vtuber di desktop" saat ini = jendela aplikasi biasa berjudul
*Silver Wolf* dengan chat dua kolom.

---

## P1 — Rute aset `/models/` dan `/assets/` tidak pernah terisi

`packages/server-runtime/src/http.ts` menyajikan:

- `/models/*` → `assetRoot/live2d/*`
- `/assets/*` → `assetRoot/*`

dengan `assetRoot` bawaan `join(akar,'assets')`. Isi `assets/` Anda saat ini
**hanya** `voices/silverwolf/model.onnx`. Tidak ada `live2d/`, tidak ada
`encoders/`, tidak ada `piper/`.

Akibatnya:

- RVC **selalu** gagal mengambil `/assets/encoders/vec-768-layer-12.onnx`
  (`App.vue:34`) → jatuh ke suara Piper polos. Ini sudah tercatat di README
  ("Masih kurang untuk Fase 4"), jadi bukan kejutan, tapi layar tidak pernah
  memberi tahu Anda — hanya fallback diam-diam di `BrowserVoicePipeline`.
- Di aplikasi **Electron terpasang**, `main/index.ts:34` membuat folder kosong
  `assets/live2d/silverwolf`, `assets/piper`, `model`, `bin/llama` di
  `userData` — semuanya kosong. Live2D bisa lolos lewat `staticRoot`
  (`out/renderer` menerima salinan `public/`), tetapi RVC dan Piper pasti mati.
- `scripts/verify-assets.mjs` memeriksa `assets/piper/...`, padahal Piper
  disajikan dari bundel Vite (`/piper`). Jadi `npm run assets:verify` selalu
  melaporkan "✗ Piper model" meski aplikasinya bisa berbicara — sinyal yang
  menyesatkan.

---

## P2 — Tidak ada lip-sync, tidak ada gerakan berkelanjutan

- `setMouth()` sudah diimplementasi (`stage-ui-live2d/src/index.ts:21`) tetapi
  **tidak pernah dipanggil**. Tidak ada `AnalyserNode` pada `playing` audio di
  `App.vue`. Karakter bicara dengan mulut tertutup.
- `setPose()` kosong → kacamata/jaket/ganti wajah/tanda air tidak bisa
  dinyalakan.
- Tidak ada mesin keadaan `VITE_KEADAAN`: setelah `startMotion('isyarat',2,1)`
  di `onMounted`, karakter tidak melakukan apa pun sampai Anda mengirim pesan.

---

## P2 — Bug-bug kecil lain

| # | Lokasi | Masalah |
|---|---|---|
| 1 | `core-config/src/index.ts:216` | Nama field `ttsBatasaDetik` — salah ketik (huruf "a" ekstra). Kunci env `VTUBER_TTS_BATAS_DETIK` benar, jadi tidak berdampak fungsional, tapi membingungkan. |
| 2 | `core-config/src/index.ts:167` | `bacaMotionIndeks` bawaan `() => 0`. Di pemakaian nyata **semua** `VITE_GERAK_*` dapat `indeks: 0`, jadi selalu memutar `berubah-1` — konfigurasi `berkas=` diabaikan. Hanya tes yang meng-injeksi fungsi ini. |
| 3 | `server-runtime/src/bootstrap.ts:25` | `model: cariModel(config) ?? 'gguf'` mengirim **path absolut** sebagai nama model OpenAI. llama.cpp saat ini menolerannya, tapi rapuh. Seharusnya `-a <alias>` di `LlamaServerProcess` + alias itu yang dikirim. |
| 4 | `server-runtime/src/llama-process.ts:20` | `cariModel()` fallback memilih `.gguf` **pertama yang ditemukan** di `model/` (urutan `readdir`). Begitu MiniCPM selesai diunduh dan Anda menghapus/menambah berkas, model yang dipakai bisa berubah tanpa peringatan. |
| 5 | `server-runtime/src/http.ts:113` + `listen(...,'0.0.0.0')` | Sidecar LLM terbuka ke seluruh LAN, `access-control-allow-origin: *`, tanpa autentikasi. Untuk aplikasi lokal seharusnya `127.0.0.1`. |
| 6 | `server-runtime/src/llama-process.ts:44` | Bila llama-server sudah berjalan di port `port+1` (proses sisa), health langsung `ok` → **dua server** berebut GPU. Tidak ada deteksi "sudah hidup". |
| 7 | `server-runtime/src/bootstrap.ts:24` | Kegagalan boot llama-server hanya `console.warn`; `/api/health` tetap `ok:true` dan error baru muncul saat chat. UI menampilkan "TERHUBUNG" padahal model mati. |
| 8 | `server-runtime/src/http.ts:81` | `maxTokens: 512` dan `temperature: 0.7` di-hardcode, tidak ada `stop`. |
| 9 | `App.vue:30` | TTS baru mulai **setelah** balasan lengkap. `VTUBER_TTS_PER_KALIMAT=true` tidak diimplementasikan — jeda panjang sebelum suara. |
| 10 | `App.vue:35-36` | `playing` tidak dihentikan saat pesan baru dikirim; audio lama bisa tumpang tindih. `URL.revokeObjectURL` hanya terjadi di `onended` (bocor kalau di-pause). |
| 11 | `pipelines-audio/src/browser-rvc.ts:11-20` | `estimateF0` O(frame × 289 lag × 1024 sampel) berjalan **di main thread**. Kalimat 10 detik ≈ 300 juta operasi → UI membeku beberapa detik. Butuh Web Worker. |
| 12 | `packages/audio/src/index.ts:20` | `ScriptProcessorNode` sudah deprecated (masih jalan, tapi diganti `AudioWorklet`). |
| 13 | `git status` | `pnpm-workspace.yaml`, `.npmrc`, dan 12 `package.json` workspace **terhapus** tapi belum di-commit. Proyek sudah berubah jadi npm single-package; README masih menyebut "npm + Turborepo" dan `turbo.json` masih ada. |

---

## Ganti model ke MiniCPM5-2B-GGUF — yang harus diubah

Informasi dari kartu model HF + `OpenBMB/MiniCPM/docs/deployment/llama_cpp.md`,
dan sudah saya cocokkan dengan build `bin/llama/llama-server.exe` Anda
(verifikasi: `llama-server --help`).

### 1. Pilih kuantisasi

| Berkas | Ukuran | Catatan |
|---|---|---|
| `MiniCPM5-2B-Q4_K_M.gguf` | **1,56 GB** | yang saya sarankan; lebih kecil & lebih cepat dari Llama-3.2-3B Q4_K_M (2,0 GB) |
| `MiniCPM5-2B-Q8_0.gguf` | 2,68 GB | |
| `MiniCPM5-2B-F16.gguf` | 5,04 GB | tidak perlu untuk kasus ini |

### 2. `LlamaServerProcess` harus menambah flag

Perintah resmi:

```bash
llama-server -m MiniCPM5-2B-Q4_K_M.gguf -a MiniCPM5-2B --port 8080 -ngl 99 -c 8192 --jinja \
             --min-p 0 --top-p 0.95 --temp 0.7 -rea off
```

Yang **wajib** ditambahkan ke `packages/server-runtime/src/llama-process.ts:38`:

| Flag | Alasan |
|---|---|
| `--min-p 0` | **Paling penting.** llama.cpp bawaan `min_p=0.05`; dokumentasi OpenBMB memperingatkan ini membuat MiniCPM5 **mengulang-ulang kalimat**. |
| `-rea off` (`--reasoning off`) | MiniCPM5 punya mode *thinking*. Kalau aktif, balasan diawali blok `<think>…` — dan `bacaTagAwal()` (`core-character/src/tags.ts`) **tidak akan menemukan tag emosi** di awal → raut wajah tidak pernah berubah **dan** blok `<think>` ikut tampil di chat. |
| `-a MiniCPM5-2B` | alias model, supaya field `model` di request tidak perlu berisi path absolut (lihat bug #3 di atas). |
| `--jinja` | bawaan build Anda sudah *enabled*, tapi sebaiknya ditulis eksplisit. |

Flag yang sudah ada (`-m`, `--host`, `--port`, `-c`, `-t`, `-ngl`, `--flash-attn`)
tidak perlu diubah.

### 3. Sampling di sisi klien

`http.ts:81` mengirim `{ model, messages, stream, max_tokens, temperature }`.
Karena `--min-p`/`--top-p`/`-rea` sudah jadi bawaan server, request tidak perlu
diubah. Yang perlu dipertimbangkan:

- `temperature: 0.7` cocok dengan baris "No-think" di dokumen (0,7 – 0,9).
- `maxTokens: 512` — kalau `-rea off` tidak dipasang, 512 token habis di dalam
  blok `<think>` dan balasan kosong.

### 4. Perubahan `.env`

```diff
-VTUBER_LOCAL_MODEL_PATH=model/Llama-3.2-3B-Instruct-Q4_K_M.gguf
+VTUBER_LOCAL_MODEL_PATH=model/MiniCPM5-2B-Q4_K_M.gguf
```

Biarkan `VTUBER_LLM_PROVIDER=vulkan`, `VTUBER_LOCAL_MODEL_CTX=8192`,
`VTUBER_VULKAN_NGL=99`. (Model ini sanggup 128K, tapi 8192 sudah cukup untuk
persona + riwayat 24 pesan dan jauh lebih hemat VRAM.)

### 5. Peringatan penting

**MiniCPM5 adalah model yang berpusat pada bahasa Mandarin/Inggris.** Kartu
modelnya hanya mendaftar EN + ZH; Bahasa Indonesia tidak disebut. Persona Anda
(`silver_wolf_memory/persona.md`) dan seluruh prompt sistem
(`core-character/src/prompt.ts`) berbahasa Indonesia, dan instruksi
"awali dengan tag emosi `[senyum]`" menuntut kepatuhan format yang tinggi.

Risiko yang saya perkirakan:

- kualitas/maskesin bahasa Indonesia turun dibanding Llama-3.2-3B
- bisa menyisipkan aksara Mandarin
- kepatuhan pada aturan "tag di paling depan" lebih lemah → raut wajah sering
  tidak berubah (mekanismenya *fail-soft*, jadi hanya terasa sebagai "dia
  datar terus")

Saran: **jangan hapus Llama-3.2-3B dulu** — simpan keduanya di `model/`,
pilih lewat `VTUBER_LOCAL_MODEL_PATH`, bandingkan. Kalau tujuannya memang
kemampuan bahasa Indonesia yang lebih baik, **Qwen3-4B** (atau 1,7B/8B) adalah
pilihan yang jauh lebih tepat daripada MiniCPM5: ukuran setara, multibahasa
jauh lebih kuat, dan sudah punya dukungan llama.cpp yang matang.

### Hasil uji nyata (21:00–21:05, mesin ini)

Dijalankan persis dengan argumen yang dihasilkan `LlamaServerProcess`:

```bash
bin/llama/llama-server.exe -m model/MiniCPM5-2B-Q4_K_M.gguf -a MiniCPM5-2B \
  --host 127.0.0.1 --port 18899 -c 8192 -t 4 -ngl 99 --flash-attn on \
  --jinja --min-p 0 --top-p 0.95 -rea off
```

| Yang diuji | Hasil |
|---|---|
| Boot | **siap < 3 detik**, model 1,56 GB terbaca; `/health` → `{"status":"ok"}` |
| Blok `<think>` | **tidak muncul** — `-rea off` bekerja |
| Tag emosi di awal | **dipatuhi** — dua dari dua balasan diawali `[netral]` / `[lelah]` |
| Pengulangan kalimat | **tidak terjadi** — `--min-p 0` bekerja |
| Kecepatan | **~10 token/detik** (96 token = 9,5 s) |
| Perangkat | `Vulkan0: Intel(R) Iris(R) Xe Graphics (8083 MiB)` |

**Kualitas bahasa Indonesia — ini masalah nyata.** Contoh balasan:

> `[lelah] Saya tidak bisa menekinkan zoom karakter live2d karena itu adalah
> kemampuan sifat teknologi, dan perbaikinya membutuhkan pemahaman teknis yang
> kami milik.`

Tata bahasanya rusak ("menekinkan", "kemampuan sifat teknologi"), memakai
"Saya"/"kami" padahal persona mewajibkan **"gw"**, dan isinya mengelak. Balasan
pertama juga memakai kosakata Jawa ("nggoleki") yang tidak pernah ada di persona.
Jadi mekanismenya jalan, tetapi karakternya akan terasa "aneh" — persis risiko
yang diperkirakan sebelumnya.

**Catatan kecepatan:** ~10 tok/detik itu lambat untuk percakapan (satu balasan
2–3 kalimat ≈ 10–20 detik). Mesin ini hanya 4 prosesor dan GPU-nya Iris Xe
terintegrasi. Saya sudah menguji `-ngl 0` (CPU murni, `-t 8`): **lebih lambat
lagi** — tidak sanggup menyelesaikan 150 token dalam 60 detik. Jadi
`VTUBER_VULKAN_NGL=99` tetap pilihan yang benar; satu-satunya jalan mempercepat
adalah model yang lebih kecil (Qwen3-1,7B) atau kuantisasi lebih agresif.

---

## Urutan perbaikan yang saya sarankan

1. ~~**Zoom** — ganti `model.width` → `model.internalModel.width`, pakai
   `model.anchor`, pasang `renderer.on('resize')`, lepas listener di
   `destroy()`.~~ **SELESAI 21:25**, lengkap dengan tes regresi.
2. ~~**Sambungkan konfigurasi** — suntikkan `VITE_*` lewat `define`/`window.__VTUBER_ENV__`
   (gunakan `envWeb()` yang sudah ada), lalu baca `VITE_AVATAR_ZOOM`,
   `VITE_AVATAR_X`, `VITE_MODEL_URL`, `VITE_RVC_TRANSPOSE`.~~ **SELESAI 21:20**
   (`envDir` di kedua vite config, `bacaPanggung()`).
3. ~~**MiniCPM5** — tambahkan `--min-p 0`, `-rea off`, `-a` di
   `llama-process.ts`, ganti path di `.env`, uji banding dengan Llama.~~
   **SELESAI 21:05**.
4. ~~**Lip-sync + gerak** — `AnalyserNode` → `setMouth()`, dan mesin keadaan
   `VITE_KEADAAN` sederhana (diam/siklus → bicara → tidur).~~ **Lip-sync SELESAI
   22:00** (`AntreanSuara.gerakkanMulut`). Mesin keadaan `VITE_KEADAAN` **belum**.
5. **Mode pet Electron** — `frame:false`, `transparent`, `alwaysOnTop`,
   `setIgnoreMouseEvents`, hotkey & `petSembunyi` dari config.
6. **Kebersihan** — `localStorage`-free: rapikan `assets/` (isi `live2d/` &
   `encoders/`), perbaiki `verify-assets.mjs`, commit penghapusan workspace pnpm.

---

## P0 lanjutan — TTS per-kalimat (SELESAI 22:05)

Butir 1-3 backlog optimisasi. Angka diukur dengan `llama-server` langsung
(`-ngl 99 --flash-attn on -c 8192 -t 4`), skrip `out/ukur-p0.mjs`.

### Yang dibangun

| Berkas | Isi |
|---|---|
| `packages/pipelines-audio/src/kalimat.ts` | `potongKalimat()`, `buangTag()` — fungsi murni |
| `packages/pipelines-audio/src/antrean.ts` | `AntreanSuara` — pemutar antrean + lip-sync RMS |
| `packages/stage-ui/src/store.ts` | `send(teks, { onDelta })` |
| `apps/stage-web/src/App.vue` | `submit()` mengalirkan teks ke antrean selagi LLM menulis |

### Hasil ukur

| | rata-rata (3 pertanyaan) |
|---|---|
| Kalimat pertama utuh | **2,58 dtk** |
| Aliran selesai | **6,69 dtk** |
| **Hemat dari sisi teks** | **4,11 dtk** |

Latency total dulu = `aliran selesai` + `Piper+RVC`. Sekarang =
`kalimat-1` + `Piper+RVC` → penghematan nyata **lebih besar** dari 4,11 dtk,
karena Piper+RVC (8-20 dtk) dulu juga menunggu aliran selesai.

### Tiga bug yang ditemukan saat pengukuran

1. **Ambang gabung terlalu agresif.** `minimal = 12` menahan SETIAP kalimat
   pendek, sehingga `'Satu hal. Dua hal!'` tetap jadi satu potongan — P0 jadi
   tidak berguna. Sekarang kalimat hanya ditahan kalau pendek **dan** hanya
   satu kata.
2. **Daftar bernomor & angka utuh.** `'1. Pertama.'` sempat terpotong jadi
   potongan 2 huruf. `bukanAkhirKalimat()` ditulis ulang: desimal (`3.14`),
   daftar (`1. `), dan angka utuh di akhir kalimat (`100.`) kini dibedakan.
3. **Tag kendali bocor ke TTS.** Dengan persona cadangan pendek, MiniCPM5
   menaruh tag di **tengah** — `'Wajib [senyum].'` — sehingga Piper membacakan
   kata "senyum". `buangTag()` menyaring semua `[...]` SEBELUM dipotong, dan
   tidak menyentuh kode (`array[0]`, `item[1]` tetap utuh).

### Catatan penting: tag & persona

Ternyata **persona asli dipatuhi**: dengan `silver_wolf_memory/persona.md`
(6.276 byte), tag keluar **di awal 8/8 kali**. Tag tengah-kalimat tadi hanya
muncul karena uji pertama memakai `PERSONA_CADANGAN` satu baris
(`bootstrap.ts:10`). Jalur streaming sebenarnya aman; `buangTag()` tetap
dipasang sebagai jaring pengaman kalau model mengganti kebiasaan.

### Kualitas bahasa MiniCPM5 — batas keras

Dengan persona asli pun keluaran Indonesianya sering **rusak bercampur Jawa**:

```
"Langit kadai karo cukup, gw nggoleki nggoleki."
"RAM itu data yang tersimai saat puncak."
"Gunakake kabel listr..."
```

Ini batas model (MiniCPM5-2B resminya EN + ZH), bukan bug kode, dan **tidak
bisa diperbaiki dari sisi prompt**. Kalau kualitas bahasa Indonesia penting,
Qwen3 tetap perlu diuji — atau model yang dilatih untuk bahasa Indonesia.
