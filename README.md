# Silver Wolf — WinUI 3 (Windows App SDK)

Aplikasi desktop native Windows untuk **Silver Wolf**, pendamping AI/VTuber lokal.
Ini adalah hasil migrasi dari `../AI Vtuber Web/` (Electron + Vue 3 + server Node)
ke WinUI 3 dengan C#/.NET, XAML, dan pola MVVM.

## Status migrasi saat ini

> Tabel ini **ringkas saja**. Acuan lengkapnya `docs/PROYEK.md` §2 — kalau
> keduanya berbeda, yang menang adalah `docs/PROYEK.md`.

| Milestone | Isi | Status |
|---|---|---|
| **M0** | Toolchain WinUI 3 terbukti menghasilkan aplikasi yang berjalan | Selesai |
| **M1** | Bersihkan template WinUI nyasar di `AI Vtuber Web/` | Selesai |
| **M2** | Skeleton solusi (App / Core / Services / Tests) | Selesai |
| **M3** | Prasyarat C++ (Windows SDK, MSVC, Cubism SDK 5 R5) | Selesai |
| **M4** | Peta fitur + `SilverWolf.Core`: config, teks, domain | Selesai |
| **M5** | `KizunaEngine`, `CharacterVault`, `TieredMemoryEngine` | Selesai |
| **M6** | `OpenAiCompatibleProvider`, `LlamaServerProcess`, health | Selesai |
| **M7** | `AgentService`, `CompanionBackend`, `CompanionRuntime`, 108 unit test | Selesai |
| **M8** | Cubism native: render, fit, fokus, motion, efek hidup | **Berjalan** — sisa: LipSync |
| **M9** | `MainWindow`, `CompanionViewModel`, 4 view, `AssetLocator` | Selesai |
| M10 | Tema, blur, animasi, font | Belum |
| M11 | TTS: phonemizer, Piper, NAudio, lip-sync | **Rantai terbukti jalan** (`tools/tts/`); sisa integrasi |
| M12 | Tray, hotkey, single-instance, close-to-tray | Belum |
| M13 | Integrasi fitur end-to-end | Belum |
| M14 | Rilis | Belum |

⚠️ **Cacat aktif yang memblokir segalanya:** aplikasi **mati senyap** —
direproduksi 2 dari 2 peluncuran pada 8 Okt 2026. Rincian di
`docs/LAPORAN-MASALAH.md` §1.1 dan `docs/PROYEK.md` §8.1. Bukti:
`tools/bukti/mati-saat-inferensi-2026-10-08.log`.

## Prasyarat

### Sudah terpenuhi
- .NET SDK 10.0.401 (atau lebih baru). SDK 11 RC juga sudah diuji berhasil.
- Paket NuGet: WindowsAppSDK 2.5.1, Windows SDK BuildTools, NAudio — sudah ter-restore.

### Belum terpenuhi (MEMBLOKIR M3)

M3 tidak bisa dimulai sebelum ketiganya selesai:

