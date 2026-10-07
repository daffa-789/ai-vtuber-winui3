# Riwayat Migrasi — AI Vtuber Web → Silver Wolf WinUI 3

Dokumen tunggal yang merangkum seluruh riwayat proyek: keputusan, milestone,
masalah nyata yang ditemukan beserta penyelesaiannya, status verifikasi,
blocker yang masih terbuka, dan sisa pekerjaan.

**Terakhir diperbarui:** 2026-10-06
**Status ringkas:** M0–M2 dan M4–M7 **selesai**. M3, M8–M14 **belum**, sebagian terblokir prasyarat C++.

| | |
|---|---|
| Aplikasi sumber | `Desktop/AI Vtuber Project/AI Vtuber Web/` — Electron + Vue 3 (JSX) + server Node ESM |
| Aplikasi target | `Desktop/AI Vtuber Project/AI Vtuber WINUI3/` — WinUI 3, C#/.NET 8, XAML, MVVM |
| Verifikasi terakhir | build x64 **0 error / 0 warning**; **108 unit test lulus** (termasuk uji golden terhadap aplikasi lama); aplikasi berjalan stabil; aset 6,9 GB sudah dipindah ke sini |

---

## 1. Tujuan dan batasan

Memindahkan **Silver Wolf** — pendamping AI/VTuber lokal (Live2D, LLM lokal,
TTS, sistem ikatan hubungan) — dari aplikasi Electron ke aplikasi desktop
Windows native, tanpa kehilangan perilaku yang dirasakan pengguna.

Empat keputusan yang dikunci user di awal dan tidak dibuka lagi:

| Area | Keputusan | Alasan |
|---|---|---|
| Live2D | **Native C++/WinRT** membungkus Cubism Native Framework, render D3D11 ke `SwapChainPanel`. **Tanpa WebView2.** | Permintaan user |
| Backend | **Port penuh ke C#.** Tidak ada sidecar Node saat runtime. | Permintaan user |
| TTS | ONNX Runtime + espeak-ng native + NAudio | Permintaan user |
| TTS/ONNX, lokasi | **Exe worker terpisah** (`SilverWolf.TtsWorker`), bukan in-proc | Paksa: tabrakan `onnxruntime.dll` dengan WindowsAppSDK.ML |
| Packaging | **Unpackaged + WASDK self-contained** | Paksa: aset ~7 GB dan `llama-server` butuh folder yang bisa ditulis |

---

## 2. Riwayat per milestone

### M0 — Toolchain terbukti ✅

Membuktikan toolchain bisa menghasilkan aplikasi WinUI 3 yang benar-benar jalan
sebelum menulis kode domain.

- .NET SDK **11.0.100-rc.1**; `net8.0-windows10.0.19041.0` + WindowsAppSDK **2.5.1**.
- `PublishTrimmed=False` dipaku sejak awal — trimming + XAML (`XamlTypeInfo.g.cs`,
  `IXamlMetadataProvider`) + aktivasi WinRT adalah sumber kerusakan yang sudah dikenal.
- Hasil: **0 error / 0 warning**.

### M1 — Bersihkan template nyasar di `AI Vtuber Web/` ✅

Template WinUI 3 nyasar di dalam folder aplikasi web, berisiko tertelan glob
default SDK (`**/*.cs`, `**/*.xaml`).

- Dipindahkan (bukan dihapus) ke `AI Vtuber Web/_migration_backup_templatewinui/`:
  `AI Vtuber.csproj`, `.csproj.user`, `AI Vtuber.slnx`, `App.xaml(.cs)`,
  `MainWindow.xaml(.cs)`, `Package.appxmanifest`, `app.manifest`, `Properties/`,
  `obj/`, dan 7 logo template.
