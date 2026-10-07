# Prompt untuk melanjutkan proyek — Silver Wolf (WinUI 3)

Dokumen ini berisi **prompt siap tempel** untuk sesi AI berikutnya, plus
ringkasan konteks kalau AI-nya tidak punya akses berkas.

Terakhir diperbarui: **2026-10-07 01:45**

---

## Cara pakai

1. Buka sesi AI baru di folder `C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\`
2. Salin **seluruh blok di bawah** dan tempel sebagai pesan pertama
3. Kalau AI-nya punya akses berkas, ia akan membaca dokumen rujukan sendiri

---

## ▼ SALIN DARI SINI ▼

```
Lanjutkan pengerjaan migrasi project "AI Vtuber Web" (Electron + Vue 3 + server
Node) ke aplikasi desktop native WinUI 3 / Windows App SDK.

LOKASI
  Target  : C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\
  Sumber  : sudah DIHAPUS (aset & dokumentasi sudah dipindah ke target)

BACA DULU (urutan prioritas)
  1. docs/RIWAYAT-MIGRASI.md          <- dokumen utama, baca seluruhnya
  2. docs/migrasi/01-peta-fitur.md    <- pemetaan layar/panel/state/endpoint
  3. docs/migrasi/02-dependensi.md    <- penggantian dependensi + batasan Windows
  4. docs/migrasi/03-langkah-migrasi.md <- langkah per project + blocker
  5. assets/fonts/README.md           <- aset font
  6. docs/arsip/sumber-web/           <- KODE SUMBER APLIKASI LAMA (referensi)

STATUS SEKARANG (2026-10-07)
  Selesai:
    M0 toolchain          net8.0-windows10.0.19041.0 + WindowsAppSDK 2.5.1
    M1 bersihkan template
    M2 skeleton solusi    SilverWolf.sln (App/Core/Services/Core.Tests)
    M3 prasyarat C++      Windows SDK 10.0.28000 + MSVC 14.51.36231 + Cubism SDK 5 R5
    M4 peta fitur + Core  config, teks, domain
    M5 KizunaEngine, CharacterVault, TieredMemoryEngine
    M6 OpenAiCompatibleProvider, LlamaServerProcess
    M7 AgentService, CompanionBackend, CompanionRuntime, 108 unit test
    Aset 6,9 GB dipindah ke sini (model GGUF, suara, Live2D, vault, font, ikon)
    Data golden kizuna diambil dari aplikasi lama -> tests/.../TestData/golden/

  Belum:
    M8  komponen Cubism C++/WinRT (render, fit, fokus, motion)   <- SIAP DIMULAI
    M9  MainWindow, CompanionViewModel, 4 view, AssetLocator     <- SIAP DIMULAI
    M10 tema, blur, animasi, font
    M11 TTS: phonemizer, Piper, NAudio, lip-sync
    M12 tray, hotkey, single-instance, close-to-tray
    M13 integrasi end-to-end
    M14 rilis

  Blocker tersisa: HANYA satu —
    data golden phoneme_ids belum diambil (butuh piper_phonemize.wasm jalan di
    browser, tidak bisa dari CLI). Tertahan sampai M11.

VERIFIKASI (jalankan dulu sebelum menulis kode)
  dotnet build SilverWolf.sln -p:Platform=x64 -c Debug    -> harus 0 error, 0 warning
  dotnet test tests/SilverWolf.Core.Tests/...             -> harus 108 lulus
  lalu JALANKAN:
  src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/SilverWolf.App.exe