1. **Windows SDK belum terpasang.**
   `C:\Program Files (x86)\Windows Kits\10\` hanya berisi `UnionMetadata`,
   tidak ada `Include\`, `Lib\`, maupun `bin\`.
2. **Toolset MSVC tidak lengkap.**
   `...\VC\Tools\MSVC\14.51.36231\` hanya punya `bin`, `lib`, `Auxiliary` —
   folder `include` tidak ada.
   → Untuk 1 dan 2: buka Visual Studio Installer, tambahkan workload
     **"Desktop development with C++"** (atau komponen **Windows 11 SDK 10.0.26100+**).

   Build C# saat ini **tidak** terpengaruh; ia memakai
   `Microsoft.Windows.SDK.NET.Ref` dari NuGet. Yang tidak bisa dibangun
   hanyalah C++/WinRT.

3. **Cubism SDK for Native belum ada.**
   Yang tersedia hanya `../AI Vtuber Web/public/live2dcubismcore.min.js`, yaitu
   Cubism Core **Web/WASM** — artefak yang berbeda.
   → Unduh Cubism SDK for Native dari live2d.com **di bawah lisensi Live2D**.
     Core dikirim sebagai static library (`Live2DCubismCore_MT.lib`), bukan DLL.
     Jangan di-commit; tunjuk lewat variabel lingkungan `CUBISM_SDK_DIR`
     (lihat `build/CubismSdk.props`).

## Struktur

```
SilverWolf.sln
Directory.Build.props            properti bersama + impor build/CubismSdk.props
build/CubismSdk.props            penunjuk $(CubismSdkDir), bukan isi SDK-nya
docs/PROYEK.md                    DOKUMEN TUNGGAL proyek (mulai dari sini)
docs/migrasi/                    01-peta-fitur · 02-dependensi · 03-langkah-migrasi
docs/arsip/                      dokumen lama, memori web, sumber web
tools/                           potret-jendela.py (pemotret jendela)
src/
  SilverWolf.App/                WinUI 3 exe (unpackaged, WASDK self-contained)
    Views/ ViewModels/ Models/ Configuration/ Native/ Diagnostics/
  SilverWolf.Core/               net8.0, bebas WinRT, bisa di-unit-test
    Configuration/               EnvFile, EnvSource, AppConfig
    Text/                        EmotionParser, SentenceSplitter, TagSkipper
    Domain/                      Mood, PersonaComposer, CharacterVault,
                                 ChatHistory, ProactiveDirector,
                                 TieredMemoryEngine, Kizuna/
  SilverWolf.Services/           net8.0-windows — llama, inference, audio
    Configuration/               AppPaths
    Inference/                   ILlmProvider, OpenAiCompatibleProvider, StubProvider
    Llama/                       ModelLocator, LlamaServerProcess
    Agent/                       AgentService
    Backend/                     CompanionBackend (pengganti rute HTTP)
    Bootstrap/                   CompanionRuntime (pengganti main.js)
native/
  SilverWolf.Live2D/             C++/WinRT WRC (belum dibuat)
  SilverWolf.Phonemizer/         C++ DLL phonemizer (belum dibuat)
  third_party/                   GITIGNORED: Cubism SDK, piper-phonemize
tests/SilverWolf.Core.Tests/     xunit (105 uji)
```

Konvensi penamaan: **identifier C# memakai bahasa Inggris**, tetapi **nilai
string yang dilihat pengguna tetap Indonesia** (`"siap"`, `"memuat"`,
`"tidak-jalan"`, `"pet"`, `"stranger"`). Nilai-nilai itu ikut menentukan
perilaku prompt LLM, jadi jangan diterjemahkan.

## Membangun dan menjalankan

```bash
dotnet build SilverWolf.sln -p:Platform=x64 -c Debug

# Aplikasi unpackaged, jadi bisa dijalankan langsung:
./src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/SilverWolf.App.exe

dotnet test tests/SilverWolf.Core.Tests/SilverWolf.Core.Tests.csproj
```

Rilis: **x64 saja**. Build llama.cpp Vulkan di `bin/llama/` khusus x64, sehingga
ARM64 tidak bisa menjalankan LLM lokal.

## Tata letak aset (mesin-lokal, tidak ikut repo)

Aset 6,9 GB dipindahkan dari `../AI Vtuber Web/` pada 2026-10-07. Semuanya
diabaikan `.gitignore` — sebagian karena ukurannya, sebagian karena lisensi.

```
model/gemma-4-E4B-it-UD-Q4_K_XL.gguf   4,8 GB  model LLM
assets/piper/                           61 MB  model suara Piper (TTS aktif, M11)
assets/whisper/                        605 MB  model STT
assets/rvc/ + assets/voices/           329 MB  model RVC (saat ini tidak berfungsi)
assets/model-dasar/                    699 MB  pembantu RVC (berkas .pt = PyTorch)
assets/live2d/silverwolf/               19 MB  model Live2D  ← TIDAK boleh disebar (lisensi)
bin/llama/                              91 MB  binary llama.cpp Vulkan
silver_wolf_memory/                    130 KB  data pribadi: persona, Fakta, Mood, Riwayat
.env                                    5 KB  konfigurasi
```

**Ikut repo (kecil, wajib):**

```
assets/fonts/                          400 KB  3 variable TTF — Plus Jakarta Sans,
                                                JetBrains Mono, Orbitron