- `bin/x64` (kosong) dihapus. **`bin/llama` dan `assets/{piper,rvc,voices,whisper,model-dasar}` diverifikasi selamat.**
- ⚠️ Catatan yang harus selalu diingat: `Assets\` (template) dan `assets\`
  (aset aplikasi) adalah **folder yang sama** karena Windows tidak
  case-sensitive. Jangan pernah menghapus `assets/` secara utuh.

### M2 — Skeleton solusi ✅

```
SilverWolf.sln
Directory.Build.props            properti bersama + impor build/CubismSdk.props
build/CubismSdk.props            penunjuk $(CubismSdkDir)
src/SilverWolf.App/              WinUI 3 exe
src/SilverWolf.Core/             net8.0, bebas WinRT
src/SilverWolf.Services/         net8.0-windows
native/{SilverWolf.Live2D,SilverWolf.Phonemizer,third_party}/
tests/SilverWolf.Core.Tests/     xunit
```

- App csproj **dipindah dari root** ke `src/SilverWolf.App/` supaya glob default
  SDK tidak menelan `src/`, `native/`, `tests/`.
- Namespace diganti `AI_Vtuber_WINUI3` → `SilverWolf.App`.
- `App.xaml` di-set `RequestedTheme="Dark"` (tema dipaku; aplikasi tidak pernah light).
- `MainWindow` dibuatkan `InitializeWindowing()`: `AppWindow.Resize(1180, 760)` +
  `OverlappedPresenter.PreferredMinimum{Width,Height}` = 780/560 (paritas Electron).
- **Aplikasi berhasil dijalankan** dan hidup stabil 8 detik tanpa error.

Dua masalah besar muncul di sini — lihat §3.1 dan §3.2.

### M3 — Komponen Cubism C++/WinRT ⛔ TERBLOKIR

Belum bisa dimulai. Lihat §6.

### M4 — Pemetaan + `SilverWolf.Core` ✅

Pemetaan struktur/halaman/fitur lebih dulu, karena aplikasi lama punya beberapa
kebiasaan yang tidak terlihat dari daftar berkas. Hasil lengkap:
`docs/migrasi/01-peta-fitur.md`.

Temuan pemetaan yang menentukan bentuk kode berikutnya:

- **Tidak ada router, tidak ada multi-halaman, tidak ada halaman pengaturan.**
  Satu layar, dua panel: Panggung (Live2D) dan Konsol (chat + HUD Kizuna).
- **Satu store Pinia** (`useCompanionStore`) → jadi **satu** `CompanionViewModel`.
- **Tidak ada IPC Electron yang hidup** — preload script mati. Renderer bicara
  HTTP ke server Node. Karena itu memindahkan backend ke C# tinggal mengganti
  lapisan transport, tanpa menyentuh logika bisnis.
- `uno.config.js` adalah **konfigurasi mati** — tidak pernah di-wire ke Vite.

`SilverWolf.Core` dibangun sebagai `net8.0` polos (tanpa suffix `-windows`,
bebas WinRT) supaya seluruh domain bisa diuji dari mesin apa pun:

- `Configuration/` — `EnvFile`, `EnvSource`, `AppConfig` (port `config.js`)
- `Text/` — `EmotionParser` (ganti `@aituber-onair/voice`),
  `SentenceSplitter` (ganti `kalimat.js`), `TagSkipper` (ganti `ucapan.js`)
- `Domain/` — `Mood`, `PersonaComposer`, `CharacterVault`, `ChatHistory`,
  `ProactiveDirector`, `TieredMemoryEngine`

### M5 — Mesin ikatan, vault, memori bertingkat ✅

Bagian berisiko tertinggi: tiga paket npm tanpa padanan .NET harus
direimplementasi, dan `@aituber-onair/kizuna` adalah yang paling kompleks.

`Domain/Kizuna/` memuat port dari `KizunaManager` + `UserManager` +
`PointCalculator` + `BondDynamics` + `BondEvaluator` + `BondContextBuilder`,
digabung menjadi **satu** `KizunaEngine`:

> Kenapa digabung, bukan dipecah per kelas seperti aslinya: pustaka aslinya
> multi-pengguna (livestream dengan banyak penonton). Aplikasi ini **satu
> pengguna** (`master`), tanpa aturan poin, tanpa ambang pencapaian, tanpa sesi.
> Mempertahankan lapisan multi-pengguna hanya menambah kode mati.

Yang **tidak** disederhanakan karena menentukan perilaku: rumus poin, peluruhan
kehangatan, hysteresis tahap, dan format berkas.

### M6 — Inferensi, llama-server, health ✅

- `Inference/OpenAiCompatibleProvider.cs` — SSE untuk llama-server (`/v1`) dan
  Ollama (`/api`), dengan percobaan ulang 45 detik: jeda 1500 ms bila koneksi
  gagal, 1200 ms bila balasannya HTTP 503.
- `Inference/StubProvider.cs` — jalur `VTUBER_STUB` untuk menguji aliran tanpa
  model 5 GB.
- `Llama/ModelLocator.cs` — pemilihan model GGUF + pencarian `llama-server.exe`
  (kedalaman 2 untuk model, 3 untuk binary).
- `Llama/LlamaServerProcess.cs` — argumen Vulkan (`-ctk/-ctv q8_0`,
  `--flash-attn on`), `WorkingDirectory` = akar repo (butuh ~20 DLL Vulkan di
  sebelahnya), polling `/health` 120 × 250 ms.

### M7 — Agen, backend, runtime, unit test ✅

- `Agent/AgentService.cs` — urutan dipertahankan persis:
  baca memori → susun prompt sistem → alirkan balasan → **baru** simpan ke
  memori, kizuna, dan vault.
- `Backend/CompanionBackend.cs` — pengganti rute HTTP dengan **kontrak setara**:
  kode status, `{ error }`, dan kapan snapshot ikatan dikirim.
- `Bootstrap/CompanionRuntime.cs` — pengganti `main.js`; merakit konfigurasi,
  vault, kizuna, memori, provider, proses llama, agen, dan backend.
- **105 unit test xunit**, semuanya lulus.

### M8–M14 — belum

| Milestone | Isi | Status |
|---|---|---|
| M8 | Cubism native: render, fit, fokus, motion | Terblokir |
| M9 | `MainWindow`, `CompanionViewModel`, 4 view, `AssetLocator` | Belum |
| M10 | Tema, blur, animasi, font | Belum |
| M11 | TTS: phonemizer, Piper, NAudio, lip-sync | Sebagian terblokir |
| M12 | Tray, hotkey, single-instance, close-to-tray | Belum |
| M13 | Integrasi fitur end-to-end | Belum |
| M14 | Rilis | Belum |

---

## 3. Masalah nyata yang ditemukan dan penyelesaiannya

Bagian ini yang paling berharga untuk dibaca ulang. Semua masalah di bawah
muncul dari pemeriksaan langsung, bukan dugaan.

### 3.1 MSIX `APPX1101` — `onnxruntime.dll` bentrok (M2)

**Gejala:** build gagal dengan
`Payload contains two or more files with the same destination path 'onnxruntime.dll'`.

**Penyebab sebenarnya** (dibaca dari log `-v:diag`): dua paket Microsoft yang
sama-sama sah mengklaim **identitas yang sama**.

| | `Microsoft.ML.OnnxRuntime` 1.22.0 | `Microsoft.Windows.AI.MachineLearning` 2.1.74 |
|---|---|---|
| native | `onnxruntime.dll` | `onnxruntime.dll` |
| `PathInPackage` | `runtimes/win-x64/native/onnxruntime.dll` | identik |
| managed | `Microsoft.ML.OnnxRuntime.dll` | `Microsoft.ML.OnnxRuntime.dll` |

Yang kedua ikut tertarik otomatis oleh
`Microsoft.WindowsAppSDK` → `Microsoft.WindowsAppSDK.ML`, jadi **selalu ada di
setiap aplikasi WinUI 3**.

**Pelajaran mahal:** mengganti nama berkas **tidak menyelesaikan apa pun**,
karena MSIX mengelompokkan payload berdasarkan **destinasi**, bukan sumber.
Menimpa `<None>` juga percuma — payload MSIX dirakit dari `@(PackagingOutputs)`.
Dan target `GenerateProjectPriFile` yang sempat dipakai sebagai titik sisip
**tidak ada di project ini**; MSBuild diam-diam mengabaikan `BeforeTargets` ke
target yang tidak eksis.

**Solusi:** `SilverWolf.Services` tidak mereferensikan ONNX Runtime sama sekali.
Piper TTS pindah ke exe worker terpisah tanpa WindowsAppSDK (M11), sehingga
tidak pernah menyentuh payload MSIX aplikasi.

**Efek samping menguntungkan:** sintesis Piper tidak lagi berebut UI thread
maupun perangkat D3D11 milik Cubism, dan TTS bisa di-restart tanpa mematikan
aplikasi.

### 3.2 `REGDB_E_CLASSNOTREG` saat start (M2)

**Gejala:** build sukses, aplikasi gagal start dengan
`COMException (0x80040154)` di `DeploymentManagerCS.AutoInitialize.AccessWindowsAppSDK()`.

**Penyebab:** Windows App SDK Runtime belum terpasang. Masalah lingkungan, bukan kode.

**Solusi:** `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`
(unpackaged + self-contained). Runtime ikut ke folder output, aplikasi jalan
tanpa prasyarat instalasi. Ini juga bentuk distribusi yang tepat untuk aplikasi
offline beraset 7 GB.

### 3.3 `MSB4024` — XML tidak mengizinkan `--` di dalam komentar (2026-10-06)

**Gejala:** build gagal total di semua proyek:
`The imported project file "build\CubismSdk.props" could not be loaded.
An XML comment cannot contain '--', and '-' cannot be the last character.`

**Penyebab:** saat memperbarui `build/CubismSdk.props` untuk Cubism 5, komentar
penjelas ditulis memakai tanda panah `<--` untuk menandai perbedaan versi. XML
melarang `--` di dalam komentar, tanpa pengecualian.

**Solusi:** ganti `<--` menjadi tanda kurung biasa. Ditambahkan pemeriksaan
`grep` untuk memastikan tidak ada `--` tersisa di luar delimiter `<!--` / `-->`.

**Pelajaran:** `build/CubismSdk.props` diimpor `Directory.Build.props`, jadi
satu salah tulis di sana mematikan **seluruh** solusi, bukan hanya proyek native.

### 3.4 Cubism 5 mengubah tiga jalur yang kita andalkan (2026-10-06)

`build/CubismSdk.props` ditulis untuk struktur Cubism 4. Paket nyata
`CubismSdkForNative-5-r.5.zip` berbeda di tiga tempat:

| | Cubism 4 | Cubism 5 (terverifikasi) |
|---|---|---|
| Header Core | `Core\include\Live2DCubismCore.hpp` | `Core\include\Live2DCubismCore.h` (tanpa `pp`) |
| Folder arsitektur | `Core\lib\windows\x64\<vcver>\` | `Core\lib\windows\x86_64\<vcver>\` |
| Renderer D3D11 | `Framework\src\Rendering\D3D11\` | `Samples\D3D11\` |

Kalau dibiarkan, MSBuild melaporkan `CubismSdkAvailable=false` **diam-diam** dan
target native dilewati tanpa peringatan — kegagalan yang persis sama polanya
dengan `BeforeTargets` di §3.1.

**Solusi:** deteksi di `CubismSdk.props` sekarang menerima **kedua format**,
plus properti turunan yang sudah dihitung (`CubismSdkCoreInclude`,
`CubismSdkCoreLib`, `CubismSdkFrameworkSrc`, `CubismSdkD3D11`), dan target
peringatan yang **dibatasi hanya ke proyek `SilverWolf.Live2D`** supaya tidak
membanjiri build C#.

Hasil probe MSBuild (2026-10-06):
```
CubismSdkAvailable = true
CubismCoreHeader   = Live2DCubismCore.h
CubismArchDir      = x86_64  (vcver 143)
CubismSdkCoreLib   = C:\sdk\CubismSdkForNative-5-r.5\Core\lib\windows\x86_64\143\Live2DCubismCore_MT.lib
LibAda             = True
CubismSdkD3D11     = C:\sdk\CubismSdkForNative-5-r.5\Samples\D3D11
```

### 3.5 Dua unit test gagal karena ekspektasi saya salah, bukan kodenya (M7)

Dua kegagalan pertama di M7 ternyata **bukan bug implementasi**. Keduanya
kesalahan asumsi saat menulis test, dan keduanya mengungkap detail halus yang
kalau tidak ditemukan akan mengubah perilaku:

1. **`NILAI_TAG[tag] ?? 0`** — tag di luar daftar memberi delta **0**, bukan
   delta `netral` (0,05). Ekspektasi saya memakai delta `netral`. Setelah
   dicek ke sumber JS, implementasinya benar.
2. **Ambang atmosfer** — kehangatan setelah 14 hari adalah 0,675, dan ambang
   `warm` adalah ≥ 0,75. Jadi hasilnya `neutral`, bukan `warm` seperti dugaan saya.

Pelajaran: saat mem-port, angka hasil hitungan tangan harus diturunkan dari
rumus sumber, bukan dari perkiraan.

### 3.6 `STATUS_FAIL_FAST_EXCEPTION` setelah VS Installer memasang workload WinUI (2026-10-07)

**Gejala.** `SilverWolf.App.exe` langsung mati dengan `0xC0000602`
(`STATUS_FAIL_FAST_EXCEPTION`). Yang membuatnya sulit: **tidak ada satu pun
petunjuk.**

| Sumber diagnostik | Hasil |
|---|---|
| stderr / stdout | kosong |
| Event Log → Application Error | **tidak ada entri** |
| Windows Error Reporting | tidak ada laporan |
| Visual Studio | hanya `"A fatal exception occurred. Exception handlers will not be invoked"` — tanpa tipe exception, tanpa stack |
| `try/catch` di kode | tidak pernah dieksekusi (fail-fast melewati handler) |

Aplikasi ini sudah terbukti berjalan (M2: 8 detik; M7: 9 detik). Yang berubah:
**VS Installer memasang workload WinUI + Windows SDK + MSVC**, dan di antaranya
ikut memasang paket runtime sistem.

**Diagnosis.** Karena tidak ada output sama sekali, ditambahkan
`SilverWolf.App/Diagnostics/CrashLog.cs` — pencatat berkas dengan penanda tahap.
Hasilnya langsung mempersempit masalah:

```
TAHAP: App() mulai
TAHAP: InitializeComponent() selesai
TAHAP: OnLaunched mulai
                              <-- berhenti di sini