ATURAN YANG TIDAK BOLEH DILANGGAR (semuanya sudah pernah memakan waktu)
  1. JANGAN set WindowsAppSdkBootstrapInitialize=true selama
     WindowsAppSDKSelfContained=true. Dua runtime WASDK dalam satu proses ->
     STATUS_FAIL_FAST_EXCEPTION tanpa pesan apa pun. Nilainya HARUS false.
  2. BUILD, LALU JALANKAN. WinUI 3 bisa gagal fail-fast TANPA jejak: stderr
     kosong, Event Log kosong, WER kosong, try/catch tidak menangkap.
     Diagnostics/CrashLog.cs + penanda tahap adalah alat diagnosis utama.
  3. JANGAN hapus CrashLog dan penanda tahap di App.xaml.cs / MainWindow.xaml.cs.
  4. Penanda akar repo adalah SilverWolf.sln (AppPaths). JANGAN pakai keberadaan
     folder "assets" sebagai penanda — Windows case-insensitive sehingga
     src/SilverWolf.App/Assets/ akan cocok dan akar salah terdeteksi.
  5. XML tidak mengizinkan "--" di dalam komentar. build/CubismSdk.props diimpor
     Directory.Build.props, jadi satu salah tulis mematikan SELURUH solusi.
  6. PublishTrimmed harus tetap False. Trimming + XAML = kerusakan dikenal.
  7. Rilis x64 saja. llama.cpp Vulkan hanya x64.
  8. Identifier C# pakai bahasa Inggris, TETAPI nilai string yang dilihat
     pengguna tetap Indonesia ("siap", "memuat", "tidak-jalan", "stranger").
     Nilai-nilai itu ikut menentukan perilaku prompt LLM — jangan diterjemahkan.
  9. JANGAN commit aset berlisensi/besar. Lihat .gitignore. Model Live2D tidak
     boleh didistribusikan ulang sebagai berkas lepas (lisensi Live2D).
 10. Untuk memeriksa properti MSBuild, buat csproj probe sementara dengan
     <Target BeforeTargets="Build"><Message Importance="high" .../></Target>.
     "dotnet msbuild" DIBLOKIR kebijakan keamanan.

PERILAKU SUMBER YANG HARUS DIPERTAHANKAN (mudah hilang saat refactor)
  - /api/chat TIDAK mengirim header x-kizuna. Hanya touch dan proactive.
    UI harus menyegarkan snapshot setelah aliran selesai.
  - x-kizuna pada touch diambil SEBELUM recordTouch() berjalan (pra-sentuhan).
  - POST /api/kizuna/touch menghasilkan DUA interaksi: touch (+6,8) lalu pesan
    balasan (+2,89). Jangan "perbaiki" jadi satu.
  - midTermPrompt SELALU string kosong (enableSummarization:false). Bukan bug.
  - Pengguna kizuna berperan "guest", bukan owner. Retensi 90 hari berlaku.
  - Poin bersifat pecahan: pesan ke-2 di hari sama = +3,4 (pengali 0,85).

ANGKA GOLDEN YANG SUDAH DIVALIDASI terhadap aplikasi lama
  pesan 1 = 4 | touch = 6,8 | pesan 2 = 2,89 | progress = 14 | role = guest

  Font: aplikasi lama mengambil dari fonts.googleapis.com saat runtime. Untuk
  aplikasi offline, 3 variable TTF sudah dibundel di assets/fonts/.
  RISIKO BELUM DIUJI: apakah WinUI 3 menerapkan instance sumbu wght otomatis
  saat FontWeight diminta. Uji di M10; kalau gagal, ambil instance statis dari
  ofl/<keluarga>/static/ di repo google/fonts.

