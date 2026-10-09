# Silver Wolf — Dokumen Proyek

**Satu-satunya dokumen acuan proyek.** Menggantikan `RIWAYAT-MIGRASI.md`,
`PROMPT-LANJUTAN.md`, `PROGRES-LIVE2D.md`, `PROMPT-LANJUTAN-LIVE2D.md`, dan
`SERAH-TERIMA-M8.md` — kelimanya sudah dipindahkan ke `docs/arsip/dokumen-lama/`
dan tidak lagi diperbarui.

**Terakhir diperbarui:** 2026-10-08 01:00
**Status ringkas:** M0–M2, M4–M9 **selesai**. M8 (Live2D native) **berjalan** —
model tampil, bergerak, bernapas, berkedip, ikut kursor; 9 ekspresi dan 4
gerakan terverifikasi berfungsi. **Rantai suara sudah terbukti berjalan** —
TTS Indonesia (Piper) + RVC SilverWolf menghasilkan audio nyata, tetapi masih
sebagai skrip Python di `tools/tts/` dan **belum tersambung ke aplikasi**.
M10, M12–M14 belum.

| | |
|---|---|
| Aplikasi target | `C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\` |
| Aplikasi sumber | Electron + Vue 3 + server Node — **sudah dihapus**; aset & dokumen sudah dipindah, sumber diarsipkan ke `docs/arsip/sumber-web/` |
| Verifikasi terakhir | build x64 **0 error / 0 warning** · **114 unit test lulus** · aplikasi dijalankan dan model Live2D tampil · **rantai suara terbukti** (`tools/tts/`, hasil acuan `contoh/04-transpose-3.wav`) · **M11 tersambung** — memutar audio sungguhan, `crash.log` memuat `tts: siap` |
| ⚠️ Cacat aktif | aplikasi **mati senyap** — **tiga mode** teridentifikasi (A: kompilasi shader, **terbukti INTERMITEN**, B: memori habis, C: saat Vulkan mulai inferensi). §8.1 |

---

## 0. Blok tempel untuk sesi AI berikutnya

```
Lanjutkan proyek "Silver Wolf" (WinUI 3) di
C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\

BACA DULU: docs/PROYEK.md (dokumen tunggal, seluruh konteks ada di sana).
Indeks seluruh dokumentasi: docs/README.md
Dokumen lama sudah diarsipkan di docs/arsip/dokumen-lama/ — jangan dipakai.
JANGAN sentuh atau taut silver_wolf_memory/ — itu memori karakter, pulau
terpisah by design (aturan: silver_wolf_memory/_PETUNJUK.md).

STATUS: M0-M2, M4-M9 selesai. M8 berjalan (Live2D tampil + efek hidup).
        M10, M12-M14 belum.
        Rantai suara SUDAH TERBUKTI jalan sebagai skrip Python (tools/tts/):
        teks Indonesia -> Piper -> RVC SilverWolf. Belum tersambung ke aplikasi.
        M11 tinggal integrasi (TtsWorker + NAudio + LipSync), bukan riset lagi.

⚠️  CACAT AKTIF, BACA SEBELUM APA PUN: aplikasi MATI SENYAP, intermiten.
    DIUKUR ULANG 8-9 Okt. TIGA MODE, jangan disatukan:
      A) mati di dalam kompilasi shader Cubism (crash.log beku 13.449 bita,
         detik ke-13..19)
      B) mati karena memori habis (sisa RAM ~3 MiB, llama ikut mati, ~28 dtk)
      C) mati SAAT VULKAN MULAI INFERENSI — lama tidak terlihat; jalan-7 di
         tools/bukti/ mereproduksi keluhan Master persis: log putus di
         "launch_slot_: processing task" tanpa balasan apa pun.
    Fakta yang berlaku untuk ketiganya: exit code 0 = KELUAR BERSIH (bukan
    fail-fast); nol entri Event Log. Jadi proses DIHENTIKAN dari luar CLR.
    Yang sudah disingkirkan: fail-fast, exception, penutupan jendela,
    kehabisan memori (untuk A), dan llama-server (untuk A).
    HATI-HATI: kesimpulan lama "bukan rebutan GPU" itu benar untuk A/B tetapi
    Mode C justru mati tepat ketika Vulkan bekerja -> jangan digeneralisasi.
    GPU = Intel Iris Xe (terintegrasi, memori bersama RAM 16 GB, ~3,7 GB bebas).
    Baca docs/PROYEK.md §8.1 (termasuk pembaruan 2026-10-09) sebelum apa pun.
    Jangan bangun fitur di atas aplikasi yang belum bisa bertahan hidup.