assets/ikon/                             3 KB  ikon aplikasi Silver Wolf
docs/arsip/sumber-web/                 740 KB  sumber aplikasi lama (referensi M9–M11)
docs/arsip/memori-web/                  84 KB  catatan pengembangan aplikasi lama
```

Font sengaja dibundel: aplikasi lama mengambilnya dari `fonts.googleapis.com`
saat runtime, dan aplikasi desktop harus bisa jalan offline. Lisensi SIL OFL 1.1
mengizinkan pembundelan. Rincian cara mengunduh dan risiko variable font ada di
`assets/fonts/README.md`.

`AppPaths.TentukanAkar()` menemukan akar lewat `SilverWolf.sln`, lalu
`AppPaths.Periksa()` memeriksa kelengkapan aset. Hasilnya dicatat ke
`crash.log` setiap kali aplikasi dijalankan, jadi aset yang hilang langsung
terlihat:

```
TAHAP: akar = C:\…\AI Vtuber WINUI3
TAHAP:   OK     model LLM              …\model
TAHAP:   OK     model Live2D           …\assets\live2d\silverwolf
```

**Artefak WASM dari aplikasi lama sengaja tidak dipindah** karena mustahil
berjalan di aplikasi native: `public/onnx/ort-wasm-*.wasm` (83 MB) digantikan
`Microsoft.ML.OnnxRuntime`, `public/piper/piper_phonemize.wasm` (18 MB)
digantikan `native/SilverWolf.Phonemizer`, dan `live2dcubismcore.min.js`
digantikan Cubism SDK for Native.

## Keputusan arsitektur yang sudah dikunci

| Area | Keputusan |
|---|---|
| Live2D | Native: C++/WinRT WRC membungkus Cubism Native Framework, render ke `SwapChainPanel` (D3D11). Tanpa WebView2. |
| Backend | Port penuh ke C# — `apps/server-node` tidak dipakai lagi saat runtime. |
| TTS / ONNX Runtime | **Dipisah ke proses terpisah.** Lihat di bawah. |
| Packaging | Unpackaged + WASDK self-contained. Lihat di bawah. |
| Bootstrap WASDK | **`WindowsAppSdkBootstrapInitialize=false`.** Jangan diaktifkan selama `WindowsAppSDKSelfContained=true` — memuat dua runtime sekaligus dan menyebabkan `STATUS_FAIL_FAST_EXCEPTION` tanpa pesan. Lihat `docs/PROYEK.md` §7.6. |

> ⚠️ **Jalankan aplikasinya, bukan hanya build.** WinUI 3 bisa gagal dengan
> fail-fast yang **tidak meninggalkan jejak apa pun** — tanpa stderr, tanpa
> entri Event Log, tanpa laporan WER, dan `try/catch` tidak menangkapnya.
> `src/SilverWolf.App/Diagnostics/CrashLog.cs` menulis jejak tahap startup ke
> `crash.log` di sebelah exe; itu alat diagnosis utama untuk kasus seperti ini.

### Mengapa TTS/ONNX Runtime dipisah ke proses sendiri

Ini temuan dari M2, bukan pilihan gaya. Dipakai `Microsoft.ML.OnnxRuntime` di
dalam proyek aplikasi, build gagal dengan:

```
error APPX1101: Payload contains two or more files with the same destination
path 'onnxruntime.dll'.
```

Penyebabnya lebih dalam daripada sekadar nama file yang bentrok. Pemeriksaan
metadata MSBuild menunjukkan dua paket yang sama-sama sah mengklaim
**identitas yang sama**:

| | `Microsoft.ML.OnnxRuntime` | `Microsoft.Windows.AI.MachineLearning` |
|---|---|---|
| native | `onnxruntime.dll`<br>`PathInPackage=runtimes/win-x64/native/onnxruntime.dll` | `onnxruntime.dll`<br>`PathInPackage=runtimes/win-x64/native/onnxruntime.dll` |
| managed | `Microsoft.ML.OnnxRuntime.dll` | `Microsoft.ML.OnnxRuntime.dll` |

Yang kedua ikut tertarik oleh `Microsoft.WindowsAppSDK` →
`Microsoft.WindowsAppSDK.ML` → `Microsoft.Windows.AI.MachineLearning`, sehingga
selalu ada di setiap aplikasi WinUI 3.

Penggantian nama file tidak menyelesaikannya: `PathInPackage` tetap sama.
Mengganti keduanya berarti menaruh dua ONNX Runtime berbeda di satu proses,
dengan versi berbeda, berbagi nama assembly yang sama — kegagalannya baru
muncul sebagai crash P/Invoke di runtime, bukan error build.

**Solusinya:** `SilverWolf.Services` **tidak** mereferensikan ONNX Runtime.
Piper TTS direncanakan berjalan di exe worker terpisah (`SilverWolf.TtsWorker`,
dipakai mulai M11) yang tidak menyertakan WindowsAppSDK. Payload MSIX aplikasi
tidak lagi bersentuhan dengan jalur ONNX Runtime sama sekali.

Efek sampingnya menguntungkan: sintesis Piper tidak lagi berebut UI thread
maupun perangkat D3D11 milik Cubism, dan TTS bisa di-restart tanpa mematikan
aplikasi.

### Mengapa unpackaged + self-contained

Build pertama sukses tetapi aplikasi gagal start:

```
COMException (0x80040154): Class not registered (REGDB_E_CLASSNOTREG)
at ...DeploymentManagerCS.AutoInitialize.AccessWindowsAppSDK()
```

Windows App SDK Runtime belum terpasang. Dengan
`WindowsAppSDKSelfContained=true`, runtime ikut ke folder output sehingga
aplikasi berjalan tanpa prasyarat instalasi. Ini juga bentuk distribusi yang
tepat untuk aplikasi offline beraset 7 GB.

## Batasan yang diketahui

- **ARM64**: UI bisa dibangun, tetapi LLM lokal tidak. Build llama.cpp Vulkan di
  `AI Vtuber Web/bin/llama/` hanya x64.
- **`PublishTrimmed=False`** — trimming + XAML/WinRT adalah sumber kerusakan
  yang sudah dikenal. Jangan diaktifkan.
- **Live2D**: file model tidak boleh didistribusikan ulang sebagai file lepas
  (lisensi Live2D). Dimuat saat runtime dari folder aset pengguna.
- **Windows minimum**: `10.0.17763` (RS5). Mica memerlukan Windows 11 22000,
  jadi backdrop harus di-feature-detect.
- **Live2D native**: dibangun lewat `bash native/SilverWolf.Live2D/build-cli.sh`
  karena `MSBuild.exe` diblokir kebijakan. Detail di `docs/PROYEK.md` §10.

## Rujukan

| Dokumen | Isi |
|---|---|
| **`docs/README.md`** | **Indeks seluruh dokumentasi** — mulai dari sini kalau bingung harus membaca apa |
| **`docs/PROYEK.md`** | **DOKUMEN TUNGGAL proyek.** Prompt siap tempel untuk sesi AI berikutnya, status milestone, perintah build/uji/jalankan, angka golden, kontrak perilaku, seluruh masalah nyata + penyelesaiannya, cacat terbuka, alat diagnosis, jebakan toolchain & API Cubism 5, konvensi kode, dan peta migrasi |
| **`docs/LAPORAN-MASALAH.md`** | Prioritas: apa yang rusak, tingkat, sebab, dampak, urutan tindak lanjut |
| **`tools/tts/README.md`** | Rantai suara (TTS Indonesia + RVC SilverWolf): cara pakai, profil suara terkunci, tuning, latensi |
| `docs/migrasi/01-peta-fitur.md` | Pemetaan layar, panel, state, endpoint → padanan C#; fitur yang tidak bisa dipindah 1:1 beserta alternatifnya |
| `docs/migrasi/02-dependensi.md` | Tabel penggantian/penghapusan dependensi pihak ketiga, alasan `APPX1101`, batasan platform Windows, kompatibilitas berkas data |
| `docs/migrasi/03-langkah-migrasi.md` | Langkah per proyek, status, dan daftar blocker |
| `docs/arsip/` | Dokumen lama, memori pengembangan aplikasi web, dan sumber lengkap aplikasi web. **Arsip — jangan dipakai sebagai acuan** |

## Paritas dengan aplikasi lama

Aplikasi lama (`../AI Vtuber Web/`) **sudah dihapus** — asetnya dipindah ke sini
dan sumbernya diarsipkan ke `docs/arsip/sumber-web/`. Data golden kizuna sudah
diambil lebih dulu dan tersimpan di
`tests/SilverWolf.Core.Tests/TestData/golden/`.

~~Yang masih tertahan: data golden `phoneme_ids`~~ — **tidak lagi tertahan
sejak 2026-10-08.** Paket `piper-tts` membawa phonemizer espeak-ng-nya sendiri,
jadi `piper_phonemize.wasm` tidak dibutuhkan. Rantai suaranya kini berjalan;
lihat `tools/tts/README.md`.

**Suara:** Piper `id_ID-news_tts-medium` (Indonesia) + RVC v2 `SilverWolfJP`.
Profil suara yang dipakai terkunci di `.env` dan hasil acuannya
`tools/tts/contoh/04-transpose-3.wav`.
