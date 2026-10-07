# Serah-terima M8 (renderer Live2D native) — ringkasan sesi

> **Cara pakai**: salin seluruh isi di bawah `--- PROMPT MULAI ---` ke sesi AI
> berikutnya. Ringkasan ini berdiri sendiri; sesi itu tidak melihat percakapan
> sebelumnya. Riwayat panjang: `docs/PROGRES-LIVE2D.md`. Dokumen umum proyek:
> `docs/PROMPT-LANJUTAN.md`, `docs/RIWAYAT-MIGRASI.md`.

--- PROMPT MULAI ---

# Tugas: selesaikan M8 (renderer Live2D native) — AI Vtuber WINUI3

Akar repo: `C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3`.
Model Cubism 2D Silver Wolf **sudah tampil, mengisi panel, dan bergerak**.
Jangan ulangi pekerjaan yang sudah beres. Kerjakan §2 secara berurutan.

## 1. Tiga cacat penentu yang SUDAH diperbaiki — jangan dibalik

Ketiganya sendirian sudah cukup membuat layar kosong, dan **tidak satu pun
menghasilkan galat saat build**. Semuanya hanya ketahuan dengan menjalankan
aplikasi lalu membaca kembali isi buffer gambarnya.

### 1.1 Penunjuk menggantung di `CubismFramework::StartUp`

- **SDK**: `Framework/src/CubismFramework.cpp:53-61` → `s_option = option;`
  — menyimpan **penunjuk**, bukan salinan.
- **Kode kita**: `native/SilverWolf.Live2D/Live2DStage.cpp:360`
  (`CubismFramework::Option g_opsi;` statis berkas), dipakai di `:889-894`.
- **Gejala menyesatkan**: pemuatan model dan tekstur **tetap berhasil** (kita
  memanggil `MuatBerkas` langsung), lalu proses mati mendadak hanya ketika
  **Cubism sendiri** memuat berkas — yaitu saat mengompilasi shader. Jejak
  tumpukan menunjuk `CubismShader_D3D11::GenerateShaders`, tetapi alamat
  kesalahannya (`0x5400000000`) di luar semua modul, seolah Cubism yang rusak.
- **Perbaikan**: `Option` wajib hidup sepanjang proses. Contoh resmi aman
  karena Option-nya anggota kelas `LAppDelegate` (singleton).

### 1.2 Viewport 1×1 dari `CreateRenderer(1, 1)`

- **Kode kita**: `CreateRenderer` dipanggil di `Live2DStage.cpp:561-562` dan
  `:719`; `AturUkuranTarget(lebar, tinggi)` di `:708`, dipanggil dari
  `swl2d_stage_resize` (`:1262`).
- **SDK**: `Rendering/D3D11/CubismRenderer_D3D11.cpp:911-914` — `PreDraw()`
  memanggil `SetDefaultRenderState(_modelRenderTargetWidth, _modelRenderTargetHeight)`
  → `SetViewport(ctx, 0, 0, w, h, 0, 1)`.
- **Gejala menyesatkan**: seluruh model digambar ke **satu piksel di sudut kiri
  atas**, `Present()` tetap `S_OK`, nol galat. Terukur: `terisi=1`,
  `kotak=(0,0)-(0,0)`.
- **Perbaikan**: `AturUkuranTarget` memanggil **ulang** `CreateRenderer`
  (bukan sekadar mengubah field) karena render target offscreen/mask dibuat di
  dalam `Initialize()` memakai ukuran saat itu — pola `LAppModel::ReloadRenderer()`.

### 1.3 Koreksi aspek memakai sisi yang salah

- **Kode lama**: selalu lanskap → `proyeksi.Scale(1.0f, lebar / tinggi);`
- **Kode sekarang**: `Live2DStage.cpp:1387` (`Scale(1.0f, aspek)`) dan `:1391`
  (`Scale(1.0f / aspek, 1.0f)`) — memilih sisi yang **lebih kecil**.
- **Acuan**: contoh resmi `LAppLive2DManager.cpp:256-263`. Panel kita potret
  (408×528), jadi harus `Scale(h/w, 1)`.
- **Gejala menyesatkan**: model tampak kecil dan **gepeng** — bukan karena
  perbesarannya kurang. Memperbesar `Perbesaran` tidak akan menolong.