PEKERJAAN SUARA (M11) — rantainya SUDAH JALAN, jangan diriset ulang:
  Resep lengkap + profil terkunci : tools/tts/README.md
  Hasil acuan (jangan diubah)     : tools/tts/contoh/04-transpose-3.wav
  PROFIL SUARA DIKUNCI di .env — jangan ubah tanpa izin Master:
    VTUBER_RVC_TRANSPOSE=-3 · VTUBER_RVC_INDEKS_LAJU=0.6 · VTUBER_RVC_F0=rmvpe
  .env adalah SATU-SATUNYA sumber kebenaran; tools/tts/*.py membacanya.
  Lingkungan Python (dua-duanya sudah siap, jangan pasang ulang):
    RVC  : "C:/Users/Daffa/Desktop/Folder Space AI/Folder Space Semester 6/voice changer3 glm/venv/Scripts/python.exe"
           (Python 3.10.11, torch 2.12.1+cpu, fairseq 0.12.2, rvc-python 0.1.5)
    Piper: "C:/Users/Daffa/.workbuddy-ai/binaries/python/envs/default/Scripts/piper.exe"
           (Python 3.13.14, piper-tts 1.8.0)
  Jebakan wajib: torch.load harus dipatch weights_only=False; nama parameter F0
  adalah f0method (bukan f0_method); index RVC bisa gagal dipakai diam-diam.

VERIFIKASI (lakukan sebelum menulis kode apa pun):
  dotnet build SilverWolf.sln -p:Platform=x64 -c Debug    -> 0 error, 0 warning
  dotnet test tests/SilverWolf.Core.Tests/...             -> 114 lulus
  # native (MSBuild.exe DIBLOKIR; dotnet build tak punya VCTargetsPath):
  bash native/SilverWolf.Live2D/build-cli.sh Release
  # C# — Platform=x64 DAN SelfContained WAJIB:
  dotnet build src/SilverWolf.App/SilverWolf.App.csproj \
    -c Debug -p:Platform=x64 -r win-x64 -p:SelfContained=true
  LALU JALANKAN. Build hijau tidak berarti aplikasi jalan.

ATURAN YANG TIDAK BOLEH DILANGGAR — semuanya sudah pernah memakan waktu:
  1. WindowsAppSdkBootstrapInitialize HARUS tetap false selama
     WindowsAppSDKSelfContained=true. Dua runtime WASDK = fail-fast senyap.
  2. BUILD, LALU JALANKAN. Fail-fast WinUI 3 tidak meninggalkan jejak apa pun;
     Diagnostics/CrashLog.cs + penanda tahap adalah alat diagnosis utama.
     Jangan pernah membuang penanda tahap itu.
  3. Jangan percaya build yang ada di folder keluaran — BANGUN ULANG dari
     sumber terkini sebelum menyimpulkan sebuah konfigurasi rusak. Kesimpulan
     "Debug mati karena stack overflow" bertahan lama hanya karena build-nya basi.
  4. Satu kali mati BUKAN bukti adanya regresi. Uji berulang dulu.
  5. Penanda akar repo = SilverWolf.sln. JANGAN pakai folder "assets" —
     Windows case-insensitive sehingga src/SilverWolf.App/Assets/ ikut cocok.
  6. XML tidak boleh memuat "--" di komentar. build/CubismSdk.props diimpor
     Directory.Build.props; satu salah tulis mematikan SELURUH solusi.
  7. PublishTrimmed harus tetap False. Rilis x64 saja.
  8. Identifier C# Inggris; nilai string yang dilihat pengguna tetap Indonesia
     ("siap", "memuat", "tidak-jalan", "stranger") — nilai itu ikut menentukan
     perilaku prompt LLM.
  9. Jangan commit aset berlisensi/besar. Model Live2D tidak boleh
     didistribusikan ulang sebagai berkas lepas.
 10. dotnet msbuild, reg.exe, Add-Type, dan New-Object -ComObject DIBLOKIR
     kebijakan keamanan. Verifikasi properti MSBuild lewat csproj probe.
 11. Jangan pernah menyalin runtimeconfig.json/deps.json antar folder keluaran.
 12. kill -0 $PID tidak bisa dipakai cek proses hidup — pakai
     tasklist //FI "IMAGENAME eq SilverWolf.App.exe".

Jangan tanya ulang keputusan yang sudah dikunci (Live2D native C++/WinRT tanpa
WebView2, backend port penuh ke C#, TTS di exe worker terpisah, unpackaged +
self-contained). Alasan lengkapnya ada di docs/PROYEK.md §6.
```

---

## 1. Tujuan dan keputusan yang dikunci

Memindahkan **Silver Wolf** — pendamping AI/VTuber lokal (Live2D, LLM lokal,
TTS, sistem ikatan hubungan) — dari aplikasi Electron ke aplikasi desktop
Windows native, **tanpa kehilangan perilaku yang dirasakan pengguna**.

| Area | Keputusan | Alasan |
|---|---|---|
| Live2D | **Native C++/WinRT** membungkus Cubism Native Framework, render D3D11 ke `SwapChainPanel`. **Tanpa WebView2.** | Permintaan user |
| Backend | **Port penuh ke C#.** Tidak ada sidecar Node saat runtime. | Permintaan user |
| TTS | ONNX Runtime + espeak-ng native + NAudio | Permintaan user |
| TTS/ONNX, lokasi | **Exe worker terpisah** (`SilverWolf.TtsWorker`), bukan in-proc | Paksa: tabrakan `onnxruntime.dll` dengan WindowsAppSDK.ML (`APPX1101`) |
| Packaging | **Unpackaged + WASDK self-contained** | Paksa: aset ~7 GB dan `llama-server` butuh folder yang bisa ditulis |

---

## 2. Status milestone

| Milestone | Isi | Status |
|---|---|---|
| M0 | Toolchain terbukti (`net8.0-windows10.0.19041.0` + WindowsAppSDK 2.5.1) | ✅ |
| M1 | Bersihkan template WinUI nyasar di `AI Vtuber Web/` | ✅ |
| M2 | Skeleton solusi (`SilverWolf.sln`) | ✅ |
| M3 | Prasyarat C++ (Windows SDK 10.0.28000, MSVC 14.51.36231, Cubism SDK 5 R5) | ✅ |
| M4 | Peta fitur + `SilverWolf.Core` (config, teks, domain) | ✅ |
| M5 | `KizunaEngine`, `CharacterVault`, `TieredMemoryEngine` | ✅ |
| M6 | `OpenAiCompatibleProvider`, `LlamaServerProcess` | ✅ |
| M7 | `AgentService`, `CompanionBackend`, `CompanionRuntime`, 108 uji | ✅ |
| M8 | Renderer Live2D native (render, fit, fokus, motion, efek hidup) | 🟢 **Berjalan** — sisa: LipSync |
| M9 | `MainWindow`, `CompanionViewModel`, 4 view, `AssetLocator` | ✅ |
| M10 | Tema, blur, animasi, font | ⬜ |
| M11 | TTS: phonemizer, Piper, NAudio, lip-sync | 🟢 **terintegrasi & terdengar** — sisa: LipSync (§8.2) |
| M12 | Tray, hotkey, single-instance, close-to-tray | ⬜ |
| M13 | Integrasi fitur end-to-end | ⬜ |
| M14 | Rilis | ⬜ |

### M11 — apa yang sudah dan belum

**Sudah (terbukti 2026-10-08).** Rantai lengkap teks Indonesia → suara Silver
Wolf berjalan: Piper `id_ID-news_tts-medium` (espeak voice `id`) → RVC v2
`SilverWolfJP`. Seluruh asetnya sudah ada di repo; tidak ada yang perlu diunduh.
Rincian, angka tuning, dan latensi ada di **`tools/tts/README.md`**.

Dua koreksi terhadap catatan lama:

1. **"Blocker: data golden `phoneme_ids`" sudah tidak relevan.** Paket
   `piper-tts` membawa phonemizer espeak-ng-nya sendiri, jadi phonemizer tidak
   perlu dibangun ulang dan `piper_phonemize.wasm` tidak dibutuhkan. Uji paritas
   phonemizer tetap boleh dilakukan, tetapi bukan lagi penghambat.
2. **`assets/encoders/` yang kosong bukan penghalang.** Dokumentasi lama
   menyebut hilangnya ContentVec ONNX sebagai sebab RVC mati di aplikasi web.
   Ternyata `rvc-python` memakai `hubert_base.pt` lewat fairseq, bukan ONNX.

**SELESAI 2026-10-09 — integrasi M11 sudah dikerjakan.** Akar keluhan Master
("pesannya sudah jalan, TTS-nya tidak balas") terbukti: **tidak ada satu pun
kode pemutaran audio di seluruh proyek.** NAudio dirujuk di csproj sehingga
DLL-nya ikut tersalin, tetapi `WaveOutEvent`/`AudioFileReader` **tidak pernah
dipanggil**. `KirimAsync` mengalirkan balasan ke gelembung lalu berhenti.

Yang ditambahkan:

| Berkas | Isi |
|---|---|
| `src/SilverWolf.Services/Tts/TtsWorker.cs` | menjalankan `tools/tts/buat_suara.py` sebagai proses; potong per kalimat; cache SHA-256 atas **teks + seluruh parameter suara**; batas `TtsBatasDetik` + `Kill(entireProcessTree)` |
| `src/SilverWolf.Services/Tts/PcmPlayer.cs` | `WaveOutEvent` + `AudioFileReader` (satu instance dipakai ulang); `LevelBerubah` tiap 16 ms untuk LipSync |
| `src/SilverWolf.Services/Tts/TtsPipeline.cs` | menyatukan produksi + pemutaran; balasan terbaru selalu menang |
| `src/SilverWolf.Core/Configuration/AppConfig.cs` | blok setelan TTS/RVC (baris ~219–234), dibaca dari `.env` |
| `CompanionViewModel` | `SiapkanTts()`, `BacakanAsync()`, pemicu di `AlirkanAsync`; `TeksTts` kini melaporkan `AlasanSuaraHening` alih-alih `"siap"` palsu |

**Verifikasi:** build solusi 0 warning/0 error; `dotnet test` **114/114 lulus**;
rantai dijalankan dengan perintah persis seperti yang dikirim aplikasi →
`sw-e2e.wav` **40.000 Hz mono 16 bit, 4,16 dtk, RMS 6.105** (ADA SUARA);
`crash.log` aplikasi nyata memuat `tts: siap (rantai=piper+rvc,piper, rvc=True)`.

**Blocker yang masih nyata:** hanya §8.1 (kematian senyap, tiga mode).
Integrasi sudah jalan, tetapi aplikasi masih bisa mati senyap — lihat §8.1.

#### Pekerja TTS menetap (komit `dfc1125`, 2026-10-09)

Masalah yang berhasil ditutup: cara lama menjalankan **proses Python baru per
kalimat**, sehingga seluruh model RVC dimuat ulang tiap kali. Diukur di mesin
ini (4 core, CPU saja):

| Tahap | Waktu |
|---|---|
| muat RVC + HuBERT | 0,6–1,0 dtk |
| **inferensi pertama** (memuat rmvpe) | **21–24 dtk** |
| inferensi berikutnya | 6,1–13,3 dtk |

Balasan dua kalimat menghabiskan ~45 dtk dan **menembus batas 40 dtk**, jadi
antrean dibatalkan sebelum satu pun WAV sampai ke pemutar — gejalanya
"menyiapkan suara…" tanpa henti, tanpa suara. Kini satu pekerja Python menetap
memuat model **sekali per sesi**:

| Berkas | Isi |
|---|---|
| `src/SilverWolf.Services/Tts/PekerjaTts.cs` | pekerja menetap: pipa bernama + protokol JSON satu baris |
| `tools/tts/pekerja_tts.py` | sisi Python; `pasang_patch_torch()` (`weights_only=False`) |
| `tools/uji-tts-integrasi/` | penguji integrasi — memanggil `TtsWorker` dari rakitan yang sama |

**Protokol lewat pipa bernama, BUKAN stdout.** Pustaka pihak ketiga mencetak
sendiri ke stdout (`rvc_python/configs/config.py:93`), jadi stdout tidak pernah
bisa dipakai sebagai jalur data.

**Verifikasi** (`tools/uji-tts-integrasi`, `outputs/uji-tts-pekerja.log`):
kalimat 1 **20,1 dtk** (muat rmvpe di dalamnya), kalimat 2 **6,0 dtk**, total
**26,1 dtk untuk 2 kalimat**, audio 2,26 + 2,32 dtk @40 kHz, berhenti bersih.

⚠️ **Jebakan yang menghabiskan waktu — jangan diulang.** Kalau pekerja menetap
bermasalah, periksa urutan ini sebelum menebak yang lain:

| Gejala | Sebab |
|---|---|
| proses diam, **nol baris log**, harus dibunuh | `HasilkanAsync` deadlock dengan dirinya sendiri: ia memegang `_kunci` lalu memanggil `NyalakanAsync` yang meminta `_kunci` sama. Bendera `_nyalaDalam` **harus** disetel sebelum pemanggilan, bukan di dalam `NyalakanAsync` |
| model termuat (0,5 dtk) tetapi induk menunggu batas penuh lalu gagal | nama pipa harus diawali `\\.\pipe\`; tanpa itu Windows menganggapnya berkas biasa di direktori kerja. Arah pipa juga harus cocok: server `PipeDirection.InOut` untuk klien `O_RDWR` |
| `json.loads` menolak baris pertama: "Unexpected UTF-8 BOM" | `StandardInputEncoding = Encoding.UTF8` menulis preamble; pakai `new UTF8Encoding(false)` **dan** `lstrip("\ufeff")` di sisi Python |

Catatan: `uji-tts-integrasi` membangun `EnvSource` sendiri, jadi ia berjalan
pada **nilai bawaan** (`batas=40`, `siap=120`), bukan `.env` (75/180). Jangan
terkecoh baris pertama lognya.

---

## 3. Struktur solusi

```
SilverWolf.sln                   penanda akar repo
Directory.Build.props            properti bersama + impor build/CubismSdk.props
build/CubismSdk.props            penunjuk $(CubismSdkDir) + deteksi Cubism 4/5
src/SilverWolf.App/              WinUI 3 exe (unpackaged + WASDK self-contained)
src/SilverWolf.Core/             net8.0 polos, bebas WinRT, bisa diuji di mana saja
src/SilverWolf.Services/         net8.0-windows: llama, inference, agent, backend
native/SilverWolf.Live2D/        DLL ekspor-C polos (BUKAN C++/WinRT) + build-cli.sh
native/SilverWolf.Phonemizer/    belum dibuat (M11)
tests/SilverWolf.Core.Tests/     xunit, 108 uji
tools/                           alat bantu Python (lihat §9)
docs/                            dokumen ini + migrasi/ + arsip/
```

### Inventaris berkas penting

**`src/SilverWolf.Core`** — `Configuration/{EnvFile,EnvSource,AppConfig}.cs`;
`Text/{EmotionParser,SentenceSplitter,TagSkipper}.cs`;
`Domain/{Mood,PersonaComposer,CharacterVault,ChatHistory,ProactiveDirector,TieredMemoryEngine}.cs`;
`Domain/Kizuna/*` (`KizunaConfig`, `BondEvaluator`, `BondDynamics`,
`BondContextBuilder`, `KizunaEngine`, `KizunaStorage`, `BondSnapshot`).

**`src/SilverWolf.Services`** — `Configuration/AppPaths.cs`;
`Inference/{LlmModels,OpenAiCompatibleProvider,StubProvider}.cs`;
`Llama/{ModelLocator,LlamaServerProcess}.cs`;
`Agent/AgentService.cs`; `Backend/CompanionBackend.cs`;
`Bootstrap/CompanionRuntime.cs`.

**`src/SilverWolf.App`** — `App.xaml(.cs)`; `MainWindow.xaml(.cs)`;
`Diagnostics/CrashLog.cs`; `Configuration/{AssetLocator,UiSettings}.cs`;
`Models/ChatBubble.cs`; `ViewModels/CompanionViewModel.cs`;
`Views/{StageView,ConsoleView,MessageListView,ComposerView}.xaml(.cs)`;
`Native/Live2DNative.cs`.

> **Catatan `ConsoleView` (9 Okt 2026).** Panel kanan **sengaja dibuat polos**:
> banner galat + `MessageListView` + `ComposerView`, titik. Header
> NEURO-SAMA/status, kartu HUD ikatan (Lv/XP/ProgressBar/Kehangatan/Atmosfer/
> Sikap, Elus Kepala, Pancing Obrolan), dan telemetri GPU/MEM/TTS dibuang dari
> **tampilan** atas permintaan Master ("saya butuh chatnya aja").
>
> **Mesinnya tidak dibuang.** Semua properti itu (`TeksIkatan`, `TeksXp`,
> `Kemajuan`, `TeksGpu`, `TeksTts`, `TeksNeuro`, `StatusTombol`, …) masih ada
> dan masih dihitung di `CompanionViewModel`; yang hilang hanya XAML-nya.
> Jadi mengembalikan panel = menulis ulang `ConsoleView.xaml`, tanpa satu pun
> perubahan C#. **Jangan** menyimpulkan properti itu mati lalu menghapusnya.
>
> `AdaHealthError` ada khusus untuk banner itu — `HealthError` diisi tetapi
> tidak punya penampil mana pun sebelum ini, sehingga setiap kegagalan runtime
> menjadi tak terlihat persis setelah panel status dibuang.
>
> Handler `OnNeuroDiklik` **sudah dihapus** bersama tombolnya. Mode otonom tetap
> hidup di `CompanionViewModel.AutonomousMode`; kalau tombolnya perlu lagi,
> pakai `Command="{Binding PancingObrolanCommand}"` — tanpa handler baru.

**`native/SilverWolf.Live2D`** — `Live2DStage.cpp` (±1600 baris, inti renderer),
`Live2DStage.h` (API C), `build-cli.sh`, `rva-lookup.py`.

---

## 4. Perintah: build, uji, jalankan

```bash
cd "C:/Users/Daffa/Desktop/AI Vtuber Project/AI Vtuber WINUI3"

# 1. Build C# — Platform=x64 DAN SelfContained WAJIB
dotnet build SilverWolf.sln -p:Platform=x64 -c Debug

# 2. Uji
dotnet test tests/SilverWolf.Core.Tests/SilverWolf.Core.Tests.csproj

# 3. Native — MSBuild.exe DIBLOKIR; dotnet build tak punya VCTargetsPath (MSB4019)
bash native/SilverWolf.Live2D/build-cli.sh Release     # Debug juga sudah jalan

# 4. Bangun C# (perintah lengkap — tanpa flag ini hasilnya TIDAK BISA dijalankan)
dotnet build src/SilverWolf.App/SilverWolf.App.csproj \
  -c Debug -p:Platform=x64 -r win-x64 -p:SelfContained=true

# 5. Sebarkan native ke folder keluaran
T="src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64"
NAT="native/SilverWolf.Live2D/build/x64/Release"
cp -f "$NAT/SilverWolf.Live2D.dll" "$T/"
mkdir -p "$T/FrameworkShaders"
cp -f "$NAT/FrameworkShaders/"*.fx "$T/FrameworkShaders/"

# 6. Jalankan dan baca buktinya
cd "$T" && rm -f crash.log run.out && (./SilverWolf.App.exe > run.out 2>&1 &)
sleep 25
grep -a -E "panggung 1 aktif|periksa piksel|kanvas model|efek:|MainWindow" crash.log
tasklist //FI "IMAGENAME eq SilverWolf.App.exe"
```

Keluaran yang diharapkan:

```
TAHAP: live2d: panggung 1 aktif pada percobaan 1
TAHAP: [live2d] [swl2d] efek: kedip aktif, 2 parameter
TAHAP: [live2d] [swl2d] efek: napas aktif
TAHAP: [live2d] [swl2d] efek: pandangan aktif (dengan peredaman CubismTargetPoint)
TAHAP: [live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=0.70, geser=(-0.20,0.30)
TAHAP: [live2d] [swl2d] periksa piksel (bingkai ke-10): 641x851, terisi=113893, kotak=(46,151)-(543,555)
```

---

## 5. Angka golden & kontrak perilaku yang dijaga

### Angka yang dikunci unit test

Semuanya diturunkan dari rumus sumber, dan sebagian sudah divalidasi terhadap
artefak asli aplikasi lama.

| Kejadian | Hasil |
|---|---|
| Pesan pertama | +4 poin |
| Pesan kedua, hari yang sama | +3,4 (pengali pengulangan 0,85) |
| Pesan di hari berikutnya | +4,12 (streak 2 → bonus 1,03) |
| Sentuhan pertama | +8 poin |
| Kehangatan setelah 14 hari | 0,675 → atmosfer `neutral` |
| Kehangatan setelah 28 hari | 0,5125 → `neutral` |
| Kehangatan setelah 42 hari | 0,43125 → `cool` |
| `Mood.Perbarui(Awal,"senyum")` | valensi 0,645; energi 0,8; afinitas 0,92 |
| `Mood.Perbarui(Awal,"goda")` | energi 0,9 |
| `Mood.Perbarui(Awal,"sedih")` | valensi 0,54 |
| Tag di luar daftar | delta **0** (bukan 0,05 seperti `netral`) |
| Tahap dari poin | ≥2000 `lover`, ≥1000 `companion`, ≥400 `regular`, ≥100 `acquaintance`, lainnya `stranger` |

### Validasi terhadap artefak asli (golden kizuna)

Diambil 2026-10-07 dengan menjalankan server Node asli mode stub
(`VTUBER_STUB=true node out/server/main.js --port 8799`), lalu `POST /api/chat`,
`POST /api/kizuna/touch`, `GET /api/kizuna`. Tersimpan di
`tests/SilverWolf.Core.Tests/TestData/golden/kizuna-setelah-3-interaksi.json`.

| Interaksi | Aplikasi lama | Port C# |
|---|---|---|
| Pesan 1 | `4` | 4 ✅ |
| Touch | `6.800000000000001` (8 × 0,85) | 8 × 0,85 = 6,8 ✅ |
| Pesan 2 | `2.8900000000000006` (4 × 0,85²) | 4 × 0,7225 = 2,89 ✅ |
| `progress` | `14` | round(13,69) = 14 ✅ |
| `role` | `"guest"` | guest ✅ |
| `positiveBucketCounts` | `{"day:20732": 3}` | sama ✅ |

### Kontrak perilaku yang tidak boleh melenceng

1. **`/api/chat` tidak pernah mengirim header `x-kizuna`.** Hanya `touch` dan
   `proactive`. UI **wajib** menyegarkan snapshot setelah aliran selesai.
2. **Header `x-kizuna` pada `touch` diambil SEBELUM `recordTouch()`** —
   snapshotnya pra-sentuhan. Jangan "perbaiki" jadi pasca.
3. **`POST /api/kizuna/touch` menghasilkan DUA interaksi**, bukan satu:
   `touch` (+6,8) lalu pesan balasan (+2,89).
4. **`midTermPrompt` selalu string kosong** (`enableSummarization:false`).
   Bukan bug — dipertahankan agar prompt identik.
5. **Pengguna kizuna berperan `guest`, bukan `owner`.** Retensi 90 hari berlaku.
6. **Poin bersifat pecahan**, bukan bilangan bulat.
7. **Polling health:** 8000 ms bila siap, 2000 ms bila belum.
8. **Pekerja proaktif:** interval 5000 ms; picu bila hewan > **65.000 ms** dan
   `!AutonomousMode || IsSending || audioBusy` tidak terlewati; nilai `idle` =
   `max(10, round((now - LastUserActivity) / 1000))`.
9. **Mirror disimpan ke `UiSettings`** (JSON di `LocalApplicationData\SilverWolf\`),
   **bukan** `ApplicationData.Current.LocalSettings` (lihat §7.13).
10. **Jangan hidupkan server HTTP lagi.** `CompanionBackend` mempertahankan
    *kontrak* HTTP, bukan soketnya.

---

## 6. Arsitektur & keputusan teknis

| Keputusan | Alasan |
|---|---|
| `SilverWolf.Core` = `net8.0` polos, **tanpa** PackageReference | Bisa diuji dari mesin mana pun; mencegah ONNX Runtime ikut tertarik |
| `KizunaEngine` menggabungkan 6 kelas pustaka asli | Aplikasi satu pengguna; lapisan multi-pengguna hanya jadi kode mati |
| `CompanionBackend` mempertahankan kontrak HTTP meski tidak ada HTTP | Perilaku pengguna identik, pemetaan ke UI 1:1 |
| `PerformCleanup()` tidak memakai timer internal | Bisa diuji; host yang menjadwalkan (24 jam) |
| `FileKizunaStorage` memakai `safeKey + ".json"` dan camelCase | Berkas data pengguna dari aplikasi lama harus tetap terbaca |
| `EnvFile` ditulis tangan | Aturan potong-komentar aplikasi lama tidak umum dan ikut menentukan isi prompt LLM |
| Identifier C# Inggris, string yang dilihat pengguna Indonesia | Nilai seperti `"siap"`, `"stranger"` ikut menentukan perilaku prompt LLM |
| Model data kizuna `JsonIgnoreCondition.WhenWritingNull` | Setara penyebaran bersyarat `...(x && {x})` milik pustaka asli |
| Live2D dibungkus DLL **ekspor-C polos**, bukan C++/WinRT | Tidak perlu `.idl`/`.winmd`/midl; `cl.exe` + `link.exe` bisa dipanggil langsung tanpa MSBuild |
| Cubism `Option` disimpan sebagai variabel **statis** | `StartUp` menyimpan penunjuk, bukan salinan — lihat §7.9 |

**Antarmuka `SilverWolf.Live2D.dll`** (semua jalur UTF-8):

```c
void swl2d_set_log(void (*sink)(const char*));
int  swl2d_init(void);
void swl2d_shutdown(void);
int  swl2d_stage_create(void* panelNative, const char* modelDir, const char* modelJson);
void swl2d_stage_destroy(int stage);
int  swl2d_stage_resize(int stage, int width, int height);
int  swl2d_stage_render(int stage);
int  swl2d_stage_set_expression(int stage, const char* name);
int  swl2d_stage_play_motion(int stage, const char* group, int no);
int  swl2d_stage_play_idle(int stage, const char* group, int no);
int  swl2d_stage_set_look(int stage, float x, float y);
int  swl2d_stage_set_view(int stage, float zoom, float offsetX, float anchorY);
```

---

## 7. Masalah nyata yang ditemukan dan penyelesaiannya

Bagian paling berharga untuk dibaca ulang. Semua muncul dari pemeriksaan
langsung, bukan dugaan.

### 7.1 MSIX `APPX1101` — `onnxruntime.dll` bentrok (M2)

**Gejala:** `Payload contains two or more files with the same destination path
'onnxruntime.dll'`.

**Penyebab:** `Microsoft.ML.OnnxRuntime` 1.22.0 dan
`Microsoft.Windows.AI.MachineLearning` 2.1.74 (ikut `WindowsAppSDK` →
`WindowsAppSDK.ML`, jadi **selalu ada** di setiap aplikasi WinUI 3) sama-sama
mengirim `onnxruntime.dll` dengan `PathInPackage` identik.

**Pelajaran:** mengganti nama berkas **tidak menyelesaikan apa pun** — MSIX
mengelompokkan payload berdasarkan **destinasi**, bukan sumber. Menimpa `<None>`
juga percuma (payload dirakit dari `@(PackagingOutputs)`). Dan target
`GenerateProjectPriFile` yang dipakai sebagai titik sisip **tidak ada di project
ini**; MSBuild diam-diam mengabaikan `BeforeTargets` ke target yang tidak eksis.

**Solusi:** `SilverWolf.Services` tidak mereferensikan ONNX Runtime sama sekali;
Piper TTS pindah ke exe worker terpisah tanpa WindowsAppSDK (M11).
**Efek samping menguntungkan:** sintesis Piper tidak lagi berebut UI thread
maupun perangkat D3D11 milik Cubism, dan TTS bisa di-restart sendiri.

### 7.2 `REGDB_E_CLASSNOTREG` saat start (M2)

**Gejala:** build sukses, aplikasi gagal start `COMException (0x80040154)` di
`DeploymentManagerCS.AutoInitialize.AccessWindowsAppSDK()`.
**Penyebab:** Windows App SDK Runtime belum terpasang — masalah lingkungan.
**Solusi:** `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`.

### 7.3 `MSB4024` — XML tidak mengizinkan `--` di komentar

Komentar penjelas ditulis memakai `<--` untuk menandai perbedaan versi. XML
melarang `--` di dalam komentar, tanpa pengecualian. Karena
`build/CubismSdk.props` diimpor `Directory.Build.props`, satu salah tulis
mematikan **seluruh** solusi.

Cek: `grep -n -- "--" build/CubismSdk.props | grep -v "<!--" | grep -v -- "-->"`

### 7.4 Cubism 5 mengubah tiga jalur yang kita andalkan

| | Cubism 4 | Cubism 5 (terverifikasi) |
|---|---|---|
| Header Core | `Core\include\Live2DCubismCore.hpp` | `Core\include\Live2DCubismCore.h` |
| Folder arsitektur | `Core\lib\windows\x64\<vcver>\` | `Core\lib\windows\x86_64\<vcver>\` |
| Renderer D3D11 | `Framework\src\Rendering\D3D11\` | `Samples\D3D11\` |

Dibiarkan → MSBuild melaporkan `CubismSdkAvailable=false` **diam-diam** dan
target native dilewati tanpa peringatan. `CubismSdk.props` sekarang menerima
kedua format.

### 7.5 Dua unit test gagal karena ekspektasi salah, bukan kodenya (M7)

1. **`NILAI_TAG[tag] ?? 0`** — tag di luar daftar memberi delta **0**, bukan
   delta `netral` (0,05).
2. **Ambang atmosfer** — kehangatan setelah 14 hari 0,675, ambang `warm` ≥ 0,75
   → hasilnya `neutral`.

Pelajaran: saat mem-port, angka hasil hitungan tangan harus diturunkan dari
rumus sumber.

### 7.6 `STATUS_FAIL_FAST_EXCEPTION` tanpa pesan (bootstrap ganda)

**Gejala:** `0xC0000602` langsung mati. stderr kosong, Event Log kosong, WER
kosong, `try/catch` tidak pernah dieksekusi.

**Diagnosis:** ditambahkan `Diagnostics/CrashLog.cs` dengan penanda tahap. Log
berhenti **sebelum baris pertama konstruktor `MainWindow`** → yang gagal
konstruktor dasar `Microsoft.UI.Xaml.Window`, bukan kode aplikasi.

**Penyebab:** dua sumber Windows App SDK dimuat sekaligus — DLL self-contained
di folder output **dan** runtime sistem (`Microsoft.WindowsAppRuntime.2`
v2.5.1.0 yang ikut terpasang oleh VS Installer).

**Perbaikan:** `WindowsAppSdkBootstrapInitialize` diset **`false`**.

**Pelajaran:** (a) jangan pernah mengaktifkannya kembali selama self-contained;
(b) fail-fast tidak bisa ditangkap `try/catch`; (c) memasang komponen Visual
Studio dapat mengubah perilaku aplikasi yang sudah berjalan; (d) urutan
penyelidikan: exit code → Event Log → artefak build → **baru** pencatat tahap.

### 7.7 `SwapChainPanel` WinUI 3 tidak punya properti `Background`

Di UWP ia turunan `Grid`, di WinUI 3 bukan. Menulisnya membuat
`XamlParseException` saat runtime dan aplikasi mati di `InitializeComponent()`
— padahal build hijau 0/0 karena XAML diurai saat runtime.
**Perbaikan:** dibungkus `Grid` ber-`Background="Transparent"` yang memegang
penangan pointer.

### 7.8 IID `ISwapChainPanelNative` berbeda antar namespace

| Objek | IID |
|---|---|
| `Windows.UI.Xaml.Controls.SwapChainPanel` | `F92F19D2-3ADE-45A6-A20C-F6F1EA90554B` |
| `Microsoft.UI.Xaml.Controls.SwapChainPanel` | **`63AAD0B8-7C24-40FF-85A8-640D944CC325`** |
| turunan `ISwapChainPanelNative2` | `88FD8248-10DA-4810-BB4C-010DD27FAEA9` |

Header WinUI ada di
`~/.nuget/packages/microsoft.windowsappsdk.winui/*/include/microsoft.ui.xaml.media.dxinterop.h`
— **bukan** di header SDK Windows. Salah IID → `E_NOINTERFACE` dan layar kosong
tanpa pesan apa pun.

**Kesimpulan keliru yang pernah diambil:** memindai byte GUID `F92F19D2...` di
`Microsoft.UI*.dll` tidak menemukannya, lalu disimpulkan "WinUI 3 tidak
mengimplementasikannya". **Itu salah.** Pemindaian byte GUID tidak bisa dipakai
untuk menyimpulkan ada/tidaknya sebuah antarmuka.

### 7.9 `CubismFramework::StartUp` menyimpan PENUNJUK, bukan salinan

```cpp
// Framework/src/CubismFramework.cpp:53-61
csmBool CubismFramework::StartUp(ICubismAllocator* allocator, const Option* option)
{
    ...
    s_option = option;      // ← menyimpan penunjuk
}
```

Kode lama membuat `CubismFramework::Option opsi;` sebagai **variabel lokal** di
`swl2d_init`. Begitu fungsi itu `return`, kerangka stack-nya hilang dan
`s_option` menjadi **penunjuk menggantung**.

**Gejala yang sangat menyesatkan:** pemuatan model dan tekstur **tetap
berhasil** (kode kita memanggil `MuatBerkas` langsung), lalu proses mati
mendadak hanya ketika **Cubism sendiri** memuat berkas — yaitu saat
mengompilasi shader. Jejak tumpukan menunjuk
`CubismShader_D3D11::GenerateShaders`, tetapi alamat kesalahannya di luar semua
modul.

**Perbaikan:** `CubismFramework::Option g_opsi;` statis berkas.

**Cacat ini muncul dua kali dalam catatan** — yang kedua dilaporkan sebagai
"build Debug mati saat kompilasi shader, dugaan stack overflow". **Itu keliru.**
Uji A/B membuktikannya:

| Varian `Option` | Konfigurasi | Hasil |
|---|---|---|
| Lokal | Debug | mati senyap tepat setelah kedua `MuatBerkas ... FrameworkShaders/*.fx` |
| Statis (`g_opsi`) | Debug | selesai, model tampil |

Build Debug **sudah jalan** apa adanya — tanpa `/STACK`, tanpa mengubah optimasi
sumber SDK. Kesimpulan lama bertahan hanya karena **build Debug di folder
keluaran basi** (dibangun 19:17, sumber diubah 19:26) dan tidak pernah
dijalankan ulang setelah perbaikannya masuk.

### 7.10 Viewport 1×1 — model hanya satu piksel

`CreateRenderer(1, 1)` dipakai saat panggung dibuat (ukuran panel belum
diketahui). Cubism memakai nilai itu untuk **menyetel viewport**:

```cpp
// Framework/src/Rendering/D3D11/CubismRenderer_D3D11.cpp:911-914
void CubismRenderer_D3D11::PreDraw()
{
    SetDefaultRenderState(_modelRenderTargetWidth, _modelRenderTargetHeight);
}
```

Seluruh model digambar ke **satu piksel di sudut kiri atas**. `Present()` tetap
`S_OK` dan tidak ada satu pun galat. Terukur: `terisi=1`, `kotak=(0,0)-(0,0)`.

**Perbaikan:** `AturUkuranTarget(lebar, tinggi)` memanggil **ulang**
`CreateRenderer` (bukan sekadar mengubah field) karena render target
offscreen/mask dibuat di dalam `Initialize()` memakai ukuran saat itu — pola
`LAppModel::ReloadRenderer()`.

### 7.11 Koreksi aspek salah sisi

Kode lama **selalu** memakai bentuk lanskap (`Scale(1, w/h)`). Contoh resmi
memilih berdasarkan orientasi (`LAppLive2DManager.cpp:256-263`). Panel kita
potret, jadi harus `Scale(h/w, 1)`. Gejala: model tampak kecil dan **gepeng** —
bukan karena perbesarannya kurang.

### 7.12 Cacat Cubism lain yang sudah dibetulkan

- **`Initialize` dipanggil dua kali** — `CubismUserModel::CreateRenderer`
  sudah memanggilnya sendiri (`CubismUserModel.cpp:294`).
- **`StartFrame`/`EndFrame` tidak pernah dipanggil** —
  `CubismRenderer_D3D11.hpp:122,128` mendeklarasikan keduanya; contoh resmi
  SELALU membungkus `DrawModel()`.
- **`CubismMatrix44::Scale` MENIMPA**, bukan mengalikan
  (`_tr[0]=x; _tr[5]=y;` — `CubismMatrix44.cpp:94-98`). Perbaikannya
  `matriks.SetHeight(2.0f * perbesaran)`.
- **Ukuran swap chain dalam DIP, bukan piksel fisik** — tanpa
  `XamlRoot.RasterizationScale` buffer lebih kecil daripada panel dan hasilnya
  direntangkan.
- **Penangkap pengecualian diagnostik merusak runtime .NET** — memanggil balik
  ke C# dari dalam proses pengecualian menghasilkan `Fatal error. Invalid
  Program: attempted to call a UnmanagedCallersOnly method from managed code`.
  Dijadikan opt-in `SWL2D_DIAG=1`.
- **Model diam** — renderer 30 fps tetapi bingkainya identik. Ditambahkan
  `swl2d_stage_play_idle` → `SetLoop(true)` prioritas 1.
- **`C1041: cannot open program database`** — dua `cl.exe` berebut PDB.
  Diperbaiki `-FS` + `-Fd` di `build-cli.sh`.

### 7.13 `ApplicationData.Current` tidak berlaku untuk unpackaged

Rencana M4 menyebut `ApplicationData.Current.LocalSettings` sebagai padanan
`localStorage`. **Itu salah**: aplikasi ini `WindowsPackageType=None`, dan
pemanggilan itu melempar `InvalidOperationException`. Terdeteksi hanya dari
`crash.log` saat aplikasi dijalankan — bukan dari build.
**Perbaikan:** `Configuration/UiSettings.cs` (JSON di
`LocalApplicationData\SilverWolf\ui-settings.json`).

### 7.14 Dua jebakan kompilasi C# (M9)

1. `AsyncRelayCommand(KirimAsync, BisaKirim)` — `BisaKirim` adalah **properti**
   bool, bukan delegasi, sehingga kompilator memilih overload
   `(Func<Task>, AsyncRelayCommandOptions)`. Harus `canExecute: () => BisaKirim`.
2. **`Window` di WinUI 3 tidak punya kejadian `Loaded`.** Yang punya adalah
   elemen akar isinya → `Shell.Loaded += OnLoaded`.

### 7.15 Bingkai visual: kepala kegedean

`Perbesaran = 1.85` adalah warisan aplikasi web yang **tidak bisa dipindahkan
apa adanya**:

| | Aplikasi web (PIXI) | Di sini (Cubism) |
|---|---|---|
| arti zoom | pengali skala "pas panel" | `SetHeight(2.0 * perbesaran)` langsung |
| jangkar | `anchor.set(x, 0.92)`, `y = H * 0.92` | `SetPosition` pada ruang `[-1, 1]` |

Akibatnya pada 1.85 hanya ~54% tinggi model yang masuk panel, dan bagian tengah
yang terlihat kebetulan kepala.

**Nilai akhir (dipilih dari pengukuran) — diperbarui 2026-10-09 sore:**

Kanvas model = **0.6 x 0.4 (aspek 1.5:1, lebar)**, terukur di `crash.log`.
Karena panelnya potret, mengisi tinggi membuat **lebar meluap** — inilah sebab
kepala naik keluar atas dan sayap keluar kanan.

| Perbesaran | GeserX | JangkarY | Hasil |
|---|---|---|---|
| 1.85 (warisan web) | — | — | hanya kepala+bahu |
| 0.88 (lama) | −0.10 | 0.10 | kepala & sayap terpotong |
| 0.48 | −0.08 | 0.24 | semua muat tapi **kekecilan** (33% tinggi) |
| 0.80 | −0.04 | 0.30 | sayap menempel tepi (sisa 1 px) |
| 0.78 | −0.14 | 0.30 | masih terpotong saat diukur dari tangkapan layar |
| **0.70** | **−0.20** | **0.30** | **final — dua sisi lega, dikonfirmasi dua kali jalan** |

**Konfirmasi 0.70 dari dua jalan berbeda** (2026-10-09 lanjutan):

| Jalan | kotak di `crash.log` | margin kiri | margin kanan |
|---|---|---|---|
| Probe 00:39 | (46,151)-(543,555) | 46 px | 98 px |
| Probe 01:xx | (45,153)-(532,555) | 45 px | **109 px** |

Kedua jalan cocok satu sama lain (selisih < 2 px), dan **tidak ada clamp**
(`x0` = 45/46, bukan 0). Sebelumnya di 0.78 sisi kanan hanya punya **1 px**
ruang — sekarang **~100 px**. Sayap tidak lagi menempel tepi.

> Catatan jujur: nilai di tabel ini berasal dari **probe** `crash.log`, yang
> menurut peringatan di bawah **bukan** yang dilihat mata. Konfirmasi visual
> terakhir tetap milik Master, karena tangkapan jendela tidak mungkin dari sesi
> otomatis. Kalau masih ada bagian yang terpotong, kirim tangkapan layar — angka
> `Perbesaran`/`GeserX`/`JangkarY` tinggal disetel lagi dengan aritmetika yang
> sama.

> ### ⚠️ Probe piksel ≠ yang dilihat mata — ukur tangkapan layar
>
> Pada posisi 0.78/−0.14, dua pengukuran berbeda jauh:
>
> | Metode | Bbox | Margin kiri | Margin kanan |
> |---|---|---|---|
> | Probe `crash.log` | (50,135)-(603,584) di bingkai 641 | 50 px | 38 px |
> | **PIL pada tangkapan Master** | (147,221)-(671,872) di panel 672 | **147 px** | **1 px (terpotong)** |
>
> Selisih ~97 px. Probe mengukur bounding box quad/alpha, **bukan** piksel yang
> benar-benar terlihat. **Untuk keputusan visual, ukur tangkapan layar Master
> dengan PIL** (`tools/potret-jendela.py` atau analisis bbox manual).

**Aritmetika yang mengunci nilai 0.70.** Pada tangkapan Master, karakter
mengisi **524 px dari panel 672 px** (0.78). Artinya hanya ~88 px render tersisa
untuk **dua** margin — mustahil memenuhi permintaan "geser ~80 px ke kiri
sambil semua badan + sayap tetap kelihatan": salah satu sisi pasti terpotong.
Dengan 0.70 lebar turun ke ~496 px sehingga sisa ~145 px, dan geser bisa
dilakukan dengan aman.

`GeserX = -0.20` perlu karena seni model ini **tidak simetris** — sayap
mekaniknya jauh lebih panjang ke kanan, jadi pusat massa gambarnya selalu
bergeser ke kanan walaupun modelnya "di tengah".
`JangkarY = 0.30` mengangkat model agar ruang terisi lebih merata
(permintaan Master: "naikin ke atas badannya").

**Jebakan: probe di-CLAMP di `x0=0`.** `x0` tidak pernah dilaporkan negatif,
jadi `x0=0` **bukan** berarti "margin kiri nol" melainkan "kehabisan ruang
lapor". Akibatnya lebar kotak menipu (terlihat menyusut 553→552→535→527
padahal lebar karakter konstan). **Baca `x1` saja (tidak di-clamp), lalu
hitung `x0 = x1 - lebar_asli`.**

**Cara memverifikasi tanpa melihat layar** — probe di `crash.log` tiap 10 bingkai:
```
TAHAP: [live2d] [swl2d] periksa piksel (bingkai ke-10): 641x851, terisi=..., kotak=(x0,y0)-(x1,y1)
```
Bingkainya 641x851. **Uji cepat tanpa build ulang:**
`SWL2D_PERBESARAN`, `SWL2D_GESER_X`, `SWL2D_JANGKAR_Y`.
Bukti: `tools/bukti/framing-070-geser-020.log`.

### 7.16 Ketajaman: buffer sudah 1:1, tekstur kini punya mipmap

Diukur langsung dari aplikasi:

```
live2d: panel 408,8x528,8 DIP, skala=1,250, buffer=511x661
```

408,8 × 1,250 = 511 → buffer **tepat** seukuran piksel fisik panel. **Tidak ada
perentangan.** Layar memang 125% (1920×1080 fisik = 1536×864 DIP).

**Penyebab yang diperbaiki — minifikasi tanpa mipmap.** Tekstur 4096×4096,
sedangkan karakter di layar hanya ~580 px tinggi → diperkecil ~6–7×. Tekstur
dulu `MipLevels = 1`, jadi detail halus (tulisan di visor, garis tipis) pecah.
`BuatTekstur` sekarang membuat rantai mip penuh (`MipLevels = 0` +
`D3D11_RESOURCE_MISC_GENERATE_MIPS`) lalu `GenerateMips`.

Catatan implementasi: tekstur dibuat **tanpa** `pInitialData` karena dengan
`MipLevels = 0` D3D menuntut data untuk semua sub-sumber; level 0 diisi lewat
`UpdateSubresource` lalu `GenerateMips`. Biaya: 6 tekstur 4096² + rantai mip
≈ 536 MB VRAM (naik ~134 MB) — ingat ini kalau §8.1 ternyata soal tekanan
memori GPU.

### 7.17 Aset terlewat yang baru ditemukan (2026-10-07)

Setelah user bertanya "apakah sudah tidak ada yang perlu dipindah", jawabannya
**ada** — dan salah satunya tidak pernah ada di proyek lama sama sekali.

| Item | Ukuran | Nasib |
|---|---|---|
| Font Google (3 keluarga) | 400 KB | **Tidak pernah ada di proyek lama** — diambil dari `fonts.googleapis.com` saat runtime → diunduh ke `assets/fonts/` |
| `icon.png` + `icon.ico` | 3 KB | Terlewat → `assets/ikon/` |
| Sumber lengkap (51 berkas) | 740 KB | Terlewat → `docs/arsip/sumber-web/` |
| `public/bg-hsr.jpg` | 1,1 MB | **Orphan** — tidak dirujuk apa pun. Dilewati |

**Cara mengunduh font Google yang benar:** `fonts.googleapis.com/css2` dengan UA
modern mengembalikan **woff2** — dan WinUI 3 **tidak mendukung woff2**. UA lama
(IE6) mengembalikan berkas dengan header bukan `00010000` — bukan TTF sah.
Yang benar: repo `google/fonts`,
`raw.githubusercontent.com/google/fonts/main/ofl/<keluarga>/<Nama>[wght].ttf`.
Selalu verifikasi 4 byte pertama: `00010000` (TrueType) atau `4f54544f`
(OpenType).

⚠️ Ketiganya **variable font**. DirectWrite seharusnya menerapkan instance sumbu
`wght` otomatis, tetapi **belum diuji di WinUI 3** — risiko M10.

### 7.18 Cara menghapus aplikasi lama (dan pengaman yang tidak boleh diakali)

Recycle Bin **tidak bisa diakses secara programatik** di mesin ini — tiga jalur
diblokir kebijakan: `Add-Type -AssemblyName Microsoft.VisualBasic`,
`New-Object -ComObject Shell.Application`,
`[Reflection.Assembly]::LoadWithPartialName(...)`.

Lingkungan ini juga punya pengaman **safe-delete** yang membungkus
`Remove-Item`, memaksa lewat trash, dan **menolak hapus permanen** bila
trash-nya gagal:

```
[safe-delete][SAFE_DELETE_FAIL_CLOSED] {"reason":"trash-failed",
 "detail":"genie-trash failed; refusing fallback delete"}
```

Hasil: **3,1 GB terhapus**, sisa **824 MB** berupa satu berkas
`apps/stage-tamagotchi/release/win-unpacked/resources/app.asar` yang ditolak
trash. Pengaman itu **tidak diakali** — sisanya dihapus user lewat File Explorer.

### 7.19 Pembongkaran membuang `CancellationTokenSource` yang masih dipakai

**Gejala.** Setiap kali jendela ditutup, `crash.log` mencatat dua exception:

```
sumber  : PancingObrolanAsync
tipe    : System.Net.Http.HttpIOException
pesan   : The response ended prematurely. (ResponseEnded)

sumber  : SegarkanHealthAsync
tipe    : System.ObjectDisposedException
pesan   : The CancellationTokenSource has been disposed.
   at CompanionViewModel.SegarkanHealthAsync() line 536
```

**Penyebab.** `DispatcherQueueTimer.Stop()` **tidak menunggu** callback yang
sedang berjalan. Callback yang belum sempat mulai tetap dipanggil sekali lagi
setelah pembongkaran, lalu membaca `_cts.Token` — padahal `DisposeAsync` sudah
memanggil `_cts.Dispose()`.

**Perbaikan** (`CompanionViewModel.cs`):

1. Penanda `_sedangDibuang` (volatile) yang diperiksa di awal
   `SegarkanHealthAsync` dan `CekProaktifAsync`.
2. `_cts` **hanya dibatalkan, tidak dibuang**. Satu `CancellationTokenSource`
   yang tidak dibuang tidak menimbulkan masalah saat proses sedang berakhir.
3. Handler `Tick` kedua timer dibungkus `try/catch`. Handler timer adalah
   `async void` — satu exception yang lolos dari situ menjadi exception tak
   tertangani dan mematikan proses dengan **fail-fast senyap**, persis pola
   kegagalan yang paling sulit didiagnosis di aplikasi ini.

**Verifikasi.** Jendela ditutup sengaja lewat `WM_CLOSE`: proses keluar bersih,
**0 entri exception** di `crash.log` (sebelumnya selalu ada dua).

**Pelajaran sampingan yang penting:** temuan ini muncul karena log kematian
disimpan utuh. Sebelumnya loop uji menghapus `crash.log` tiap percobaan
sehingga buktinya hilang — dan tanpa log itu, `DisposeAsync` yang muncul di
tengah akan terus disalahartikan sebagai "crash".

### 7.20 Jendela bisa berada dengan kotak input keluar layar

**Gejala.** Setelah ukuran jendela diperbaiki ke DIP (§8.3), di layar
1920×1080 skala 125% jendelanya menjadi 1475×950 piksel. Kalau Windows
menaruhnya agak ke bawah — mis. di (224,224) seperti yang teramati — bagian
bawah jendela jatuh di luar layar, dan **kotak input pesan ikut hilang**
sehingga tidak bisa diklik sama sekali.

**Penyebab.** `AppWindow.Resize` tidak memperhitungkan area kerja yang tersedia.
Ukuran "paritas Electron" (1180×760 DIP) memang benar, tetapi tidak selalu muat.

**Perbaikan** (`MainWindow.xaml.cs`): ukuran dijepit ke 95% area kerja dari
`DisplayArea.GetFromWindowId(...).WorkArea`. Saat ada ruang, hasilnya tetap
1475×950; saat tidak, jendelanya mengecil supaya seluruh isinya tetap terjangkau.

Terverifikasi:

```
TAHAP: windowing: area kerja 1920x1020, skala 1,250
TAHAP: windowing: Resize(1475,950)
```

### 7.21 Enter untuk mengirim pesan

`ComposerView` sekarang mengirim pesan saat Enter ditekan
(`ComposerView.xaml.cs` → `OnKotakPreviewKeyDown`).

Penangan dipasang ke **dua** kejadian sekaligus, `PreviewKeyDown` dan
`KeyDown`. `PreviewKeyDown` berjalan lebih dulu dan menandai kejadiannya sudah
ditangani, sehingga `KeyDown` tidak menyala — tidak ada pengiriman ganda.
Cadangannya ada karena perilaku Enter pada `TextBox` WinUI 3 bergantung versi.

`KeyboardAccelerator` **tidak** dipakai: kotak ini `AcceptsReturn="False"`
sehingga `TextBox` menangani Enter lebih dulu dan accelerator tidak pernah
kebagian.

⚠️ **Belum diverifikasi secara interaktif.** Lihat §9 — input sintetis tidak
sampai ke aplikasi di lingkungan ini.

### 7.22 Arah pandang kursor terbalik (kepala menunduk saat kursor di atas)

Gejala yang dilaporkan Master: kursor di atas kepala → kepala **menunduk**;
kursor di bawah → kepala **mendongak**. Persis kebalikan.

**Sebabnya dua sistem koordinat yang berlawanan arah sumbu Y:**

| Sistem | Arah Y positif | Akibat |
|---|---|---|
| Layar (WinUI) | ke **BAWAH** | kursor di atas kepala → `titik.Y - wajahY` **negatif** |
| Cubism `ParamAngleY` | ke **ATAS** | nilai positif = kepala menoleh naik |

Framework mendaftarkan parameter itu di `Live2DStage.cpp` (~baris 712) sebagai
`LookParameterData(ParamAngleY, 0.0f, 30.0f)` — basis 0, puncak +30 untuk
masukan positif. Jadi Y layar yang negatif diteruskan apa adanya ke
`CubismLook::UpdateParameters` dan berubah arti menjadi "menunduk".

**Perbaikan** di `StageView.xaml.cs`, `OnPanggungPointerBergerak`:

```csharp
var x = Jepit((titik.X - wajahX) / rentangX);     // X TIDAK dibalik
var y = Jepit(-(titik.Y - wajahY) / rentangY);    // Y DIBALIK — layar ke bawah
```

Sumbu X tidak dibalik karena kiri/kanan sama arahnya di kedua sistem. Titik
wajah ada di `TitikWajahY = 0.46f` (tinggi panel) dan tetap netral di kedua
versi.

**Cara memeriksa kalau gejala ini muncul lagi:** hitung tanda `y` untuk kursor di
`titik.Y = 0` (atas). Harus **positif**. Kalau negatif, pembalikannya hilang.

---

### 7.23 Model tidak tahu tanggal & jam — alat `waktu` (2026-10-09)

**Gejala.** Ditanya "hari ini tanggal berapa" atau "jam berapa", Silver Wolf
menjawab dari ingatan latihannya dan meleset berbulan-bulan sampai bertahun-
tahun. Ia juga tidak tahu kapan ulang tahun Master.

**Sebab.** Tidak ada satu pun tempat di prompt yang memberi model angka waktu
nyata. Model GGUF kecil juga tidak andal menghitung selisih tanggal, jadi
"tinggal berapa hari lagi" hampir pasti salah kalau diserahkan ke model.

**Keputusan — alat otomatis, bukan function calling.** `llama-server` hanya
meneruskan apa yang dikeluarkan template obrolan; model yang dipakai tidak
konsisten menghasilkan JSON `tool_calls`. Karena itu alatnya **tidak menunggu
diminta model**: `IAlat.Otomatis = true` berarti dijalankan setiap giliran dan
hasilnya disuntikkan ke prompt sistem. Jawabannya tetap benar walau model tidak
pernah memanggil alat sama sekali.

Berkasnya:

| Berkas | Isi |
| --- | --- |
| `src/SilverWolf.Core/Domain/Tool.cs` | `IAlat` + `DaftarAlat` (registri). Alat yang melempar **dilewati**, tidak menggagalkan giliran. |
| `src/SilverWolf.Core/Domain/TimeTool.cs` | `AlatWaktu` — tanggal, hari, jam, bagian hari, hitung mundur ulang tahun. |
| `src/SilverWolf.Core/Domain/MasterProfile.cs` | Tanggal lahir terstruktur. Menghitung umur & sisa hari. |
| `silver_wolf_memory/Profil.md` | Sumber data tanggal lahir. Format **wajib ISO `yyyy-MM-dd`**. |

Hitungan tanggal dikerjakan C#, bukan model: `Umur`, `UlangTahunBerikutnya`,
`HariMenujuUlangTahun`, `UmurBerikutnya`. Hari 29 Februari digeser ke 28 di tahun
biasa supaya `DateOnly` tidak melempar.

**Nama hari dan bulan ditulis sendiri** di `TimeTool.cs`, tidak lewat
`CultureInfo("id-ID")`: aplikasi bisa dibangun dengan `InvariantGlobalization`,
dan saat itu nama bulan berubah jadi bahasa Inggris tanpa peringatan.

**Jangan ditulis sebagai tebakan.** Keluaran alat menyertakan kalimat
"jangan menebak tanggal atau jam sendiri" — tanpa itu model cenderung
mengabaikan bloknya dan mengarang tanggal.

**Data Master saat ini:** ulang tahun **16 Oktober**, lahir **2005-10-16**.
Tersimpan di `silver_wolf_memory/Profil.md` **dan** sebagai baris fakta di
`Fakta.md`. Folder `silver_wolf_memory/` di-gitignore, jadi tanggal lahir tidak
ikut repo publik.

---

## 8. Cacat terbuka & sisa pekerjaan

### 8.1 🔴 Proses mati senyap — INTERMITEN, belum tertutup

**Laju sejauh ini: sekitar 1 dari 10 kali jalan** (hitungan lama, hanya untuk
kematian saat llama memuat model). **Cara menghitungnya perlu hati-hati** —
lihat catatan di bawah.

> **Pembaruan 2026-10-08 — cacat ini direproduksi 2 dari 2 peluncuran, di DUA
> titik berbeda.** Jadi lajunya tampaknya lebih tinggi daripada 1 dari 10.
>
> | Peluncuran | Mati di mana | Bukti |
> |---|---|---|
> | 1 | saat prompt LLM pertama diproses (pekerja proaktif) | `HttpIOException: The response ended prematurely`; app + llama-server hilang bersamaan |
> | 2 | saat pembuatan panggung Live2D, **sebelum** `runtime:` pernah tercatat | `crash.log` berakhir di `live2d: ThisPtr=...` |
>
> Keduanya tanpa entri Event Log, tanpa dump WER, tanpa `DisposeAsync` —
> fail-fast senyap.
>
> **Uji kontrol yang penting.** `llama-server` dijalankan SENDIRI (tanpa
> aplikasi, tanpa renderer D3D11) dengan argumen **persis sama**: `/health` → 200,
> dan prompt **2003 token** (seukuran prompt aplikasi) **selesai normal** tanpa
> kematian. Semua flag yang dipakai aplikasi juga diverifikasi valid terhadap
> `llama-server --help`. Artinya model, flag, dan endpoint **bukan** penyebabnya —
> yang membedakan hidup dan mati adalah **hadirnya renderer Live2D**.
>
> Bukti lengkap: `tools/bukti/mati-saat-inferensi-2026-10-08.log`

| Uji | Hasil | Artinya |
|---|---|---|
| `VTUBER_STUB=true` | hidup ≥45 s | llama tidak terlibat |
| `VTUBER_LLM_PROVIDER=local` (`-ngl 0`) | hidup 50 s, llama-server **selesai memuat**, listening di :8788 | llama sendiri sehat |
| Vulkan (`-ngl 99`) | sebagian hidup, sebagian mati | jalur Vulkan dicurigai |
| Event Log (`Get-WinEvent`) | kosong | bukan crash biasa |
| WER LocalDumps | **tidak menghasilkan dump** | fail-fast tidak lewat jalur WER |
| `FailFast`/`Environment.Exit` di `src/` | tidak ada | bukan kode kita yang memanggilnya |

> ⚠️ **Jangan menyimpulkan dari satu kali "proses hilang".** Loop uji yang
> sekadar memeriksa `tasklist` **tidak bisa membedakan crash dari jendela yang
> ditutup**. Dua dari kematian yang teramati ternyata jendela uji yang ditutup
> pengguna — terbukti dari entri `DisposeAsync` di log (lihat §7.19).
> **Tanda crash yang sebenarnya:** log berakhir pada baris `[llama] ...`
> **tanpa** entri exception sesudahnya.

#### Pembaruan 2026-10-08 (sore) — dua asumsi lama terbukti SALAH

Diukur ulang dengan `tools/uji-kematian.py` (mencatat **exit code utuh 32 bit**
dan memori bebas; versi bash sebelumnya cuma mengembalikan 8 bit bawah sehingga
`0xC0000602` terbaca sebagai `2`). Hasilnya menumbangkan dua kesimpulan lama:

| Asumsi lama | Kenyataan |
|---|---|
| "fail-fast senyap di tingkat driver" | **Exit code-nya `0` — keluar bersih.** Bukan fail-fast, bukan exception, bukan `DisposeAsync`. Tidak ada satu pun entri Event Log pada menit kematian (diperiksa Application + System, hanya ada id 16394 SPP yang tak terkait) |
| "rebutan driver GPU antara D3D11 dan Vulkan llama-server" | **Salah.** Dengan `VTUBER_VULKAN_NGL=0` (nol layer di GPU, llama murni CPU) aplikasi **tetap mati 2 dari 6 jalan**. Dan pada satu kematian, `llama-server` **tidak pernah menyala sama sekali** (nol baris `[llama]`, memori bebas masih 3,3 GB) — jadi llama bukan penyebab, bukan pemicu, dan tidak perlu ada |

**Fakta perangkat keras yang selama ini tidak tercatat** (dan menjelaskan banyak
hal): GPU-nya **Intel Iris Xe Graphics — grafis terintegrasi yang memakai RAM
sistem sebagai VRAM**. RAM total 16 GB, dan **hanya ~3,7 GB bebas saat idle**.

**Titik matinya selalu sama.** Log berhenti tepat setelah kedua berkas
`FrameworkShaders/*.fx` dibaca di dalam `sebelum GetDeviceInfo` — yaitu di
dalam `CubismShader_D3D11::GenerateShaders`. Ukuran `crash.log` pada titik itu
selalu **13.449 bita**.

**Kompilasi shader Cubism memakan ~17 detik di GPU ini.** Terukur pada jalan
yang selamat: `crash.log` membeku di 13.449 bita dari detik ke-3 sampai
detik ke-17, lalu melonjak ke 31.173 bita dan memori proses naik 184 MB →
1,27 GB (tekstur + rantai mip termuat). Jadi jendela 13–19 detik itu adalah
kompilasi shader, dan **kematian selalu terjadi di dalam jendela itu**.

**Dua mode kematian yang berbeda** — **kini diperbarui menjadi TIGA** (lihat
pembaruan 2026-10-09 di bawah, Mode C) — jangan disatukan:

| Mode | Ciri | Dugaan sebab |
|---|---|---|
| **A — macet di kompilasi shader** | mati ~16 dtk, `crash.log` 13.449 bita, llama boleh jadi belum menyala sama sekali | kompilasi ~70 shader D3D pada Intel Iris Xe; exit 0 menunjukkan proses diakhiri, bukanmeledak |
| **B — memori habis** | mati ~28 dtk, memori bebas menyentuh **3 MiB**, llama-server ikut mati | model 5,1 GB + KV cache f16 ctx 16.384 pada mesin yang hanya punya ~3,7 GB bebas |
| **C — saat Vulkan mulai inferensi** | llama sudah `listening`, lalu mati persis di `launch_slot_: processing task`; tidak butuh kirim pesan (mati juga di `listening` saja) | rebutan driver Intel (D3D11 Cubism vs Vulkan llama) **kembali menguat** untuk mode ini |

Mode B punya perbaikan yang jelas dan **harus dikerjakan tanpa menunggu Mode A**:
`VTUBER_VULKAN_CTX=16384` itu **empat kali lipat** dari
`VTUBER_LOCAL_MODEL_CTX=4096` yang sebenarnya dipakai — murni pemborosan
(~2,3 GB KV cache f16) tanpa manfaat apa pun.

**Sebab pasti Mode A** belum dipastikan. Yang sudah disingkirkan: fail-fast,
exception, penutupan jendela, kehabisan memori, dan llama-server. Penanda
`AppDomain.ProcessExit` sudah dipasang di `CrashLog.Pasang()` — kalau baris
`PROSES KELUAR` muncul di ujung log, berarti proses diakhiri dari dalam
CLR (`Environment.Exit`/`Main` kembali); kalau tidak muncul, berarti
`TerminateProcess` dari luar (driver atau OS). Itu pembedanya.

**Dampak.** Cacat paling serius yang tersisa: fungsi utama (mengobrol dengan LLM
lokal) tidak andal, dan jendela menutup tanpa penjelasan apa pun.

**Langkah berikutnya (urut dari yang paling murah):**

1. ✅ **`VTUBER_VULKAN_CTX` `16384` → `4096`** — bukan sekadar penghematan:
   nilai itu **empat kali lipat** dari `VTUBER_LOCAL_MODEL_CTX=4096` yang
   benar-benar dipakai prompt. Menutup Mode B. **Dikerjakan 2026-10-09.**
   Sekaligus `VTUBER_VULKAN_NGL` `0` → `40`: nilai `0` adalah sisa eksperimen
   dan **tidak** menghemat memori (model 5,1 GB lalu dihitung di RAM murni,
   bukan memori bersama GPU) — ia hanya membebani CPU.
2. **Tahu dulu siapa yang mengakhiri proses** (Mode A). Penanda
   `PROSES KELUAR` sudah terpasang; tangkap satu kematian dan lihat ada tidaknya
   baris itu di ujung `crash.log`. Tanpa ini semua perbaikan berikutnya hanya
   terkaan.
3. Kalau ternyata `TerminateProcess` dari luar, curigai driver Intel: uji
   `D3D11_CREATE_DEVICE_SINGLETHREADED`, atau pindahkan
   `swl2d_stage_create` ke utas kerja ber-stack besar supaya kompilasi 17 detik
   itu tidak memblokir utas UI.
4. **Serialkan inisialisasi** — tunda pemuatan llama sampai panggung Live2D
   melaporkan `panggung N aktif`. Ini tidak menyembuhkan Mode A (terbukti: mati
   pun terjadi saat llama belum menyala), tetapi menghilangkan tumpang tindih
   pemuatan 5 GB dengan kompilasi shader, yang jelas tidak menolong.

**Definisi selesai:** 10 kali jalan berturut-turut bertahan ≥2 menit dengan
Live2D aktif **dan** llama memuat model, tanpa exception di `crash.log`.

#### Pembaruan 2026-10-09 — klasifikasi ulang `jalan-1..9`, Mode C ditemukan

Menghitung ulang sembilan log di `tools/bukti/` dengan penanda `PROSES KELUAR`
(sebelumnya tidak dipakai karena belum ada) menghasilkan gambaran yang jauh
berbeda dari `ringkasan-uji.txt`. **Baris "selamat=4 mati=5" itu keliru**: lima
"kematian" tersebut semuanya **jendela ditutup** — `shutdown: selesai` diikuti
`PROSES KELUAR`, yang artinya jalur normal `MainWindow.OnClosed`.

| Jalan | Penanda akhir | Arti |
|---|---|---|
| 1,2,3,4,5,8,9 | `shutdown: selesai` → `PROSES KELUAR` | jendela DITUTUP — bukan crash |
| **6** | berhenti di `listening on http://127.0.0.1:8788` | **MATI di ambang inferensi** (hidup 25 dtk) |
| **7** | berhenti di `slot launch_slot_: task 0 processing task` | **MATI saat token pertama** (hidup 56 dtk) |

**Jalan 7 mereproduksi keluhan asli Master** ("dikirim, tidak ada balasan apa pun"):

```
02:32:47.050  TAHAP: composer: Enter, panjang draf=4, bisaKirim=True
02:32:47.145  [llama] slot get_availabl: id 0 | task -1 | selected slot by LRU
02:32:47.145  [llama] slot launch_slot_: id 0 | task 0 | processing task
   (log putus — tanpa balasan, tanpa PROSES KELUAR)
```

Proses lenyap di **milidetik** yang sama ketika slot inferensi mulai memproses.
Ini bukan hang dan bukan timeout: benar-benar berhenti. Jalan 6 menunjukkan hal
penting lain — ia mati **36 detik hidup, llama `listening`, tanpa satu pun baris
`composer`** — jadi kematian tidak memerlukan kirim pesan, tetapi **pemicu
terkuatnya adalah saat Vulkan mulai menghitung.**

**Konsekuensi untuk diagnosis lama.** Kesimpulan "bukan rebutan GPU" yang
diambil dari uji `-ngl 0` tetap benar **untuk Mode A/B**, tetapi Mode C justru
memberi bukti sebaliknya: mati tepat ketika Vulkan aktif bekerja. Jadi jangan
generalisasi — ketiga mode punya pemicu sendiri.

**Langkah berikutnya (paling murah dulu), untuk Mode C:**

1. **Uji silang sekali**: jalankan aplikasi dengan panggung Live2D dimatikan
   (llama hidup sendiri), lalu sebaliknya (Live2D hidup, llama jangan dimuat).
   - Kalau C hilang saat Live2D mati → rebutan driver terbukti → kerjakan
     langkah serial (poin 4 di daftar atas).
   - Kalau C tetap ada saat Live2D mati → penyebabnya di llama/Vulkan sendiri,
     dan `VTUBER_VULKAN_CTX`/`NGL` yang harus dikecilkan lebih agresif.
2. Pastikan `CrashLog` tetap dipanggil di jalur shutdown **sebelum** proses
   benar-benar berhenti, supaya kematian Mode C pun bisa dibedakan dari
   penutupan jendela tanpa harus menebak.

#### Pembaruan 2026-10-09 (lanjutan) — Mode A terbukti INTERMITEN

Menjalankan biner yang **sama persis** empat kali berturut-turut dari folder
keluaran x64 memberi hasil yang berbeda-beda:

| Jalan | Ukuran `crash.log` | Proses | Probe piksel |
|---|---|---|---|
| 1 | **13.449 bita** | MATI | tidak ada |
| 2 | 31.872 bita | HIDUP | belum sampai |
| 3 | 34.669 bita | HIDUP | **ADA** |
| 4 | 34.xxx bita | HIDUP | **ADA** |

**Ini temuan terpenting tentang Mode A sejauh ini: bukan data rusak, melainkan
race condition.** Dugaan lama "shader `.fx` korup" **terbantah** — berkasnya
lengkap dan valid (`CubismEffect.fx` 5.389 bita / 154 baris;
`CubismBlendMode.fx` 16.611 bita / 524 baris; seluruh blok kurung tutup utuh).
Kalau datanya rusak, semua jalan akan mati, bukan sebagian.

Titik mati yang tepat (`tools/bukti/mode-A-13449-2026-10-09.log`):

```
TAHAP: [live2d] [swl2d] probe2: D3DCompile(VertCopy) -> 0x00000000   ← SUKSES
TAHAP: [live2d] [swl2d] sebelum GetDeviceInfo
TAHAP: [live2d] [swl2d] MuatBerkas dipanggil: FrameworkShaders/CubismEffect.fx
TAHAP: [live2d] [swl2d] MuatBerkas dipanggil: FrameworkShaders/CubismBlendMode.fx
   (log putus di sini — 13.449 bita, tanpa PROSES KELUAR)
```

`D3DCompile(VertCopy)` sudah mengembalikan `0x00000000` (sukses) sebelum mati,
jadi kompilasi D3D sendiri **sehat**. Kematian terjadi saat penyerahan isi
berkas ke runtime Cubism — bukan saat kompilasi.

**DLL native sudah dicocokkan dan bukan penyebabnya:**
`SilverWolf.Live2D.dll` di folder jalan = `ba295d6190cad0730d07ec8b95ef133c`,
**identik** dengan `native/SilverWolf.Live2D/build/x64/Debug/`.

**Konsekuensi untuk strategi perbaikan:** karena Mode A intermiten, satu
keberhasilan **tidak** membuktikan perbaikan, dan satu kegagalan **tidak**
membuktikan kerusakan. Setiap perubahan pada jalur ini wajib diuji **minimal 5
kali jalan** dan dilaporkan sebagai rasio (mis. "lolos 4/5"), bukan "berhasil".

**Bukti tersimpan:**
- `tools/bukti/mode-A-13449-2026-10-09.log` — jalan yang MATI.
- `tools/bukti/mode-A-lolos-shader-2026-10-09.log` — jalan yang LOLOS; memuat
  `tts: siap (rantai=piper+rvc,piper, rvc=True)`, bukti bahwa pipeline suara
  (M11) benar-benar terbangun di dalam aplikasi nyata.

### 8.2 🟡 LipSync belum ada

Grup `LipSync` (`ParamMouthOpenY`) sudah tersedia di `model3.json` dan **rantai
suaranya sekarang benar-benar menghasilkan audio** (`tools/tts/`), tetapi mulut
belum digerakkan.

Butuh `CubismLipSyncUpdater` (menuntut `CubismUpdateScheduler` yang sengaja
dihindari) atau jalur sederhana: hitung amplitudo RMS per bingkai dari buffer
audio yang sedang diputar, lalu `AddParameterValue(ParamMouthOpenY, a)`. Jalur
kedua lebih cocok dengan pola `SiapkanEfek()` yang sudah ada.

**Prasyarat TERBUKTI TERPENUHI (2026-10-09).** Integrasi TTS (M11) selesai —
pekerja menetap menghasilkan WAV nyata dan `PcmPlayer` sudah memunculkan
`LevelBerubah` tiap 16 ms. Jadi §8.2 **tidak lagi menunggu M11**; ini pekerjaan
mandiri berikutnya, dan jalurnya sudah ada tinggal disambungkan:
amplitudo RMS per bingkai → `AddParameterValue(ParamMouthOpenY, a)` di dalam
`SiapkanEfek()`.

Yang belum dikonfirmasi: audio **terdengar** dari speaker pada mesin Master
(integrasi terbukti di tingkat berkas WAV dan log, bukan telinga). Konfirmasi
terakhir harus dilakukan Master dengan menjalankan aplikasi.

### 8.3 ✅ SELESAI — ukuran jendela 20% lebih kecil daripada aplikasi lama

`AppWindow.Resize` memakai **piksel fisik**, sedangkan Electron memakai **DIP**.
`Resize(1180, 760)` apa adanya membuat jendela 944×608 DIP, dan panggung
Live2D-nya 408 DIP alih-alih ~519 DIP — model pun tampil lebih kecil.

Diperbaiki lewat `Native/WindowsDpi.cs` (`GetDpiForWindow`), **dan** ukurannya
dijepit ke area kerja yang tersedia (lihat §7.20). Terverifikasi di layar 125%:

```
TAHAP: windowing: area kerja 1920x1020, skala 1,250
TAHAP: windowing: Resize(1475,950)
```

1475 = 1180 × 1,25 dan 950 = 760 × 1,25. Panggungnya kini 641×851 piksel.

### 8.4 🟡 Instrumentasi penyidikan masih terpasang

Probe `D3DCompile`, log per `MuatBerkas`, `PeriksaPiksel`, dan alat uji
`SWL2D_UJI`. Berfungsi dan tidak mengganggu, tetapi sebaiknya dijadikan opt-in
sebelum rilis.

### 8.5 ✅ TIDAK LAGI MEMBLOCKIR — data golden `phoneme_ids`

Dulu dicatat sebagai satu-satunya blocker: butuh `piper_phonemize.wasm` berjalan
di browser, tidak bisa dari CLI.

**Ditutup 2026-10-08.** Paket `piper-tts` membawa phonemizer espeak-ng-nya
sendiri, sehingga phonemizer tidak perlu dibangun ulang dan `piper_phonemize.wasm`
tidak dibutuhkan sama sekali. Uji paritas phonemizer tetap *boleh* dilakukan
kalau dianggap berguna, tetapi **bukan penghambat** apa pun.

### 8.6 ✅ SELESAI (2026-10-08) — Status "memuat model" tidak dikenali

~~UI menampilkan OFFLINE padahal model sedang dimuat.~~ **Sudah diperbaiki.**
Pengenalan bentuk badannya dipindah ke `SilverWolf.Core/Inference/HealthProbe.cs`
supaya bisa dikunci unit test (`tests/.../HealthProbeTests.cs`, 6 uji). Total uji
kini **114** (dulu 108).

Masalah aslinya — `OpenAiCompatibleProvider.AvailableAsync`
(`Inference/OpenAiCompatibleProvider.cs` baris 65–74) mencari bentuk badan 503
seperti ini:

```csharp
b.TryGetProperty("status", out var status) && status.GetString() == "loading model"
```

Padahal badan 503 `llama-server` yang **sebenarnya** adalah:

```json
{"error":{"message":"Loading model","type":"unavailable_error","code":503}}
```

Properti `status` tidak ada, jadi cabang itu tidak pernah kena dan fungsi jatuh
ke `{ Ok = false, Loading = false, Reason = "HTTP 503" }`. Akibatnya
`StatusTeks` menjadi `"tidak-jalan"` dan tombol menampilkan **OFFLINE**, padahal
model sedang dimuat. Nilai yang benar: `"memuat"` / `"MEMUAT VULKAN…"`.

**Dampak.** Pengguna mengira LLM tidak jalan lalu menutup aplikasi, padahal
hanya perlu menunggu. Ini juga memperburuk §8.1 karena kematian sering terjadi
tepat pada fase pemuatan yang salah dilaporkan ini.

**Perbaikan.** `HealthProbe.MenandakanSedangMemuat(status, badan)` kini
mengenali `error.message` **dan** bentuk lama `status`, tanpa peduli besar-kecil
huruf. Ia sengaja mensyaratkan **503** dan menolak badan kosong/bukan JSON,
supaya kegagalan nyata tidak tersamar menjadi "memuat". Nilai yang dikembalikan
`Reason = "memuat model ke VRAM..."` → `StatusTeks = "memuat"`.

⚠️ **Belum diverifikasi terhadap llama-server sungguhan** — baru dikunci unit
test. Perlu satu jalan nyata yang menunjukkan "MEMUAT VULKAN…" di UI.

### Sisa pekerjaan, urut disarankan

1. **§8.1 — hentikan kematian senyap.** Ini yang memblokir segalanya. Mulai dari
   langkah `.env` yang paling murah, satu perubahan per uji.
2. **§8.2 — LipSync.** ✅ **M11 selesai** (komit `dfc1125`): pekerja menetap
   menghasilkan WAV nyata, `PcmPlayer` sudah memunculkan `LevelBerubah` tiap
   16 ms. Rantainya tidak perlu diriset lagi — `tools/tts/README.md` memuat cara
   pakai, profil terkunci, dan angka latensi. Yang tinggal: RMS per bingkai →
   `AddParameterValue(ParamMouthOpenY, a)`.
3. **§8.6 — verifikasi pengenalan 503 terhadap llama-server sungguhan.**
   Kodenya sudah dikunci unit test; yang kurang satu jalan nyata yang
   menunjukkan "MEMUAT VULKAN…" di UI.
4. **M10 — tema, blur, animasi, font.** Uji risiko variable font.
5. **M12 — tray, hotkey, single-instance, close-to-tray.**
6. **M13 — integrasi end-to-end.**
7. **M14 — rilis.**

---

## 9. Alat diagnosis (jangan dihapus)

| Alat | Cara pakai | Kegunaan |
|---|---|---|
| **`PeriksaPiksel`** | otomatis, bingkai ke-10 | Membaca kembali **back buffer swap chain**, menghitung piksel terisi + kotak pembatas. Pembeda "`Present()` sukses" vs "benar-benar tergambar" |
| **`SimpanBingkai`** | `SWL2D_TANGKAP=<berkas.bmp>` | Membuang back buffer ke BMP 32-bit. Satu-satunya cara andal menilai bingkai secara visual |
| **Uji ekspresi/gerakan** | `SWL2D_UJI=ekspresi` \| `gerakan` | Membekukan pose lalu mencoba setiap ekspresi/gerakan bergiliran, mencatat sidik piksel tiap entri |
| Penimpaan pembingkaian | `SWL2D_PERBESARAN`, `SWL2D_GESER_X`, `SWL2D_JANGKAR_Y` | Menala bingkai **tanpa membangun ulang** aplikasi C# |
| `SWL2D_DIAG=1` | variabel lingkungan | Penangkap pengecualian + pembuang 32 alamat tumpukan. **Jangan diaktifkan bawaan** |
| `SWL2D_NOLOG=1` | variabel lingkungan | Log native murni ke `swl2d-native.log`, melewati .NET |
| `rva-lookup.py` | `python native/SilverWolf.Live2D/rva-lookup.py build/x64/Release/SilverWolf.Live2D.map 0x5E4D1` | RVA → nama fungsi |
| **`tools/uji-kematian.py`** | `python tools/uji-kematian.py [n] [detik]` | Loop uji kematian §8.1. **Mencatat exit code utuh 32 bit** (versi bash hilang 24 bit atas sehingga `0xC0000602` terbaca `2`), memori bebas terendah, dan nasib llama-server |
| **`tools/probe-keluar.py`** | `python tools/probe-keluar.py [detik]` | Satu jalan dengan garis waktu: status `Running`/`Not Responding`, ukuran `crash.log` tiap 2 dtk, exit code, dan selisih antara tulisan log terakhir dengan keluarnya proses |
| `tools/potret-jendela.py` | `python tools/potret-jendela.py keluar.png "Silver Wolf"` | Pemotret jendela lewat `PrintWindow` — **tetap bekerja walau jendela tertutup jendela lain**. Wajib `SetProcessDpiAwarenessContext` lebih dulu |
| `CrashLog.Tulis` | `Diagnostics/CrashLog.cs` | `File.AppendAllText` — **tidak buffered**, jadi baris terakhir di `crash.log` benar-benar langkah terakhir yang dijalankan |

### Batas yang perlu diketahui saat menguji

- **Input sintetis tidak sampai ke aplikasi.** `SetCursorPos` berhasil, tetapi
  `mouse_event`/`keybd_event` tidak menghasilkan klik atau ketikan di jendela
  aplikasi. Terbukti lewat uji kontrol: mengklik tombol "Elus Kepala" yang sudah
  diketahui menambah interaksi kizuna **tidak mengubah apa pun**. Jadi
  perilaku yang butuh interaksi (mis. Enter untuk mengirim, §7.21) **tidak bisa
  diverifikasi otomatis** di lingkungan ini — harus dicoba langsung oleh
  pengguna.
- **`SetForegroundWindow` dari proses latar ditolak Windows.** Trik menekan ALT
  lebih dulu berhasil membawa jendela ke depan (`GetForegroundWindow()` cocok),
  tetapi tidak membuat input sintetis tembus.
- **`PrintWindow` memotong bagian bawah jendela** sekitar setinggi title bar
  (47 px pada skala 125%). Tata letak yang tampak "terpotong" di potret
  **bukan** bukti tata letak rusak — periksa dengan ukuran klien sebenarnya.

### Menguji kematian berulang (§8.1)

Aplikasi dijalankan berulang, dan **`crash.log` disalin ke berkas terpisah pada
tiap kematian** — jangan dihapus, karena justru log itu yang memuat buktinya:

```bash
T="src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64"
LOG="tools/bukti"; mkdir -p "$LOG"
hidup() { tasklist //FI "IMAGENAME eq $1" 2>/dev/null | grep -c "^$1"; }
for jalan in 1 2 3 4 5 6; do
  taskkill //F //IM SilverWolf.App.exe >/dev/null 2>&1
  taskkill //F //IM llama-server.exe >/dev/null 2>&1
  sleep 4; rm -f "$T/crash.log" "$T/run.out"
  (cd "$T" && ./SilverWolf.App.exe > run.out 2>&1 &)
  for i in $(seq 1 25); do
    sleep 3
    [ "$(hidup SilverWolf.App.exe)" = "0" ] && break
  done
  echo "percobaan $jalan: $(hidup SilverWolf.App.exe)"
  cp -f "$T/crash.log" "$LOG/mati-$jalan.log" 2>/dev/null
done
```

**Cara membaca hasilnya — jangan sampai salah lagi:**

| Akhir log | Artinya |
|---|---|
| baris `[llama] ...`, **tanpa** exception sesudahnya | **crash sungguhan** (fail-fast) |
| ada entri `DisposeAsync` / `ObjectDisposedException` | **jendela ditutup**, bukan crash |
| baris `[live2d] [swl2d] ...` | berhenti di jalur render |

Contoh bukti yang tersimpan: `tools/bukti/kematian-2026-10-07.log`.

### Membaca `crash.log`

Berkasnya di sebelah exe. Penanda penting:

| Penanda | Arti kalau ini yang terakhir muncul |
|---|---|
| `App() mulai` | gagal sangat dini, sebelum XAML |
| `InitializeComponent() selesai` | gagal di dalam `OnLaunched` |
| `akar = …` | akar repo ketemu; lanjut ke baris aset |
| `  HILANG <nama>` | aset itu belum ada — bukan kode yang salah |
| `MainWindow dibuat` | konstruktor `Window` dasar gagal → cek bootstrap ganda (§7.6) |
| `windowing: HWND = 0x…` | `InitializeWindowing()` jalan |
| `MainWindow diaktifkan` | startup tuntas |

### Bukti terukur yang sudah tercapai (Live2D)

| Tahap | Buffer | Piksel terisi | Kotak |
|---|---|---|---|
| viewport 1×1 | 408×528 | 1 | (0,0)-(0,0) |
| `CreateRenderer` ukuran nyata | 408×528 | 5.934 | (159,230)-(272,323) |
| + koreksi aspek & `SetHeight` | 408×528 | 172.339 | (0,30)-(407,527) |
| + skala DPI | 511×661 | 270.419 | (0,37)-(510,660) |
| + bingkai 0.88 & mipmap (akhir) | 511×661 | 108.631 | (38,154)-(510,548) |

**Peredaman ikut-kursor terukur** (sasaran `x=1.0`, ~9 bingkai konvergen):
`0.0000 → 0.1111 → 0.2444 → 0.5111 → 0.9587 → 1.0002`.

**Semua 9 ekspresi berfungsi** — sidik pikselnya berbeda-beda:

| Ekspresi | Terisi | Ekspresi | Terisi |
|---|---|---|---|
| netral | 109.815 | lelah | 109.813 |
| senyum | 109.923 | goda | 113.661 |
| semangat | 109.748 | sebal | 110.274 |
| kaget | 113.276 | sedih | 109.789 |
| bingung | 109.834 | | |

**Semua 4 gerakan berfungsi** (`isyarat`): `berubah-1` 106.849 · `berubah-2`
138.920 · `siklus` 140.867 · `tidur` 103.944.

---

## 10. Jebakan toolchain & lingkungan

1. **`MSBuild.exe` DIBLOKIR kebijakan keamanan**, dan `dotnet build` tidak punya
   `VCTargetsPath` (MSB4019) sehingga tak bisa membangun `.vcxproj`. Bangun
   native lewat `bash native/SilverWolf.Live2D/build-cli.sh [Debug|Release]`.
   Semua jalur ke `cl.exe` harus **gaya Windows** (`cygpath -w`); jalur POSIX
   `/c/...` dibaca sebagai `C:\c\...` → C1083. Lib wajib:
   `ole32.lib user32.lib mincore.lib`.
2. **`dotnet build` tanpa `-p:Platform=x64 -p:SelfContained=true` menghasilkan
   aplikasi framework-dependent yang TIDAK BISA dijalankan.** Folder VS
   (`bin/x64/Debug/...`) self-contained; folder `dotnet build` biasa
   (`bin/Debug/...`) tidak.
3. **Jangan pernah menyalin `runtimeconfig.json`/`deps.json` antar folder
   keluaran** — merusak aplikasi, dan build berikutnya **tidak** memperbaikinya
   karena MSBuild menganggap keluarannya masih baru. Perlu `-t:Rebuild`.
4. `dotnet msbuild`, `reg.exe`, `Add-Type`, `New-Object -ComObject` **diblokir**.
   Verifikasi properti MSBuild lewat csproj probe sementara.
5. **`kill -0 $PID` tidak bisa dipakai** cek proses hidup — pakai
   `tasklist //FI "IMAGENAME eq SilverWolf.App.exe"`.
6. **Pengaman safe-delete** menolak hapus permanen bila trash gagal — jangan
   diakali.
7. **Python tanpa `SetProcessDpiAwarenessContext` melaporkan DPI/skala yang
   SALAH** (ter-virtualisasi). Pernah menyesatkan: `GetDeviceCaps` bilang 96 DPI
   padahal layar 125%. Pillow hanya ada di Python sistem 3.10, bukan di venv.
8. **`PublishTrimmed` harus tetap False.** Trimming + XAML/WinRT = kerusakan
   dikenal. Rilis **x64 saja** — llama.cpp Vulkan hanya x64.
9. **Jangan pakai folder `assets` sebagai penanda akar repo** — Windows
   case-insensitive sehingga `src/SilverWolf.App/Assets/` ikut cocok.

---

## 11. Jebakan API Cubism 5

- `CubismFramework::StartUp` menyimpan **penunjuk** ke `Option`
  (`CubismFramework.cpp:61`). `Option` WAJIB hidup sepanjang proses.
- `CubismMatrix44::Scale` **menimpa** (`_tr[0]=x; _tr[5]=y;`), bukan mengalikan.
  Untuk zoom pakai `CubismModelMatrix::SetHeight(2.0f * zoom)`.
- `CreateRenderer(w, h)` sudah memanggil `Initialize` sendiri — jangan dobel.
- `StartFrame(context)`/`EndFrame()` wajib mengapit `DrawModel`.
- `CreateRenderer` juga menentukan **viewport** (`PreDraw` →
  `SetDefaultRenderState`). Ukuran 1×1 = model hanya satu piksel.
- Koreksi aspek mengoreksi sisi yang **lebih kecil**, bukan selalu sumbu Y
  (`LAppLive2DManager.cpp:256-263`).
- `csmLoadFileFunction` bertipe `const std::string` (by value).
- Kanvas model silverwolf = **0,6 × 0,4** satuan ternormalisasi.
- Perubahan nama Cubism 4 → 5: `Model/CubismModelSettingJson.hpp` → root;
  `CubismRenderer_D3D11` → `Rendering::`; `CubismDefaultParameterId::ParamAngleX`
  (class) → `DefaultParameterId::` (namespace, `const csmChar*`);
  `Option::ReleaseFileFunction` → `ReleaseBytesFunction`; `GetRenderer()` →
  `GetRenderer<T>()`; `SetMvpMatrix(const&)` → butuh `CubismMatrix44*` non-konstan;
  `AddParameterValue(namaChar*, f)` → butuh `CubismIdHandle` lewat
  `GetIdManager()->GetId(...)`.
- **Tekstur 4096² harus punya rantai mip** (§7.16).
- **Efek hidup tidak butuh `CubismUpdateScheduler`** — `CubismBreath`,
  `CubismEyeBlink`, dan `CubismLook` bisa dipanggil langsung dengan urutan
  `CubismUpdateOrder` (`ICubismUpdater.hpp`):
  **kedip(200) → ekspresi(300) → pandangan(400) → napas(500) → fisika(600)**.
  `SaveParameters()` harus **sebelum** efek hidup, kalau tidak nilainya
  menumpuk tiap bingkai.
- Peredaman pandangan memakai `CubismTargetPoint` (`Set`/`Update`/`GetX`/`GetY`).
- `CSM_ASSERT` adalah **no-op** tanpa `CSM_DEBUG`, dan `CSM_DEBUG` dinonaktifkan
  di `CubismFrameworkConfig.hpp:18`.
- `CSM_DELETE_SELF` null-safe (`if (!obj) break;`).

---

## 12. Konvensi kode

- **Nama berkas selalu Inggris** (`CharacterVault.cs`, `TimeTool.cs`,
  `MasterProfile.cs`) walau isinya ber-identifier Indonesia. Pola ini sudah
  berlaku di seluruh `src/` dan harus dijaga.
- **Identifier C# Inggris**, tetapi **nilai string yang dilihat pengguna tetap
  Indonesia** (`"siap"`, `"memuat"`, `"tidak-jalan"`, `"warm"`, `"stranger"`).
  Nilai itu ikut menentukan perilaku prompt LLM — jangan diterjemahkan.

  ⚠️ **Kenyataan di lapisan domain/services: mayoritas identifier memang
  Indonesia** (`BacaFaktaAsync`, `GabungSystem`, `MenandakanSedangMemuat`,
  `PersistAsync`) karena diterjemahkan langsung dari `apps/server-node/*.js`.
  Kode **baru di lapisan itu ikut gaya Indonesianya** — mencampur satu berkas
  dengan dua bahasa lebih merugikan daripada menyimpang dari aturan di atas.
  Berkas UI (`SilverWolf.App`) justru lebih konsisten Inggris; ikuti gaya
  berkas yang sedang disunting.
- **`Live2DStage.cpp` telanjur memakai identifier Indonesia** (`Panggung`,
  `Catat`, `Muat`, `Gambar`, `perangkat`, `swapChain`, `MainkanGerakanBerulang`).
  **Ikuti gaya yang sudah ada** di berkas itu; jangan campur dengan Inggris di
  berkas yang sama. Komentar ditulis Indonesia.
- Nama berkas view/VM mengikuti identifier Inggris; padanan Indonesianya
  dicantumkan di komentar.
- Subfolder model memakai nama Indonesia (`tekstur`, `ekspresi`, `gerakan`) dan
  diacu literal oleh `.model3.json` — jangan diterjemahkan.
- Istilah domain (`cspell.config.yaml`): `moc3`, `model3`, `physics3`, `cdi3`,
  `exp3`, `motion3`, `artmesh`, `lipsync`, `contentvec`, `rmvpe`.

---

## 13. Peta migrasi (ringkasan)

Detail lengkap ada di `docs/migrasi/` — masih dipakai dan dirujuk kode.

### Bentuk aplikasi (dari `01-peta-fitur.md`)

- **Tidak ada router, tidak ada multi-halaman, tidak ada halaman pengaturan.**
  Satu layar, dua panel: **Panggung** (Live2D) dan **Konsol** (chat + HUD Kizuna).
- **Satu store Pinia** (`useCompanionStore`) → jadi **satu**
  `CompanionViewModel`.
- **Tidak ada IPC Electron yang hidup** — preload script mati. Renderer bicara
  HTTP ke server Node; memindahkan backend ke C# tinggal mengganti lapisan
  transport.
- `uno.config.js` adalah **konfigurasi mati** — tidak pernah di-wire ke Vite.
- Endpoint lama → pemanggilan C#: `/api/chat` → `CompanionBackend.ChatAsync`;
  `/api/kizuna` → `GetKizuna`; `/api/kizuna/touch` → `TouchAsync`;
  `/api/proactive` → `ProactiveAsync`; `/health` → `GetHealthAsync`.

### Dependensi (dari `02-dependensi.md`)

Tiga paket npm tanpa padanan .NET sudah direimplementasi:

| npm | Padanan C# |
|---|---|
| `@aituber-onair/kizuna` | `Core/Domain/Kizuna/*` (paling kompleks) |
| `@aituber-onair/voice` | `Core/Text/EmotionParser.cs` |
| `@aituber-onair/core` | `Core/Domain/TieredMemoryEngine.cs` |

Artefak browser yang **sengaja tidak dipindah**: `public/onnx/ort-wasm-*.wasm`
(83 MB), `public/piper/piper_phonemize.wasm` (18 MB),
`public/live2dcubismcore.min.js` (204 KB).

### Status per proyek (dari `03-langkah-migrasi.md`)

`SilverWolf.Core` ✅ · `SilverWolf.Services` ✅ · `SilverWolf.App` ✅ ·
`SilverWolf.TtsWorker` ⬜ (M11) · `native/SilverWolf.Live2D` 🟢 (M8 berjalan) ·
`native/SilverWolf.Phonemizer` ⬜ (M11).

---

## 14. Riwayat migrasi aset

Keputusan user: **pindahkan**, bukan salin. Ukuran dipindah **6,9 GB**:

| Tujuan | Ukuran | Isi |
|---|---|---|
| `model/` | 4,8 GB | `gemma-4-E4B-it-UD-Q4_K_XL.gguf` |
| `assets/` | 1,7 GB | `piper`, `rvc`, `voices`, `whisper`, `model-dasar`, `live2d/silverwolf` |
| `bin/llama/` | 91 MB | binary llama.cpp Vulkan |
| `silver_wolf_memory/` | 130 KB | **data pribadi** — persona, Fakta, Mood, Riwayat |
| `.env` | 5 KB | konfigurasi |

`.gitignore` diperbaiki: `assets/` (1,7 GB) sebelumnya tidak diabaikan dan akan
ikut ter-commit. Sekarang seluruh aset besar, model berlisensi, dan
`silver_wolf_memory/` diabaikan.

**Yang diselamatkan sebelum aplikasi lama dihapus:** memori pengembangan
(`docs/arsip/memori-web/`, 6 berkas), data golden kizuna, sumber lengkap
(`docs/arsip/sumber-web/`, 51 berkas), dan seluruh aset 6,9 GB.

Konsekuensi yang sudah diterima: **referensi implementasi untuk M9/M10/M11
hilang** — `style.css` (1.318 baris, tema M10), `piper-phonemize.js` (3.094
baris, phonemizer M11), dan sumber UI `App.jsx`. Yang tersisa hanya arsip
`sumber-web/`.

### Cara menguji tanpa model 4,8 GB

```bash
# .env
VTUBER_STUB=true
```

`CompanionRuntime.PilihProvider` memilih `StubProvider`, yang mengalirkan tiga
potongan tetap (`"[senyum] "`, `"Sistem inti sudah hidup, "`, `"Master."`) dan
selalu melapor `Ok = true`. Artinya seluruh jalur streaming UI teruji tanpa
mengunduh model apa pun, `llama-server` tidak pernah dinyalakan, dan kizuna
**tetap mencatat**.

Untuk menguji status "belum siap", balikkan ke `VTUBER_STUB=false` tanpa
menjalankan `llama-server`: `StatusText` akan menjadi `"memuat"` lalu
`"tidak-jalan"`.

---

## 15. Aturan yang berlaku sepanjang proyek

1. **Selesaikan milestone berurutan.** Setiap milestone berakhir dengan solusi
   yang kompilasi **dan bisa dijalankan**.
2. **Build, lalu JALANKAN.** Build sukses tidak berarti aplikasi jalan.
3. **Jangan percaya `BeforeTargets` MSBuild diam-diam benar.** Target yang tidak
   ada di project hanya menghasilkan peringatan lalu MSBuild melanjutkan.
4. **MSIX mengelompokkan payload berdasarkan DESTINASI, bukan sumber.**
5. **Verifikasi jalur SDK lewat probe MSBuild, bukan asumsi.**
6. **Jangan pernah hapus `assets/` secara utuh** — `Assets\` dan `assets\`
   adalah folder yang sama.
7. **`WindowsAppSdkBootstrapInitialize` harus tetap `false`** selama
   `WindowsAppSDKSelfContained=true`.
8. **Fail-fast tidak bisa ditangkap `try/catch`.** `CrashLog` + penanda tahap
   adalah alat diagnosis utama, bukan tambahan opsional.
9. **XML tidak mengizinkan `--` di dalam komentar.**
10. **Memasang komponen Visual Studio bisa mengubah perilaku aplikasi yang sudah
    berjalan.** "Berhasil kemarin" bukan jaminan.
11. **Bangun ulang dari sumber terkini sebelum menyimpulkan sebuah konfigurasi
    rusak** (§7.9).
12. **Satu kali mati bukan bukti** adanya regresi — uji berulang (§8.1).
13. **"Sudah dipindah semua" harus diverifikasi, bukan diasumsikan** (§7.17).

---

## 16. Riwayat revisi dokumen ini

| Tanggal | Perubahan |
|---|---|
| 2026-10-07 | Dibuat. Menggabungkan `RIWAYAT-MIGRASI.md`, `PROMPT-LANJUTAN.md`, `PROGRES-LIVE2D.md`, `PROMPT-LANJUTAN-LIVE2D.md`, dan `SERAH-TERIMA-M8.md` menjadi satu dokumen. Status diperbarui: M8 berjalan, M9 selesai, cacat Debug ditutup (§7.9), efek hidup & bingkai & mipmap ditambahkan |
| 2026-10-07 (lanjutan) | §7.19 pembongkaran membuang CTS yang masih dipakai (diperbaiki); §8.3 ukuran jendela diperbaiki lewat `Native/WindowsDpi.cs`; §8.1 laju kejadian dikoreksi jadi ~1 dari 10 **dan** cara menghitungnya diperjelas — "proses hilang" belum tentu crash; §9 ditambah prosedur uji kematian berulang |
| 2026-10-07 (lanjutan 2) | §7.20 ukuran jendela dijepit ke area kerja — kotak input pernah keluar layar; §7.21 Enter untuk mengirim pesan; §9 ditambah daftar batas pengujian (input sintetis tidak sampai ke aplikasi, `PrintWindow` memotong bagian bawah) |
| **2026-10-08** | **Diagnosis "tidak ada balasan & tidak ada suara".** §8.1 dinaikkan ke 🔴 dan ditambah reproduksi **2 dari 2** di dua titik berbeda + uji kontrol yang membuktikan `llama-server` sendirian sehat (prompt 2003 token selesai normal) — jadi penyebabnya bukan model/flag/endpoint, melainkan hadirnya renderer Live2D. §8.1 langkah berikutnya diisi daftar tuas `.env` konkret. §8.5 ditutup (phonemizer tidak lagi menjadi blocker). §8.6 **baru**: 503 "Loading model" tidak dikenali sehingga UI menampilkan OFFLINE. §2 M11 diperbarui: rantai suara **terbukti berjalan** di `tools/tts/`, sisa pekerjaan adalah integrasi. Header §0 diberi peringatan cacat aktif. |
| **2026-10-08 (lanjutan)** | **Profil suara dikunci.** `VTUBER_RVC_TRANSPOSE` `9` → `-3` di `.env` setelah pengukuran ulang: Piper Indonesia bersuara tinggi (264,8 Hz), sehingga +9 menghasilkan 442,7 Hz; -3 menghasilkan 218,8 Hz, paling dekat dengan 223,8 Hz yang dicatat pada 29 Sep. Skrip `tools/tts/` kini membaca seluruh parameter dari `.env` sebagai sumber tunggal. Hasil acuan: `tools/tts/contoh/04-transpose-3.wav`. Ditambah `tools/tts/README.md` dan `docs/README.md` (indeks). |
| **2026-10-08 (lanjutan 3)** | §0 diperkaya agar sesi AI berikutnya tidak perlu menemukan ulang hal yang mahal: blok **PEKERJAAN SUARA** (lokasi kedua lingkungan Python, profil terkunci, tiga jebakan wajib), penunjuk `docs/README.md`, dan peringatan agar `silver_wolf_memory/` tidak disentuh maupun ditaut. |
| **2026-10-09** | **Alat waktu — Silver Wolf kini tahu tanggal & jam, dan ingat ulang tahun Master (16 Oktober, lahir 2005-10-16).** §7.23 baru: alat **otomatis** (`IAlat.Otomatis`), bukan function calling, karena model tidak andal mengeluarkan `tool_calls`. Berkas baru `Core/Domain/Tool.cs`, `TimeTool.cs`, `MasterProfile.cs`; `PersonaComposer.GabungSystem` dapat parameter `konteksAlat`; `AgentService` & `CompanionRuntime` memuat `Profil.md`. Tanggal lahir tersimpan di `silver_wolf_memory/Profil.md` (folder di-gitignore) + satu baris di `Fakta.md`. §12 konvensi dikoreksi: nama berkas Inggris, identifier domain memang Indonesia dan kode baru di lapisan itu ikut gaya tersebut. |

---

## 17. Laporan masalah terpisah

Daftar masalah yang belum selesai **beserta tingkat, sebab, dan dampaknya**
dipelihara terpisah di **`docs/LAPORAN-MASALAH.md`**. Dokumen itu adalah
ringkasan untuk pengambilan keputusan; dokumen ini adalah acuan lengkapnya.

### Peta dokumen (siapa acuan untuk apa)

| Dokumen | Acuan untuk |
|---|---|
| **`docs/PROYEK.md`** (berkas ini) | **Segalanya.** Dokumen tunggal proyek |
| `docs/LAPORAN-MASALAH.md` | Prioritas: apa yang rusak, seberapa parah, dampaknya |
| `docs/README.md` | Indeks navigasi seluruh dokumentasi |
| `README.md` (akar) | Orientasi cepat: prasyarat, struktur, cara build & jalan |
| `tools/tts/README.md` | **Rantai suara**: cara pakai, profil terkunci, angka tuning & latensi |
| `docs/migrasi/01-peta-fitur.md` | Pemetaan layar/panel/state/endpoint → padanan C# |
| `docs/migrasi/02-dependensi.md` | Penggantian dependensi pihak ketiga + alasan `APPX1101` |
| `docs/migrasi/03-langkah-migrasi.md` | Langkah per proyek + status + blocker |
| `docs/arsip/` | **Arsip — jangan dipakai sebagai acuan.** Dokumen lama, memori pengembangan aplikasi web, sumber lengkap aplikasi web |
| `silver_wolf_memory/` | **Bukan dokumentasi proyek.** Memori karakter (data runtime). **Jangan ditaut, jangan disunting** dari sini — lihat `silver_wolf_memory/_PETUNJUK.md` |
| `.workbuddy-ai/memory/` | Catatan kerja sesi AI. Bukan acuan proyek |