SARAN URUTAN KERJA
  M9 lebih dulu (murni C#/XAML, tidak butuh C++), supaya aplikasi terlihat dan
  bisa dipakai dengan placeholder Live2D. Baru M3+M8 (Cubism native).
  Setiap milestone harus berakhir dengan solusi yang KOMPILASI DAN BISA DIJALANKAN.

Jangan tanya ulang keputusan yang sudah dikunci (Live2D native C++/WinRT tanpa
WebView2, backend port penuh ke C#, TTS di exe worker terpisah, unpackaged +
self-contained). Semuanya beserta alasannya ada di docs/RIWAYAT-MIGRASI.md.
```

## ▲ SALIN SAMPAI SINI ▲

---

## Kalau AI-nya tidak punya akses berkas

Tempel blok di bawah ini **sebagai tambahan** setelah prompt di atas. Isinya
konteks minimum supaya ia tetap bisa membantu.

```
RINGKASAN KONTEKS (kalau kamu tidak bisa membaca berkas)

Proyek: Silver Wolf — pendamping AI/VTuber lokal (Live2D, LLM lokal, TTS,
sistem ikatan hubungan). Semula aplikasi Electron + Vue 3 + server Node,
sedang dipindah ke WinUI 3 (C#/.NET 8, XAML, MVVM).

Struktur solusi:
  SilverWolf.sln
  src/SilverWolf.App        WinUI 3 exe, unpackaged + WASDK self-contained
  src/SilverWolf.Core       net8.0 polos, bebas WinRT, bisa diuji di mana saja
  src/SilverWolf.Services   net8.0-windows: llama, inference, agent, backend
  tests/SilverWolf.Core.Tests  xunit, 108 uji
  native/SilverWolf.Live2D  C++/WinRT (belum dibuat)

Keputusan yang sudah dikunci:
  - Live2D: native C++/WinRT + Cubism Native, render D3D11 ke SwapChainPanel,
    TANPA WebView2
  - Backend: port penuh ke C#, tanpa sidecar Node saat runtime
  - TTS/ONNX Runtime: dipisah ke exe worker sendiri (SilverWolf.TtsWorker)
    karena onnxruntime.dll bentrok dengan Microsoft.Windows.AI.MachineLearning
    bawaan WindowsAppSDK (error APPX1101)
  - Distribusi: unpackaged + WASDK self-contained (aset ~7 GB tidak mungkin
    masuk MSIX, dan folder MSIX read-only padahal llama-server butuh folder tulis)

Tiga paket npm tanpa padanan .NET sudah direimplementasi di C#:
  @aituber-onair/kizuna   -> Core/Domain/Kizuna/*  (paling kompleks)
  @aituber-onair/voice    -> Core/Text/EmotionParser.cs
  @aituber-onair/core     -> Core/Domain/TieredMemoryEngine.cs

Yang belum: M8 (Cubism native), M9 (UI), M10 (tema), M11 (TTS), M12 (tray),
M13 (integrasi), M14 (rilis).
```

---

## Berkas rujukan, urut kepentingan

| Berkas | Isi |
|---|---|
| `docs/RIWAYAT-MIGRASI.md` | **Dokumen utama.** Seluruh riwayat: keputusan, milestone, 7 masalah nyata + penyelesaiannya, status verifikasi, blocker, inventaris, aturan proyek |
| `docs/migrasi/01-peta-fitur.md` | Layar, panel, state, endpoint → padanan C#; fitur yang tidak bisa dipindah 1:1 |
| `docs/migrasi/02-dependensi.md` | Tabel penggantian dependensi, alasan `APPX1101`, batasan platform Windows |
| `docs/migrasi/03-langkah-migrasi.md` | Langkah per project + cara mengunduh prasyarat |
| `assets/fonts/README.md` | Kenapa font dibundel, cara mengunduh yang benar, risiko variable font |
| `docs/arsip/sumber-web/` | Kode sumber aplikasi lama (referensi M9–M11) |
| `docs/arsip/memori-web/` | Catatan pengembangan aplikasi lama |
| `README.md` | Ringkasan proyek, struktur, tata letak aset |

## Kalau sesi AI-nya perlu mengecek keadaan mesin

```bash
cd "C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3"

# build + uji
dotnet build SilverWolf.sln -p:Platform=x64 -c Debug -v minimal
dotnet test tests/SilverWolf.Core.Tests/SilverWolf.Core.Tests.csproj

# jalankan (WAJIB — fail-fast tidak terlihat dari build)
cd src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64
./SilverWolf.App.exe & sleep 9 && cat crash.log
```

`crash.log` akan menampilkan akar yang ditemukan dan status 8 aset. Kalau ada
yang `HILANG`, asetnya belum lengkap di mesin itu.

---

## Yang dikerjakan berikutnya: M9 — antarmuka pengguna

M9 adalah satu-satunya milestone besar yang bisa diselesaikan **tanpa menunggu
komponen C++**. Ia murni C# + XAML dan langsung memakai `CompanionRuntime` yang
sudah selesai di M7.

### Kenapa M9 didahulukan, bukan M8

| | M8 (Cubism native) | M9 (UI) |
|---|---|---|
| Butuh komponen C++ | Ya — vcxproj C++/WinRT, taut Cubism Core, renderer D3D11 | Tidak |
| Butuh aset berlisensi | Ya — Cubism SDK (sudah ada di `C:\sdk\`) | Tidak |
| Hasil yang terlihat | Tidak ada sampai ada `SwapChainPanel` yang menampungnya | Langsung: chat jalan, HUD ikatan jalan |
| Risiko utama | Kegagalan senyap di level native | Mengubah `MainWindow` yang kini jadi alat diagnosis |

Karena itu M9 dikerjakan lebih dulu. `StageView` **tetap disiapkan** — dengan
`SwapChainPanel` kosong — supaya M8 tinggal mengisinya tanpa menyusun ulang UI.

⚠️ **Satu risiko yang harus diantisipasi sejak awal.** `MainWindow.xaml(.cs)`
dan `App.xaml.cs` saat ini memuat penanda tahap yang menyelamatkan diagnosis
§3.6. M9 akan menyusun ulang berkas itu. **Penanda tahapnya harus ikut pindah,
bukan dibuang.** Kalau `crash.log` berhenti mencatat setelah M9, kegagalan
berikutnya akan kembali tanpa jejak.

### Peta berkas M9

| Berkas yang dibuat | Port dari | Isi |
|---|---|---|
| `src/SilverWolf.App/ViewModels/CompanionViewModel.cs` | `store.js` (satu store Pinia) | Satu sumber keadaan untuk seluruh UI |
| `src/SilverWolf.App/Views/StageView.xaml(.cs)` | `komponen/Panggung.jsx` | Panel kiri; `SwapChainPanel` untuk M8, placeholder dulu |
| `src/SilverWolf.App/Views/ConsoleView.xaml(.cs)` | `komponen/Konsol.jsx` | Panel kanan: header, kartu HUD ikatan, telemetri |
| `src/SilverWolf.App/Views/MessageListView.xaml(.cs)` | `DaftarPesan` | Riwayat percakapan |
| `src/SilverWolf.App/Views/ComposerView.xaml(.cs)` | `Komposer` | Kotak input, tombol kirim, toggle suara |
| `src/SilverWolf.App/Configuration/AssetLocator.cs` | — | Pembungkus tipis di atas `AppPaths` untuk keperluan UI (font, ikon, model Live2D) |

Dua catatan pada tabel ini:

1. Nama berkas mengikuti aturan identifier **Inggris** (aturan #8 di blok
   tempel). Padanan Indonesianya tetap dicantumkan supaya tidak tercampur
   dengan komponen lama.
2. `AssetLocator` **jangan** mencari akar sendiri. `AppPaths.TentukanAkar()`
   sudah memuat penanda `SilverWolf.sln` yang diperbaiki dengan susah payah di
   §3.7 — duplikasi logika hanya membuka kembali bug "akar salah terdeteksi".

### Keadaan `CompanionViewModel`

Diturunkan dari `docs/migrasi/01-peta-fitur.md` §3, dengan tipe yang sudah ada
di `SilverWolf.Core` / `SilverWolf.Services`:

| Keadaan lama | Padanan | Catatan |
|---|---|---|
| `messages[]` | `ObservableCollection<ChatBubble>` | `{ Id, Role, Content, Pending, Error }` |
| `health` | `HealthSnapshot?` | `StatusText` = `"siap"` / `"memuat"` / `"tidak-jalan"` |
| `healthError` | `string? HealthError` | |
| `sending` | `bool IsSending` | |
| `expression` | `string Expression` | nilai tag **Indonesia apa adanya** (`"senyum"`, `"goda"`) |
| `autonomousMode` | `bool AutonomousMode` | default `true` |
| `lastUserActivity` | `DateTimeOffset LastUserActivity` | dasar hitung hening proaktif |
| `kizuna` | `BondSnapshot?` | `Stage`, `StageLabel`, `StageName`, `Level`, `Points`, `NextPoints`, `Progress`, `Warmth`, `Atmosphere`, `Trend`, `Tone`, `Streak` |
| komputasi `ready` / `loading` | `IsReady` / `IsLoading` | dari `health.Ok` / `health.Loading` |
| `localStorage['silverwolf_mirror_track']` | `ApplicationData.Current.LocalSettings` | **bukan** localStorage |

### Cara mengikat ke backend

Kontraknya sudah ada dan terverifikasi; tinggal dipanggil.

```csharp
var runtime = await CompanionRuntime.StartAsync(log: pesan => CrashLog.Tahap(pesan));
var backend = runtime.Backend;

// 1. kirim pesan — /api/chat TIDAK mengembalikan snapshot ikatan
var aliran = await backend.ChatAsync(riwayat, ct);
if (aliran.Status != 200) { TampilkanGalat(aliran.Error!); return; }

await foreach (var potong in aliran.Body!)
{
    if (potong.Err is { } e) { TampilkanGalat(e.Message); break; }
    TambahKeGelembung(potong.Text);
}

await SegarkanKizunaAsync();   // WAJIB — lihat kontrak #1 di bawah
```

```csharp
// 2. elus kepala dan pancing obrolan — keduanya mengembalikan snapshot
var sentuh    = await backend.TouchAsync(ct);               // snapshot PRA-sentuhan
var proaktif  = await backend.ProactiveAsync(idleDetik, ct);
```

Tiga hal yang mudah tertukar:

- `ChatAsync` mengembalikan `Kizuna = null` — **bukan bug**, itu paritas dengan
  `/api/chat` yang tidak pernah mengirim header `x-kizuna`.
- `TouchAsync` / `ProactiveAsync` mengembalikan snapshot **sebelum** interaksi
  dicatat, karena di aplikasi lama header disusun sebelum generator async
  berjalan.
- `HealthSnapshot.Kizuna` juga terisi, jadi polling health bisa dipakai untuk
  menyegarkan HUD tanpa memanggil `GetKizuna()` terpisah.

### Kontrak perilaku yang tidak boleh melenceng

Semuanya berasal dari aplikasi lama dan sudah dikunci oleh unit test.

1. **Segarkan snapshot setelah aliran `ChatAsync` selesai.** Aplikasi lama
   memanggil `fetchKizuna()` di akhir `store.send()`. Tanpa ini HUD ikatan
   tampak beku setelah mengobrol.
2. **Polling health: 8000 ms bila siap, 2000 ms bila belum.**
3. **Pekerja proaktif:** interval 5000 ms; picu hanya bila
   `!AutonomousMode || IsSending || audioBusy` terlewati **dan** hening >
   **65.000 ms**; nilai `idle` yang dikirim =
   `max(10, round((now - LastUserActivity) / 1000))`.
4. **Mirror memakai `ApplicationData.Current.LocalSettings`**, bukan
   `localStorage` — API browser tidak ada di WinUI 3.
5. **Nilai string yang dilihat pengguna tetap Indonesia**, termasuk tag
   ekspresi yang diteruskan ke `Mood.Perbarui`.
6. **Jangan hidupkan server HTTP lagi.** `CompanionBackend` sengaja
   mempertahankan *kontrak* HTTP (status, `{ error }`, snapshot) supaya
   perilakunya identik — bukan supaya soketnya kembali.
7. **Jangan ubah ukuran jendela.** 1180×760, minimum 780/560 — paritas
   Electron yang sudah dipetakan di M2.

### Placeholder Live2D

- Sediakan `SwapChainPanel` di `StageView` **sekarang**, biarkan kosong. M8
  yang mengisinya.
- Placeholder boleh berupa teks atau `Rectangle` sederhana, asalkan tidak
  mengubah tata letak dua panel.
- Tombol mirror tetap dipasang dan tetap menyimpan ke `LocalSettings`, walau
  belum ada yang di-mirror — perilakunya menunggu M8, bukan M13.
- 🚫 Tanpa WebView2, tanpa PixiJS, tanpa Cubism Core Web. Ketiganya sudah
  dibuang bersama aplikasi lama (lihat `docs/RIWAYAT-MIGRASI.md` §3.7).

### Cara menguji tanpa model 4,8 GB

```bash
# .env
VTUBER_STUB=true
```

Dengan itu `CompanionRuntime.PilihProvider` memilih `StubProvider`, yang
mengalirkan tiga potongan tetap (`"[senyum] "`, `"Sistem inti sudah hidup, "`,
`"Master."`) dan selalu melapor `Ok = true`. Artinya:

- seluruh jalur streaming UI teruji — penggabungan teks, pengetikan bertahap,
  deteksi tag — tanpa mengunduh model apa pun;
- `llama-server` tidak pernah dinyalakan, jadi tidak ada Vulkan yang perlu siap;
- kizuna **tetap mencatat**, jadi HUD ikatan bisa diuji kenaikannya.

Untuk menguji status "belum siap", balikkan ke `VTUBER_STUB=false` tanpa
menjalankan `llama-server`: `StatusText` akan menjadi `"memuat"` lalu
`"tidak-jalan"`, dan UI harus menampilkan keduanya dengan benar.

---

## Membaca `crash.log`

Berkasnya ada di sebelah exe (`CrashLog.JalurBerkas` =
`AppContext.BaseDirectory/crash.log`). Penanda yang sekarang dicatat:

| Penanda | Arti kalau ini yang terakhir muncul |
|---|---|
| `App() mulai` | gagal sangat dini, sebelum XAML |
| `InitializeComponent() selesai` | gagal di dalam `OnLaunched` |
| `akar = …` | akar repo ketemu; lanjut ke baris aset |
| `  HILANG <nama>` | aset itu belum ada di mesin ini — bukan kode yang salah |
| `MainWindow dibuat` | konstruktor `Window` dasar gagal → cek aturan #1 (bootstrap ganda) |
| `windowing: HWND = 0x…` | `InitializeWindowing()` jalan; lihat penanda berikutnya |
| `MainWindow diaktifkan` | startup tuntas |

**Bunyi kegagalan yang sudah dikenal:** log berhenti tepat *sebelum* baris
pertama konstruktor `MainWindow`, tanpa exception dan tanpa entri Event Log.
Itu bukan bug aplikasi — itu dua runtime Windows App SDK dalam satu proses
(§3.6). Periksa dulu bahwa `WindowsAppSdkBootstrapInitialize` masih `false`.

---

## Blok tempel tambahan — memulai M9

Tempel **setelah** blok utama di atas bila sesi berikutnya akan mengerjakan UI.

```
Kerjakan M9 (antarmuka). JANGAN menyentuh native/ - M8 bukan bagian dari tugas ini.

Yang harus ada di akhir M9:
  src/SilverWolf.App/ViewModels/CompanionViewModel.cs
  src/SilverWolf.App/Views/{StageView,ConsoleView,MessageListView,ComposerView}.xaml(.cs)
  src/SilverWolf.App/Configuration/AssetLocator.cs

Aturan M9:
  1. PERTahankan penanda tahap CrashLog.Tahap() di App.xaml.cs dan
     MainWindow.xaml(.cs). Setelah M9 selesai, crash.log harus tetap berakhir
     di "MainWindow diaktifkan".
  2. CompanionViewModel adalah SATU sumber keadaan (port store.js). Jangan
     pecah menjadi beberapa ViewModel.
  3. Panggil CompanionRuntime.StartAsync() sekali, simpan Backend-nya, jangan
     membuat runtime baru per interaksi.
  4. ChatAsync mengembalikan Kizuna = null. UI WAJIB menyegarkan snapshot
     sendiri setelah aliran selesai.
  5. TouchAsync / ProactiveAsync mengembalikan snapshot PRA-interaksi. Jangan
     "perbaiki" menjadi pasca-interaksi.
  6. Polling health 8000 ms bila siap, 2000 ms bila belum. Pekerja proaktif
     interval 5000 ms, picu bila hening > 65.000 ms.
  7. Mirror disimpan ke ApplicationData.Current.LocalSettings, bukan localStorage.
  8. StageView sediakan SwapChainPanel KOSONG sebagai placeholder. Tidak ada
     WebView2, tidak ada PixiJS.
  9. AssetLocator jangan mencari akar sendiri - pakai AppPaths.TentukanAkar().
 10. Identifier Inggris; nilai string yang dilihat pengguna tetap Indonesia.
 11. Ukuran jendela tetap 1180x760, minimum 780/560.

Cara menguji tanpa model 4,8 GB:
  set VTUBER_STUB=true di .env -> StubProvider mengalirkan 3 potongan tetap,
  kizuna tetap mencatat. Untuk menguji status "belum siap", set false tanpa
  menjalankan llama-server.

Verifikasi akhir (sama seperti sebelumnya):
  dotnet build SilverWolf.sln -p:Platform=x64 -c Debug   -> 0 error, 0 warning
  dotnet test  -> 108 lulus
  LALU JALANKAN exe-nya dan baca crash.log sampai "MainWindow diaktifkan".
```

---

## Riwayat revisi

| Tanggal | Perubahan |
|---|---|
| 2026-10-07 01:37 | Dibuat. Blok tempel utama: status M0–M7, 10 aturan, angka golden, plus ringkasan untuk sesi tanpa akses berkas |
| 2026-10-07 01:45 | Ditambahkan: peta kerja M9 (peta berkas, keadaan ViewModel, ikatan backend, 7 kontrak perilaku, placeholder Live2D, uji dengan `VTUBER_STUB`), cara membaca `crash.log`, blok tempel khusus M9, dan riwayat revisi |

⚠️ **Ketidakkonsistenan yang perlu dibersihkan lain waktu:** tajuk
`docs/RIWAYAT-MIGRASI.md` masih bertuliskan "Terakhir diperbarui: 2026-10-06"
dan "M3 … belum", padahal §6 mencatat seluruh prasyarat C++ **sudah selesai**
per 2026-10-07 00:07. Bagian §6-lah yang benar, dan dokumen ini memakai versi
itu.
