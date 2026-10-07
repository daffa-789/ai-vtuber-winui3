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
| Verifikasi terakhir | build x64 **0 error / 0 warning** · **108 unit test lulus** · aplikasi dijalankan dan model Live2D tampil · **rantai suara terbukti** (`tools/tts/`, hasil acuan `contoh/04-transpose-3.wav`) |
| ⚠️ Cacat aktif | aplikasi **mati senyap** — direproduksi 2/2 pada 2026-10-08 (§8.1). Ini penghambat utama sebelum TTS diintegrasikan |

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

⚠️  CACAT AKTIF, BACA SEBELUM APA PUN: aplikasi MATI SENYAP. Direproduksi
    2 dari 2 peluncuran pada 8 Okt 2026 (sekali saat membuat panggung Live2D,
    sekali saat prompt LLM pertama diproses). Tidak ada Event Log, tidak ada
    dump WER, tidak ada DisposeAsync — fail-fast senyap. Dugaan terkuat:
    rebutan driver GPU antara D3D11 (Cubism) dan Vulkan (llama-server).
    Bukti: tools/bukti/mati-saat-inferensi-2026-10-08.log
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
  dotnet test tests/SilverWolf.Core.Tests/...             -> 108 lulus
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
| M11 | TTS: phonemizer, Piper, NAudio, lip-sync | 🟡 **rantai terbukti** (`tools/tts/`), belum diintegrasikan |
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

**Belum.** Yang tersisa murni pekerjaan integrasi, bukan riset:
`src/SilverWolf.TtsWorker/`, pemutaran NAudio, cache (`VTUBER_TTS_CACHE`),
lip-sync `ParamMouthOpenY`, dan mengganti `HealthSnapshot.Tts` yang masih
di-hardcode `"siap"`. **Integrasi sengaja ditahan sampai §8.1 tertutup** —
menambah fitur ke aplikasi yang mati senyap tidak ada gunanya.

**Blocker yang masih nyata:** hanya §8.1 (kematian senyap).

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
TAHAP: [live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=0.88, geser=(-0.10,0.10)
TAHAP: [live2d] [swl2d] periksa piksel (bingkai ke-10): 511x661, terisi=108631, kotak=(38,154)-(510,548)
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

**Nilai akhir (dipilih dari pengukuran):**

| Perbesaran | Piksel terisi | Kotak | Ujung sayap terpotong |
|---|---|---|---|
| 1.85 (lama) | 270.523 | (0,32)-(510,660) | ya, parah (kepala saja) |
| 1.00 | 129.627 | (47,129)-(510,575) | 90 piksel di tepi kanan |
| **0.88 + geser −0.10** | **103.565** | **(39,155)-(510,547)** | **12 piksel** |
| 0.75 | 75.268 | (99,170)-(510,504) | 3 piksel, tetapi model terlalu kecil |

`GeserX = -0.10` perlu karena seni model ini **tidak simetris** — sayap
mekaniknya jauh lebih panjang ke kanan.

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

**Sebab.** Belum dipastikan. Dugaan terkuat tetap **fail-fast di tingkat driver
GPU** — rebutan antara perangkat D3D11 kita dan konteks Vulkan llama-server
(`VTUBER_VULKAN_NGL=99`, `VTUBER_VULKAN_CTX=16384`, KV cache f16).

**Dampak.** Cacat paling serius yang tersisa: fungsi utama (mengobrol dengan LLM
lokal) tidak andal, dan jendela menutup tanpa penjelasan apa pun.

**Langkah berikutnya (urut dari yang paling murah):**

1. **Kurangi tekanan VRAM lewat `.env`, satu perubahan per uji** (jangan
   mengubah beberapa sekaligus, atau tidak akan ketahuan mana yang bekerja):
   - `VTUBER_VULKAN_CACHE_TYPE` `f16` → `q8_0` (KV cache jadi separuh)
   - `VTUBER_VULKAN_CTX` `16384` → `8192`
   - `VTUBER_VULKAN_NGL` `99` → `20`–`30`
   - `VTUBER_VULKAN_MUAT_BOOT` `ya` → `tidak` — supaya pemuatan LLM tidak
     berbarengan dengan pembuatan perangkat D3D11 Live2D. Saat ini keduanya
     memang overlap by design: `CompanionRuntime.StartAsync` memuat llama lewat
     `Task.Run` sementara `StageView` membuat panggung di waktu yang sama.
2. **Serialkan inisialisasi** kalau langkah 1 menguatkan dugaan GPU: tunda
   pemuatan llama sampai panggung Live2D melaporkan `panggung dibuat`, atau
   sebaliknya.
3. **Kumpulkan sampel** dengan loop uji §9 sambil menyimpan `crash.log` tiap
   kematian, lalu saring dengan tabel di §8.1.

**Definisi selesai:** 10 kali jalan berturut-turut bertahan ≥2 menit dengan
Live2D aktif **dan** llama memuat model, tanpa exception di `crash.log`.

**Definisi selesai:** 10 kali jalan berturut-turut bertahan ≥2 menit dengan
Live2D aktif **dan** llama memuat model, tanpa exception di `crash.log`.

### 8.2 🟡 LipSync belum ada

Grup `LipSync` (`ParamMouthOpenY`) sudah tersedia di `model3.json` dan **rantai
suaranya sekarang benar-benar menghasilkan audio** (`tools/tts/`), tetapi mulut
belum digerakkan.

Butuh `CubismLipSyncUpdater` (menuntut `CubismUpdateScheduler` yang sengaja
dihindari) atau jalur sederhana: hitung amplitudo RMS per bingkai dari buffer
audio yang sedang diputar, lalu `AddParameterValue(ParamMouthOpenY, a)`. Jalur
kedua lebih cocok dengan pola `SiapkanEfek()` yang sudah ada.

**Prasyarat:** audio harus sudah diputar di dalam aplikasi. Jadi §8.2 menunggu
integrasi TTS (M11) selesai lebih dulu.

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

### 8.6 🟠 Status "memuat model" tidak dikenali — UI menampilkan OFFLINE

`OpenAiCompatibleProvider.AvailableAsync` (`Inference/OpenAiCompatibleProvider.cs`
baris 65–74) mencari bentuk badan 503 seperti ini:

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

**Perbaikan.** Kenali juga bentuk `error.message == "Loading model"`, atau
perlakukan setiap 503 dari llama-server sebagai `Loading = true`.

### Sisa pekerjaan, urut disarankan

1. **§8.1 — hentikan kematian senyap.** Ini yang memblokir segalanya. Mulai dari
   langkah `.env` yang paling murah, satu perubahan per uji.
2. **§8.6 — perbaiki pengenalan 503.** Kecil, aman, dan langsung menghilangkan
   kebingungan "OFFLINE padahal sedang memuat".
3. **M11 — integrasikan rantai suara yang sudah terbukti.** Rantainya tidak
   perlu diriset lagi: `tools/tts/README.md` sudah memuat cara pakai, profil
   terkunci, dan angka latensi. Yang tinggal: `SilverWolf.TtsWorker`
   (OutputType Exe, **tanpa** WindowsAppSDK, `Microsoft.ML.OnnxRuntime` di sana
   saja), pemutaran NAudio, cache, lalu LipSync (§8.2).
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

- **Identifier C# Inggris**, tetapi **nilai string yang dilihat pengguna tetap
  Indonesia** (`"siap"`, `"memuat"`, `"tidak-jalan"`, `"warm"`, `"stranger"`).
  Nilai itu ikut menentukan perilaku prompt LLM — jangan diterjemahkan.
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