- **Terkait, jangan diubah**: `matriks.SetHeight(2.0f * perbesaran)` di `:1416`.
  `CubismMatrix44::Scale` **MENIMPA** (`_tr[0]=x; _tr[5]=y;` —
  `CubismMatrix44.cpp:94-98`), jadi memanggil `Scale()` sesudah
  `CubismModelMatrix(w,h)` menghapus pemetaan kanvas `SetHeight(2.0f)`.
- Dua cacat pendamping yang juga sudah dibetulkan: `Initialize` dobel
  (dilewati, lihat `:571-577`) dan `StartFrame`/`EndFrame` mengapit `DrawModel`
  (`:679`/`:682`).

**Yang sudah benar dan tidak boleh diubah tanpa alasan kuat:** IID
`ISwapChainPanelNative` WinUI 3 = `63AAD0B8-7C24-40FF-85A8-640D944CC325`
(bukan `F92F19D2-...` milik XAML sistem); `g_opsi` statis; `CreateRenderer`
dipanggil ulang saat resize; `StartFrame`/`EndFrame`; koreksi aspek sisi kecil;
`SetHeight` bukan `Scale`; ukuran swap chain = DIP × `XamlRoot.RasterizationScale`.

## 2. Status cacat: dua sudah selesai, satu masih terbuka

> **Diperbarui 2026-10-07 (sesi lanjutan).** Dua dari tiga cacat di bawah sudah
> **selesai dan terverifikasi** — jangan dikerjakan ulang. Hanya §2.2 yang
> masih terbuka, dan sifatnya sudah dipersempit.

### 2.1 ✅ SELESAI — build Debug mati saat kompilasi shader

**Ini bukan cacat tersendiri: ia §1.1 (penunjuk menggantung) yang sama, hanya
terlihat di build Debug.**

Uji A/B yang membuktikannya (sudah dilakukan, tidak perlu diulang):

| Varian `Option` | Konfigurasi | Hasil |
|---|---|---|
| **Lokal** di `swl2d_init` | Debug | mati senyap tepat setelah kedua `MuatBerkas ... FrameworkShaders/*.fx` |
| **Statis** (`g_opsi`) | Debug | selesai, panggung aktif, model tampil |

Dengan `g_opsi` statis, `bash build-cli.sh Debug` menghasilkan DLL yang
menampilkan model (`periksa piksel: 511x661, terisi=270170`).

**Hipotesis stack overflow yang dulu dipegang gugur** — tidak ada `/STACK` yang
ditambahkan, tidak ada perubahan optimasi sumber SDK.

**Kenapa dulu tampak terpisah:** build Debug di folder keluaran **basi**
(dibangun 19:17, sumber diubah 19:26), jadi tidak mewakili kode terkini, dan
build Debug tidak pernah dijalankan ulang setelah `g_opsi` diperbaiki.
**Pelajaran: bangun ulang dari sumber terkini sebelum menyimpulkan apa pun
tentang sebuah konfigurasi.**

### 2.2 🔴 Proses mati saat llama.cpp memuat model — **terbuka, INTERMITEN**

- **Tidak deterministik.** Dari 6 kali jalan yang dipantau (masing-masing
  ≥45 detik): **5 hidup, 1 mati** di sekitar t=42s. Satu kali mati bukan bukti.
- **Gejala**: proses mati tanpa exception, `run.out` kosong, tanpa entri Event
  Log (diperiksa mandiri dengan `Get-WinEvent` — kosong, klaim lama benar).
  Baris log terakhir berasal dari llama.
- **Yang sudah disingkirkan:**
  - `VTUBER_STUB=true` → hidup 45 s (llama tidak terlibat).
  - `VTUBER_LLM_PROVIDER=local` (`-ngl 0`) → hidup 50 s, llama-server **selesai
    memuat** dan listening di :8788. Jadi llama sendiri sehat.
  - **llama-server mati bukan penyebabnya** — proses terpisah
    (`bin/llama/llama-server.exe`), tidak mungkin menjatuhkan proses kita.
  - **Bukan kode kita** — tidak ada `FailFast`/`Environment.Exit` di `src/`.
  - Satu kematian teramati di **~1 detik**, pada tahap pemuatan shader Live2D,
    **sebelum llama mengeluarkan satu baris pun** — jadi pemicunya bukan
    "llama selesai memuat".
- **Dugaan terkuat**: fail-fast di tingkat driver GPU — rebutan antara
  perangkat D3D11 kita dan konteks Vulkan llama-server
  (`VTUBER_VULKAN_NGL=99`, `VTUBER_VULKAN_CTX=16384`, KV cache f16),
  diperparah keluar-masuk perangkat cepat saat aplikasi dijalankan berulang.