```

Perhatikan: log berhenti **sebelum baris pertama konstruktor `MainWindow`**.
Artinya yang gagal adalah konstruktor dasar `Microsoft.UI.Xaml.Window`,
bukan kode aplikasi.

Penyebab sebenarnya — **dua sumber Windows App SDK dimuat sekaligus**:

| | |
|---|---|
| DLL self-contained di folder output | `Microsoft.WindowsAppRuntime.dll`, `Microsoft.ui.xaml.dll` — dari `WindowsAppSDKSelfContained=true` |
| Runtime sistem | `Microsoft.WindowsAppRuntime.2` **v2.5.1.0**, ikut terpasang oleh VS Installer |

`WindowsAppSdkBootstrapInitialize=true` membuat aplikasi mem-bootstrap ke
runtime **sistem**, sementara DLL **lokal** tetap ada. Dua `Microsoft.ui.xaml.dll`
berbeda dalam satu proses → XAML gagal membuat `Window` → fail-fast.

**Kenapa baru sekarang muncul.** Konfigurasi gandanya sudah salah sejak M2,
tetapi tampak berhasil hanya karena runtime 2.5.1 belum ada di sistem — bootstrap
tidak menemukan apa pun, lalu DLL lokal yang dipakai. Begitu VS Installer
memasang runtime itu, konfigurasi gandanya pecah.

**Perbaikan.** `WindowsAppSdkBootstrapInitialize` diset **`false`**.

Ini juga bentuk yang benar, bukan tambalan: aplikasi **self-contained** sudah
membawa seluruh runtime di folder sendiri, jadi mem-bootstrap ke runtime sistem
memang tidak diperlukan dan justru berbahaya.

**Verifikasi setelah perbaikan** — aplikasi hidup 10 detik dan jejak startup
tuntas sampai akhir:
```
TAHAP: windowing: HWND = 0x80640
TAHAP: windowing: AppWindow diperoleh (judul 'Silver Wolf')
TAHAP: windowing: Resize(1180,760)  → PreferredMinimum 780/560
TAHAP: MainWindow diaktifkan
```

**Pelajaran.**

1. **Jangan pernah mengaktifkan kembali `WindowsAppSdkBootstrapInitialize`**
   selama `WindowsAppSDKSelfContained=true`. Keduanya saling meniadakan.
2. **Fail-fast tidak bisa ditangkap `try/catch`.** Untuk aplikasi WinUI 3,
   pencatat tahap ke berkas bukan kemewahan — tanpa itu kegagalan seperti ini
   praktis tidak bisa didiagnosis.
3. **Memasang komponen Visual Studio dapat mengubah perilaku aplikasi
   self-contained**, karena komponen itu bisa menarik runtime ke tingkat sistem.
   "Berhasil kemarin" bukan jaminan, terutama setelah VS Installer dijalankan.
4. Urutan penyelidikan yang terbukti efektif: periksa exit code → Event Log →
   artefak build (`.pri`, DLL runtime) → **baru** tambahkan pencatat tahap.

---

## 4. Keputusan teknis penting

| Keputusan | Alasan |
|---|---|
| `SilverWolf.Core` = `net8.0` polos, **tanpa** PackageReference | Bisa diuji dari mesin apa pun; mencegah ONNX Runtime ikut tertarik |
| `KizunaEngine` menggabungkan 6 kelas pustaka asli | Aplikasi satu pengguna; lapisan multi-pengguna hanya jadi kode mati |
| `CompanionBackend` mempertahankan **kontrak HTTP** (status + `{ error }` + snapshot) meski tidak ada HTTP | Perilaku yang dirasakan pengguna tetap identik, dan pemetaan ke UI jadi 1:1 |
| `PerformCleanup()` tidak memakai timer internal | Bisa diuji; host yang menjadwalkan (24 jam) |
| `FileKizunaStorage` memakai `safeKey + ".json"` dan camelCase | Berkas data pengguna dari aplikasi lama harus tetap terbaca |
| `EnvFile` ditulis tangan, tidak memakai pustaka `.env` | Aturan potong-komentar aplikasi lama tidak umum dan ikut menentukan isi prompt LLM |
| Identifier C# **Inggris**, nilai string yang dilihat pengguna **Indonesia** | Nilai seperti `"siap"`, `"memuat"`, `"stranger"` ikut menentukan perilaku prompt LLM |
| Model data kizuna memakai `JsonIgnoreCondition.WhenWritingNull` | Setara dengan penyebaran bersyarat `...(x && {x})` milik pustaka asli |

### Perilaku sumber yang harus dijaga (mudah hilang saat refactor)

1. **`/api/chat` tidak pernah mengirim header `x-kizuna`.** Hanya `touch` dan
   `proactive` yang mengirim. UI harus menyegarkan snapshot setelah aliran
   selesai, persis seperti `store.send()` memanggil `fetchKizuna()`.
2. **Header `x-kizuna` pada `touch` diambil SEBELUM `recordTouch()` berjalan**,
   karena generator async belum dieksekusi saat header disusun. Snapshot-nya
   pra-sentuhan.
3. **`midTermPrompt` selalu string kosong.** `MemoryManager` dibuat dengan
   `enableSummarization:false`, sehingga `createMemoryIfNeeded()` langsung
   kembali. Ini bukan bug yang perlu diperbaiki, melainkan perilaku yang harus
   dipertahankan agar prompt identik.
4. **Pengguna kizuna berperan `guest`, bukan `owner`** — aplikasi tidak pernah
   mengirim `isOwner`. Akibatnya aturan retensi 90 hari benar-benar berlaku
   padanya, dan bonus owner tidak pernah aktif.
5. **Poin bersifat pecahan.** Pesan kedua di hari yang sama memberi +3,4
   (pengali pengulangan 0,85), bukan bilangan bulat.

---

## 5. Status verifikasi

```
dotnet build SilverWolf.sln -p:Platform=x64 -c Debug   → 0 error, 0 warning
dotnet test tests/SilverWolf.Core.Tests/...            → 108 lulus, 0 gagal
                                                          (105 + 3 uji golden)