- **Langkah berikutnya**: aktifkan WER LocalDumps (`HKCU\Software\Microsoft\
  Windows\Error Reporting\LocalDumps`) lalu ulangi sampai kena; uji konfirmasi
  dengan menurunkan `VTUBER_VULKAN_NGL`/`CTX`; kalau terkonfirmasi, serialkan
  inisialisasi Live2D terhadap `listening`-nya llama.
- **Selesai bila**: 10 kali jalan berturut-turut bertahan ≥2 menit.

### 2.3 ✅ SELESAI — napas, kedip, dan ikut kursor

Terpasang di `ModelPanggung::SiapkanEfek()` memakai `CubismBreath`,
`CubismEyeBlink`, dan `CubismLook`. **`CubismUpdateScheduler` sengaja tidak
dipakai** (urutan sedikit dan tetap) — urutan resmi `CubismUpdateOrder` diikuti
langsung: kedip(200) → ekspresi(300) → pandangan(400) → napas(500) → fisika(600).

Bukti log: `efek: kedip aktif, 2 parameter` / `efek: napas aktif` /
`efek: pandangan aktif (dengan peredaman CubismTargetPoint)`.

Peredaman terukur (sasaran `x=1.0`, ~9 bingkai konvergen):
`0.0000 → 0.1111 → 0.2444 → 0.5111 → 0.9587 → 1.0002`.

**Catatan penting:** `_model->SaveParameters()` sekarang dipanggil **sebelum**
efek hidup (sama dengan `LAppModel::Update()`), supaya nilainya tidak menumpuk
tiap bingkai.

**Yang belum: LipSync.** Grup `LipSync` (`ParamMouthOpenY`) sudah ada dan TTS
sudah jalan, tetapi mulut belum digerakkan.

### 2.4 ✅ SELESAI — penalaan bingkai (kepala kegedean)

Nilai akhir di `StageView.xaml.cs`: `Perbesaran = 0.88`, `GeserX = -0.10`,
`JangkarY = 0.10`.

Nilai lama 1.85 salah pindah dari web: di PIXI `VITE_AVATAR_ZOOM` mengalikan
skala "pas panel" lalu digeser jangkar 0.92, sedangkan di sini
`SetHeight(2.0 * perbesaran)` membuat model setinggi itu secara langsung —
akibatnya hanya kepala dan bahu yang terlihat. `GeserX = -0.10` perlu karena
sayap mekanik model jauh lebih panjang ke kanan (terukur 90 piksel menyentuh
tepi pada perbesaran 1.0, turun jadi 12 piksel).

**Menala tanpa membangun ulang:**

```bash
SWL2D_PERBESARAN=0.95 SWL2D_GESER_X=-0.05 SWL2D_JANGKAR_Y=0.10 ./SilverWolf.App.exe
SWL2D_TANGKAP="C:/.../tools/bingkai.bmp" ./SilverWolf.App.exe   # buang bingkai ke BMP
```

**Masih terbuka:** instrumentasi penyidikan (probe `D3DCompile`, log per
`MuatBerkas`, `PeriksaPiksel`) masih terpasang dan sebaiknya dijadikan opt-in
sebelum rilis.

### 2.5 ✅ SEBAGIAN — ketajaman ("kok kayak piksel banget")

Diperiksa, bukan ditebak. Diukur dari dalam aplikasi:

```
live2d: panel 408,8x528,8 DIP, skala=1,250, buffer=511x661
```

408,8 × 1,250 = 511 → buffer **tepat** seukuran piksel fisik panel. **Tidak ada
perentangan.** Layar memang 125% (1920×1080 fisik = 1536×864 DIP).

**Sudah diperbaiki — minifikasi tanpa mipmap.** Tekstur 4096×4096, sedangkan
karakter di layar hanya ~580 px tinggi → diperkecil ~6–7×. Tekstur dulu
`MipLevels = 1`, jadi detail halus (tulisan di visor, garis tipis) pecah.
`BuatTekstur` kini membuat rantai mip penuh lalu `GenerateMips`. Biaya: +134 MB
VRAM (total ~536 MB) — ingat ini kalau §2.2 ternyata soal tekanan memori GPU.