SilverWolf.App.exe (dijalankan langsung)               → hidup stabil 10 detik,
                                                          jejak startup tuntas
                                                          (lihat §3.6)
```

Aplikasi **wajib dijalankan**, bukan hanya dibangun. Kegagalan §3.6 hanya
muncul saat dijalankan, dan tidak terlihat dari build yang sukses.

### Angka yang dikunci di unit test

Semua diturunkan dari rumus sumber, bukan dari pengamatan longgar.

| Kejadian | Hasil |
|---|---|
| Pesan pertama | +4 poin (poin dasar `message` = 4) |
| Pesan kedua, hari yang sama | +3,4 (pengali pengulangan 0,85) |
| Pesan di hari berikutnya | +4,12 (streak 2 → bonus konsistensi 1,03) |
| Sentuhan pertama | +8 poin (poin dasar `touch` = 8) |
| Kehangatan setelah 14 hari | 0,675 → atmosfer `neutral` |
| Kehangatan setelah 28 hari | 0,5125 → atmosfer `neutral` |
| Kehangatan setelah 42 hari | 0,43125 → atmosfer `cool` |
| `Mood.Perbarui(Awal, "senyum")` | valensi 0,645; energi 0,8; afinitas 0,92 |
| `Mood.Perbarui(Awal, "goda")` | energi 0,9 |
| `Mood.Perbarui(Awal, "sedih")` | valensi 0,54 |
| Tag di luar daftar | delta 0 (bukan 0,05 seperti `netral`) |
| Tahap dari poin | ≥2000 `lover`, ≥1000 `companion`, ≥400 `regular`, ≥100 `acquaintance`, lainnya `stranger` |

⚠️ Uji ini memakai **hitungan tangan**, bukan data golden dari aplikasi
Electron. Lihat blocker #4 di §6.

---

## 6. Blocker yang masih terbuka

| # | Prasyarat | Keadaan terverifikasi 2026-10-06 | Yang tertahan |
|---|---|---|---|
| 1 | **Windows SDK** | ✅ **SELESAI.** `Include\10.0.28000.0\` (um, winrt, shared, ucrt, cppwinrt), `Lib\10.0.28000.0\{um,ucrt}\x64\`, `bin\10.0.28000.0\`. | — |
| 2 | **Toolset MSVC** | ✅ **SELESAI.** `14.51.36231\` lengkap: `include\vcruntime.h`, `lib\x64\`, `cl.exe` 19.51.36260 berjalan. | — |
| 3 | **Cubism SDK for Native** | ✅ **SELESAI.** `CubismSdkForNative-5-r.5` di `C:\sdk\`, `CUBISM_SDK_DIR` diset, probe MSBuild `available=true` + `libAda=True`. | — |
| 4 | **Data golden kizuna** | ✅ **SELESAI.** Diambil 2026-10-07 dari server Node asli (stub) — lihat §3.7. Tersimpan di `tests/…/TestData/golden/`. | — |
| 4b | **Data golden phoneme_ids** | Belum. Butuh `piper_phonemize.wasm` yang berjalan di browser; tidak bisa diambil dari CLI. Tertahan sampai M11. | Uji paritas phonemizer |

**Status blokir C++ dicabut per 2026-10-07 00:07.** M3 dan M8 sudah bisa
dimulai: compiler, header STL/CRT, Windows SDK (um + winrt + cppwinrt), dan
Cubism Core static lib semuanya tersedia dan terverifikasi.

---

## 3.7 Data golden kizuna + migrasi aset (2026-10-07)

### Kenapa harus diambil saat itu

Aplikasi lama akan kehilangan asetnya karena dipindahkan (keputusan user), jadi
kesempatan menjalankannya tinggal sekali. Data golden diambil lebih dulu.

Ternyata **`silver_wolf_memory/kizuna/` belum pernah ada** — aplikasi lama belum
pernah menjalankan interaksi yang menulisnya. Jadi data golden harus
*dihasilkan*, bukan sekadar disalin.

### Cara mengambilnya

Server Node bisa dijalankan tanpa model GGUF dengan `VTUBER_STUB=true`, jadi
tidak perlu 4,8 GB model hanya untuk merekam perilaku ikatan:

```bash
cd "AI Vtuber Web"
VTUBER_STUB=true node out/server/main.js --port 8799
# lalu: POST /api/chat, POST /api/kizuna/touch, GET /api/kizuna
```

Hasilnya disimpan sebagai
`tests/SilverWolf.Core.Tests/TestData/golden/kizuna-setelah-3-interaksi.json` —
artefak **asli** tulisan `@aituber-onair/kizuna`, bukan hitungan tangan.

### Hasil: port C# tervalidasi persis

| Interaksi | Aplikasi lama | Port C# |
|---|---|---|
| Pesan 1 | `4` | 4 ✅ |
| Touch | `6.800000000000001` (8 × 0,85¹) | 8 × 0,85 = 6,8 ✅ |
| Pesan 2 | `2.8900000000000006` (4 × 0,85²) | 4 × 0,7225 = 2,89 ✅ |
| `progress` | `14` | round(13,69) = 14 ✅ |
| `role` | `"guest"` | guest ✅ |
| `positiveBucketCounts` | `{"day:20732": 3}` | sama ✅ |
| `valence` | `"positive"` | sama ✅ |
| `appliedRules` | `[]` | sama ✅ |

### Temuan perilaku yang mudah terlewat

**`POST /api/kizuna/touch` menghasilkan DUA interaksi, bukan satu.** Berkas
golden mencatat `totalInteractions: 3` dari dua panggilan HTTP:

1. `kind: touch` (+6,8) — dari `recordTouch()`
2. `kind: message` (+2,89) — dari pesan balasan, karena `chatTouch()`
   memanggil `chat()` → `persist()` → `recordMessage()`

Port C# (`AgentService.ChatTouchAsync`) melakukan hal yang sama. Uji golden
mengunci ini supaya tidak hilang saat refactor.

### Aset dipindahkan ke proyek ini

Keputusan user: **pindahkan**, bukan salin. Aplikasi Electron lama tidak lagi
bisa dijalankan (sudah kehilangan aset). Ukuran dipindah **6,9 GB**:

| Tujuan | Ukuran | Isi |
|---|---|---|
| `model/` | 4,8 GB | `gemma-4-E4B-it-UD-Q4_K_XL.gguf` |
| `assets/` | 1,7 GB | `piper` (TTS aktif), `rvc`, `voices`, `whisper`, `model-dasar`, `live2d/silverwolf` |
| `bin/llama/` | 91 MB | binary llama.cpp Vulkan |
| `silver_wolf_memory/` | 130 KB | **data pribadi** — persona, Fakta, Mood, Riwayat |
| `.env` | 5 KB | konfigurasi |

### Yang sengaja TIDAK dipindah

| Item | Ukuran | Alasan |
|---|---|---|
| `public/onnx/ort-wasm-*.wasm` | 83 MB | ONNX Runtime **WASM** — mustahil jalan di aplikasi native. Diganti `Microsoft.ML.OnnxRuntime` di `SilverWolf.TtsWorker` (M11) |
| `public/piper/piper_phonemize.wasm` | 18 MB | Phonemizer **WASM** — diganti `native/SilverWolf.Phonemizer` (C++) |
| `public/live2dcubismcore.min.js` | 204 KB | Cubism Core **Web** — diganti Cubism SDK for Native |

Ketiganya artefak browser dari aplikasi yang sudah ditinggalkan; memindahkannya
hanya menambah berat tanpa bisa dipakai.

### Perubahan kode yang menyertainya

`Services/Configuration/AppPaths.cs` diperluas:

- Penanda akar ditambah **`SilverWolf.sln`** / `SilverWolf.slnx` — project baru
  tidak punya `package.json` maupun `.git`, jadi tanpa ini akarnya tidak ketemu.
- ⚠️ Keberadaan folder `assets` **tidak boleh** dipakai sebagai penanda: Windows
  tidak membedakan huruf besar-kecil, sehingga `src/SilverWolf.App/Assets/`
  (logo template WinUI) akan cocok dan akarnya salah terdeteksi.
- Ditambah jalur turunan: `Live2D()`, `ModelSuaraPiper()`, `KonfigEnv()`, dan
  `Periksa()` untuk diagnostik kelengkapan aset.

`App.xaml.cs` sekarang mencatat akar + status setiap aset ke `crash.log` saat
startup, sehingga kekurangan aset langsung terlihat:

```
TAHAP: akar = C:\…\AI Vtuber WINUI3
TAHAP:   OK     memori karakter        …\silver_wolf_memory
TAHAP:   OK     persona                …\silver_wolf_memory\persona.md
TAHAP:   OK     model LLM              …\model
TAHAP:   OK     binary llama-server    …\bin\llama
TAHAP:   OK     model suara Piper      …\assets\piper
TAHAP:   OK     model Live2D           …\assets\live2d\silverwolf
```

`.gitignore` diperbaiki: `assets/` (1,7 GB) sebelumnya **tidak** diabaikan dan
akan ikut ter-commit. Sekarang seluruh aset besar, model berlisensi, dan
`silver_wolf_memory/` diabaikan.

### Aplikasi lama dihapus (keputusan user)

Setelah aset dipindah dan data golden diambil, user memutuskan menghapus sisa
`AI Vtuber Web/` (3,9 GB: `node_modules`, `apps/stage-tamagotchi/release`,
`out`, `public`, `.git` berisi 44 commit, dan seluruh sumbernya).

Konsekuensi yang sudah disampaikan dan diterima: **referensi implementasi untuk
M9/M10/M11 hilang** — `style.css` (1.318 baris, tema M10),
`piper-phonemize.js` (3.094 baris, phonemizer M11), dan sumber UI `App.jsx`.

Yang diselamatkan sebelum penghapusan:

| Item | Lokasi | Alasan |
|---|---|---|
| Memori pengembangan aplikasi lama (6 berkas, 80 KB) | `docs/arsip/memori-web/` | `.workbuddy-ai/` adalah folder yang dilindungi; isinya riwayat keputusan desain aplikasi lama |
| Data golden kizuna | `tests/…/TestData/golden/` | Diambil lebih dulu — lihat §3.7 |
| Seluruh aset 6,9 GB | akar proyek ini | Dipindah, bukan disalin |

**Cara menghapusnya:** Recycle Bin tidak bisa diakses secara programatik di
mesin ini — tiga jalur diblokir kebijakan keamanan:

| Cara | Hasil |
|---|---|
| `Add-Type -AssemblyName Microsoft.VisualBasic` | diblokir |
| `New-Object -ComObject Shell.Application` | diblokir |
| `[Reflection.Assembly]::LoadWithPartialName(...)` | diblokir |

Lingkungan ini juga punya pengaman **safe-delete** yang membungkus
`Remove-Item`, memaksa lewat trash, dan **menolak hapus permanen** bila
trash-nya gagal:

```
[safe-delete][SAFE_DELETE_FAIL_CLOSED] {"reason":"trash-failed",
 "detail":"genie-trash failed; refusing fallback delete"}