**Belum diperbaiki — cacat paritas ukuran jendela.** `AppWindow.Resize(1180,
760)` memakai **piksel fisik**, sedangkan Electron memakai **DIP**. Jadi jendela
ini 944×608 DIP — **20% lebih kecil** daripada aslinya, dan panggungnya 408 DIP
alih-alih ~519 DIP; model pun tampil lebih kecil dan terasa lebih kasar.
Perbaikannya `Resize(1180 * skala, 760 * skala)` dengan `GetDpiForWindow`.
**Belum dikerjakan karena mengubah ukuran jendela yang terlihat — perlu
persetujuan.**

**Alat untuk menilai ketajaman:** `tools/potret-jendela.py` memakai
`PrintWindow` + `PW_RENDERFULLCONTENT` sehingga tetap bekerja walau jendela
tertutup jendela lain. Wajib `SetProcessDpiAwarenessContext` lebih dulu — tanpa
itu ukurannya dibagi skala tampilan dan isinya terpotong (sempat menyesatkan).

## 3. Alat ukur terverifikasi — pakai ini, jangan bikin baru

| Alat | Lokasi / cara pakai | Kegunaan |
|---|---|---|
| **`PeriksaPiksel`** | `Live2DStage.cpp:1278`, dipanggil di `:1444` (bingkai ke-10) | **Membaca kembali back buffer swap chain**, menghitung piksel tidak tembus pandang + kotak pembatas. Pembeda "`Present()` sukses" vs "benar-benar tergambar" |
| `SWL2D_DIAG=1` | variabel lingkungan saat menjalankan | Penangkap pengecualian + pembuang 32 alamat tumpukan. **Jangan diaktifkan bawaan** |
| `SWL2D_NOLOG=1` | variabel lingkungan | Log native murni ke `swl2d-native.log`, melewati .NET |
| **`SimpanBingkai`** | `SWL2D_TANGKAP=<berkas.bmp>` | **Membuang back buffer ke BMP 32-bit.** Satu-satunya cara andal menilai pembingkaian secara visual — memotret layar gagal karena jendela aplikasi sering tertutup jendela lain dan `SetForegroundWindow` dari proses latar ditolak Windows |
| Penimpaan pembingkaian | `SWL2D_PERBESARAN` / `SWL2D_GESER_X` / `SWL2D_JANGKAR_Y` | Menala bingkai tanpa membangun ulang aplikasi C# |
| `tools/potret-jendela.py` | `python tools/potret-jendela.py keluar.png "Silver Wolf"` | Pemotret jendela lewat `PrintWindow` — **tetap bekerja walau jendela tertutup jendela lain**. Tanpa `Add-Type` yang diblokir kebijakan |
| `rva-lookup.py` | `python native/SilverWolf.Live2D/rva-lookup.py build/x64/Release/SilverWolf.Live2D.map 0x5E4D1` | RVA → nama fungsi (`.map` dibuat otomatis oleh `build-cli.sh`) |
| `CrashLog.Tulis` | `Diagnostics/CrashLog.cs` | `File.AppendAllText` — **tidak buffered**, jadi baris terakhir di `crash.log` benar-benar langkah terakhir yang dijalankan |

**Bukti terukur yang sudah tercapai** (angka dari `PeriksaPiksel`):

| Tahap | Buffer | Piksel terisi | Kotak |
|---|---|---|---|
| viewport 1×1 | 408×528 | 1 | (0,0)-(0,0) |
| `CreateRenderer` ukuran nyata | 408×528 | 5.934 | (159,230)-(272,323) |
| + koreksi aspek & `SetHeight` | 408×528 | 172.339 | (0,30)-(407,527) |
| + skala DPI (akhir) | **511×661** | **270.419** | (0,37)-(510,660) |

`511×661` = 408×1,25 dan 528×1,25 → layar berskala 125%, buffer kini piksel
fisik (itulah yang menghilangkan kesan "piksel jelek"). Gerakan idle
terkonfirmasi: `live2d: idle grup=isyarat indeks=2 -> 0`.

## 4. Jebakan toolchain

1. **Native**: `MSBuild.exe` **diblokir kebijakan keamanan**, dan `dotnet build`
   tidak punya `VCTargetsPath` (MSB4019) sehingga tak bisa membangun `.vcxproj`.
   Bangun lewat `bash native/SilverWolf.Live2D/build-cli.sh Release` (pakai
   **Release** sampai §2.1 selesai). Semua jalur ke `cl.exe` harus **gaya
   Windows** (`cygpath -w`); lib yang wajib: `ole32.lib user32.lib mincore.lib`.