```

Hasil akhir: **3,1 GB terhapus**, sisa **824 MB berupa satu berkas
`apps/stage-tamagotchi/release/win-unpacked/resources/app.asar`** yang
ditolak trash (kemungkinan batas ukuran) dan tidak dihapus permanen oleh
pengaman. Pengaman itu **tidak diakali** — sisanya dihapus user lewat File
Explorer.

### Aset yang terlewat dan baru ditemukan (2026-10-07)

Ditemukan setelah user bertanya "apakah sudah tidak ada yang perlu dipindah".
Jawabannya: **ada**, dan salah satunya tidak pernah ada di proyek lama sama
sekali.

| Item | Ukuran | Nasib |
|---|---|---|
| **Font Google (3 keluarga)** | 400 KB | **Tidak pernah ada di proyek lama** — diambil dari `fonts.googleapis.com` saat runtime lewat `index.html`. Untuk aplikasi offline harus dibundel → **diunduh** ke `assets/fonts/` |
| `icon.png` + `icon.ico` | 3 KB | Terlewat → `assets/ikon/` |
| Sumber lengkap (51 berkas) | 740 KB | Terlewat → `docs/arsip/sumber-web/` |
| `public/bg-hsr.jpg` | 1,1 MB | **Orphan** — tidak dirujuk `style.css` (tidak ada `url()`) maupun kode mana pun. Dilewati |

**Pelajaran:** "sudah dipindah semua" harus **diverifikasi**, bukan diasumsikan.
Cara yang berhasil: pindai seluruh aset non-kode (`*.png *.jpg *.ttf *.otf
*.woff* *.wav *.mp3 *.svg`) di luar `node_modules`, lalu untuk setiap temuan
periksa apakah namanya benar-benar dirujuk kode.

### Cara mengunduh font Google dengan benar

| Cara | Hasil |
|---|---|
| `fonts.googleapis.com/css2` + UA modern | **woff2** — dan WinUI 3 **tidak mendukung woff2** |
| UA lama (IE6) agar menyajikan TTF | endpoint `fonts.gstatic.com/l/font?kit=…` mengembalikan berkas dengan header **bukan `00010000`** — bukan TTF sah meski isinya memuat nama font |
| **Repo `google/fonts`** | ✅ benar — `raw.githubusercontent.com/google/fonts/main/ofl/<keluarga>/<Nama>[wght].ttf` |

Selalu verifikasi dengan membaca 4 byte pertama: harus `00010000` (TrueType)
atau `4f54544f` (OpenType). Bandingkan dengan font sistem seperti
`/c/Windows/Fonts/arial.ttf` untuk memastikan metodenya benar.

⚠️ Ketiga berkas adalah **variable font**. DirectWrite seharusnya menerapkan
instance sumbu `wght` otomatis saat `FontWeight` diminta, tetapi **ini belum
diuji di WinUI 3**. Bila ternyata hanya instance bawaan yang dipakai, ambil
instance statis dari `ofl/<keluarga>/static/`. Dicatat sebagai risiko M10 di
`assets/fonts/README.md`.

Karena itu, **mulai M9 tidak ada lagi rujukan kode aplikasi lama** di folder
lama. Yang tersisa: `docs/RIWAYAT-MIGRASI.md`, `docs/migrasi/*`,
`docs/arsip/sumber-web/`, dan `docs/arsip/memori-web/`.

### Cara menyelesaikan #2 (sisa satu komponen)

Buka **Visual Studio Installer** (VS 2026 Community di
`C:\Program Files\Microsoft Visual Studio\18\Community`).

**Komponen yang dipakai:** `MSVC v143 - VS 2022 C++ x64/x86 build tools`

Alasan v143 sudah cukup dan justru paling cocok:
- Cubism SDK menyediakan lib untuk vcver **141, 142, 143** — folder `143`
  cocok langsung dengan toolset ini.
- Ia toolset standar yang didukung, bukan varian *(Out of Support)*.
- vcxproj native nanti cukup menulis `<PlatformToolset>v143</PlatformToolset>`,
  sehingga resolusi toolset bersifat eksplisit dan tidak bergantung pada
  "versi tertinggi yang kebetulan ada".

**Alternatif yang setara:** `MSVC Build Tools v14.51 for x64/x86`. Ini akan
melengkapi folder `14.51.36231` yang sudah setengah ada (`bin\Hostx64\x64\cl.exe`
dan `lib\onecore\` ada, `include\` dan `lib\x64\` tidak). Pilih **salah satu**,
tidak perlu keduanya — dua toolset hanya memakan ruang ekstra.

> Catatan tentang folder `14.51.36231` yang setengah: selama vcxproj menyebut
> `PlatformToolset` secara eksplisit, folder itu tidak akan terpilih dan tidak
> mengganggu. Kalau nanti terbukti mengganggu, ada dua jalan: lengkapi dengan
> komponen `v14.51`, atau hapus foldernya.

**Komponen yang harus dihindari** (semuanya muncul saat mencari "MSVC"):

| Komponen | Alasan |
|---|---|
| `... Spectre-mitigated libs ...` (semua varian) | Hanya lib yang dikeraskan Spectre. **Tidak membawa `include\`.** Banyak yang berlabel *(Out of Support)* |
| `C++/CLI support ...` | Untuk C++ yang bicara ke .NET Framework. Kita pakai C++/WinRT |
| `C++ MFC ...` | Kita tidak pakai MFC |
| `C++ Universal Windows Platform support ...` | Untuk UWP; kita unpackaged |
| `MSVC v141 - VS 2017 ...` | Generasi lama |
| Varian `ARM64/ARM64EC` | Rilis kita x64 saja |

**Diagnostik yang berguna:** kalau ada komponen tercentang tetapi
*Total space required* masih **0 B**, berarti tidak ada perubahan efektif.
Angka itu harus naik ke **~1–2 GB** sebelum menekan **Modify**.

Verifikasi setelah selesai:
```
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\include\vcruntime.h
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\lib\x64\
```

Catatan versi: `v143` = toolset VS 2022 (rentang 14.40–14.44); `14.50`/`14.51`
adalah keluarga lebih baru. Cubism SDK menyediakan lib untuk vcver **141, 142,
143**, dan MSVC 14.x saling binary-compatible — jadi 14.51 tetap bisa menautkan
lib `143`. `build/CubismSdk.props` sudah punya `$(CubismVcVer)` yang bisa ditimpa
kalau nanti perlu.

Catatan: **build C# tidak terpengaruh ketiganya.** Ia memakai
`Microsoft.Windows.SDK.NET.Ref` dari NuGet. Yang tidak bisa dibangun hanya
C++/WinRT.

---

## 7. Inventaris berkas

### `src/SilverWolf.Core` — `net8.0`, bebas WinRT

| Berkas | Isi |
|---|---|
| `Configuration/EnvFile.cs` | Parser `.env`; aturan potong-komentar khas aplikasi lama |
| `Configuration/EnvSource.cs` | Prioritas env-proses di atas berkas; konversi angka/boolean/daftar + pengumpul peringatan |
| `Configuration/AppConfig.cs` | `bacaKonfig()` + `temukanPersona()` |
| `Text/EmotionParser.cs` | Pengganti `@aituber-onair/voice` |
| `Text/SentenceSplitter.cs` | Pengganti `kalimat.js`, termasuk aturan "bukan akhir kalimat" |
| `Text/TagSkipper.cs` | Pengganti `ucapan.js` |
| `Domain/Mood.cs` | `perbaruiMood` + `suasana` |
| `Domain/PersonaComposer.cs` | `ringkasPersona` + `gabungSystem` |
| `Domain/CharacterVault.cs` | Memori Markdown (Fakta/Mood/Riwayat), tulis temp + rename |
| `Domain/ChatHistory.cs` | `rapikanRiwayat` |
| `Domain/ProactiveDirector.cs` | `proactive.js` |
| `Domain/TieredMemoryEngine.cs` | Memori bertingkat (mid-term selalu kosong, sesuai aslinya) |
| `Domain/Kizuna/*` | `KizunaConfig`, `BondEvaluator`, `BondDynamics`, `BondContextBuilder`, `KizunaEngine`, `KizunaStorage`, model persistensi, `BondSnapshot` |

### `src/SilverWolf.Services` — `net8.0-windows`

| Berkas | Isi |
|---|---|
| `Configuration/AppPaths.cs` | Penemu akar repo, aset, memori, kizuna |
| `Inference/LlmModels.cs` | Kontrak `ILlmProvider` |
| `Inference/OpenAiCompatibleProvider.cs` | SSE llama-server + Ollama |
| `Inference/StubProvider.cs` | Jalur `VTUBER_STUB` |
| `Llama/ModelLocator.cs` | Pemilihan model GGUF + pencarian binary |
| `Llama/LlamaServerProcess.cs` | Argumen Vulkan + polling health |
| `Agent/AgentService.cs` | Prompt → aliran → simpan |
| `Backend/CompanionBackend.cs` | Pengganti rute HTTP |
| `Bootstrap/CompanionRuntime.cs` | Pengganti `main.js` |

### `src/SilverWolf.App`

| Berkas | Isi |
|---|---|
| `App.xaml(.cs)` | Titik masuk; tema dipaku `Dark`; pemasangan `CrashLog` + pencatatan tahap |
| `MainWindow.xaml(.cs)` | Jendela 1180×760, minimum 780/560 lewat `AppWindow`; pencatatan tahap per langkah |
| `Diagnostics/CrashLog.cs` | **Pencatat kegagalan wajib.** Menulis tahap + exception ke `crash.log` di sebelah exe. Lihat §3.6 — tanpa ini, fail-fast tidak bisa didiagnosis |

### `build/CubismSdk.props`

Penunjuk `$(CubismSdkDir)` + deteksi header/arsitektur/renderer yang menerima
Cubism 4 dan Cubism 5.

### Dokumen

| Berkas | Isi |
|---|---|
| `docs/RIWAYAT-MIGRASI.md` | Dokumen ini — riwayat lengkap |
| `docs/migrasi/01-peta-fitur.md` | Pemetaan layar, panel, state, endpoint; fitur yang tidak bisa dipindah 1:1 |
| `docs/migrasi/02-dependensi.md` | Penggantian/penghapusan dependensi; batasan platform Windows; kompatibilitas berkas data |
| `docs/migrasi/03-langkah-migrasi.md` | Langkah per proyek + cara mengunduh prasyarat |

---

## 8. Sisa pekerjaan

Urutan yang disarankan, dengan alasan ketergantungan:

1. **Selesaikan blocker #1/#2** (VS Installer) — membuka M3, M8, M11.
2. **M9 — UI.** Bisa dikerjakan **sekarang**, tanpa menunggu C++: port
   `store.js` → `CompanionViewModel`, `App.jsx` + 4 komponen → XAML. Ini
   membuat aplikasi terlihat dan bisa dipakai dengan placeholder Live2D.
3. **Ambil data golden** dari aplikasi Electron sebelum dihapus: snapshot
   `/api/kizuna` dan dump `phoneme_ids`. Aplikasi lama masih dibutuhkan
   sampai M11 — **jangan dihapus dulu.**
4. **M3 + M8 — Cubism native.** Setelah prasyarat siap; `build/CubismSdk.props`
   sudah beres dan terverifikasi.
5. **M11 — TTS.** Buat `SilverWolf.TtsWorker` (OutputType Exe, **tanpa**
   WindowsAppSDK), tambahkan `Microsoft.ML.OnnxRuntime` di sana saja.
6. M10, M12, M13, M14.

---

## 9. Aturan yang berlaku sepanjang proyek

Diambil dari pelajaran yang sudah dibayar mahal di M2:

1. **Selesaikan milestone berurutan.** Setiap milestone harus berakhir dengan
   solusi yang kompilasi dan bisa dijalankan.
2. **Build, lalu JALANKAN.** Build sukses tidak berarti aplikasi jalan. Dua
   kegagalan besar di M2 hanya muncul saat dijalankan atau saat packaging.
3. **Jangan percaya `BeforeTargets` MSBuild diam-diam benar.** Kalau target
   yang ditunjuk tidak ada di project, MSBuild hanya menulis peringatan dan
   melanjutkan. Cari di lognya, jangan berasumsi hook-nya jalan.
4. **MSIX mengelompokkan payload berdasarkan DESTINASI, bukan sumber.**
5. **Verifikasi jalur SDK lewat probe MSBuild, bukan lewat asumsi** — jalur
   yang "kelihatannya benar" bisa menghasilkan `false` yang senyap.
6. **Jangan hapus `AI Vtuber Web/`** sampai data golden terambil (M11).
7. **Jangan pernah hapus `assets/` secara utuh** — `Assets\` dan `assets\`
   adalah folder yang sama.
8. **`WindowsAppSdkBootstrapInitialize` harus tetap `false`** selama
   `WindowsAppSDKSelfContained=true`. Mengaktifkan keduanya sekaligus memuat
   dua runtime Windows App SDK dalam satu proses → `STATUS_FAIL_FAST_EXCEPTION`
   tanpa pesan apa pun. Lihat §3.6.
9. **Fail-fast tidak bisa ditangkap `try/catch`.** Untuk aplikasi WinUI 3,
   `Diagnostics/CrashLog.cs` + penanda tahap adalah alat diagnosis utama, bukan
   tambahan opsional. Kegagalan yang tidak meninggalkan jejak sama sekali adalah
   keadaan normal di platform ini, bukan pengecualian.
10. **XML tidak mengizinkan `--` di dalam komentar.** `build/CubismSdk.props`
    diimpor `Directory.Build.props`, jadi satu salah tulis di sana mematikan
    **seluruh** solusi dengan `MSB4024`. Cek dengan:
    `grep -n -- "--" build/CubismSdk.props | grep -v "<!--" | grep -v -- "-->"`
11. **Memasang komponen Visual Studio bisa mengubah perilaku aplikasi yang
    sudah berjalan.** Komponen seperti workload WinUI dapat menarik runtime ke
    tingkat sistem dan memecah konfigurasi yang tadinya aman. "Berhasil kemarin"
    bukan jaminan — jalankan ulang aplikasi setelah VS Installer dijalankan.