2. **C# — perintah wajib, `Platform=x64` DAN `SelfContained=true`**:

   ```bash
   dotnet build src/SilverWolf.App/SilverWolf.App.csproj \
     -c Debug -p:Platform=x64 -r win-x64 -p:SelfContained=true
   ```

   Tanpa keduanya hasilnya **framework-dependent dan tidak bisa dijalankan**
   (`You must install or update .NET to run this application`). Folder VS
   (`bin/x64/Debug/...`) self-contained; folder `dotnet build` biasa
   (`bin/Debug/...`) tidak.
3. **Jangan pernah menyalin `runtimeconfig.json`/`deps.json` antar folder
   keluaran** — merusak aplikasi, dan build berikutnya **tidak** memperbaikinya
   karena MSBuild menganggap keluarannya masih baru. Perlu `-t:Rebuild`.
4. `C1041: cannot open program database` → sudah ditangani `-FS` (serialisasi
   `cl.exe`) + `-Fd` (PDB ke folder obj) di `build-cli.sh`.
5. **`kill -0 $PID` tidak bisa dipakai** untuk cek proses hidup di lingkungan
   ini — pakai `tasklist //FI "IMAGENAME eq SilverWolf.App.exe"`.
6. Penangkap pengecualian yang memanggil balik ke .NET merusak runtime:
   `Fatal error. Invalid Program: attempted to call a UnmanagedCallersOnly
   method from managed code.` → hanya `SWL2D_DIAG=1`.
7. **`ApplicationData.Current` tidak berlaku** untuk aplikasi unpackaged
   (`WindowsPackageType=None`) → `InvalidOperationException`. Sudah diganti
   `Configuration/UiSettings.cs` (JSON di `LocalApplicationData\SilverWolf\`).
8. XML tidak boleh memuat `--` di komentar — `build/CubismSdk.props` diimpor
   `Directory.Build.props`, satu salah tulis mematikan seluruh solusi.
9. `PublishTrimmed` harus tetap **False** (trimming + XAML/WinRT = kerusakan
   dikenal). Rilis **x64 saja**.
10. Lingkungan ini punya pengaman **safe-delete** yang menolak hapus permanen
    bila trash gagal — jangan diakali.

## 5. Perintah (sudah terbukti jalan)

```bash
cd "C:/Users/Daffa/Desktop/AI Vtuber Project/AI Vtuber WINUI3"

# 1. Bangun native — pakai Release sampai §2.1 selesai
bash native/SilverWolf.Live2D/build-cli.sh Release

# 2. Bangun C# — Platform=x64 dan SelfContained WAJIB
dotnet build src/SilverWolf.App/SilverWolf.App.csproj \
  -c Debug -p:Platform=x64 -r win-x64 -p:SelfContained=true

# 3. Sebarkan native ke folder keluaran
T="src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64"
NAT="native/SilverWolf.Live2D/build/x64/Release"
cp -f "$NAT/SilverWolf.Live2D.dll" "$T/"
mkdir -p "$T/FrameworkShaders"
cp -f "$NAT/FrameworkShaders/"*.fx "$T/FrameworkShaders/"

# 4. Jalankan dan baca buktinya
cd "$T" && rm -f crash.log run.out && (./SilverWolf.App.exe > run.out 2>&1 &)
sleep 30
grep -a -E "kanvas model|periksa piksel|idle grup|panggung dibuat" crash.log | tail -4
tasklist //FI "IMAGENAME eq SilverWolf.App.exe"
```

Keluaran yang diharapkan (angka boleh berbeda, urutan besarnya yang penting):

```
live2d: panggung 1 aktif pada percobaan 1
live2d: idle grup=isyarat indeks=2 -> 0
[live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=1.85, geser=(0.00,0.00)
[live2d] [swl2d] periksa piksel (bingkai ke-10): 511x661, terisi=270419, kotak=(0,37)-(510,660)
```

## 6. Definisi selesai

1. Build **Debug** maupun **Release** sama-sama menampilkan model
2. Aplikasi bertahan ≥2 menit dengan Live2D aktif dan llama memuat model
3. Model bernapas, berkedip, dan mengikuti kursor dengan halus
4. `crash.log` bersih dari exception tak tertangani
5. Instrumentasi penyidikan sudah dibersihkan atau dijadikan opt-in

**Laporkan** dengan format: (a) apa yang diubah — berkas + inti perubahan;
(b) bukti terukur sebelum/sesudah (kutip baris log); (c) apa yang masih belum
selesai beserta langkah konkret berikutnya. Sebutkan eksplisit bila ada klaim
yang belum diverifikasi sendiri.

--- PROMPT SELESAI ---
