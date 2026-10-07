# Progres — Model Live2D Silver Wolf tampil di WinUI 3

**Tanggal**: 2026-10-07
**Cakupan**: M8 (renderer Live2D native) — dari "model tidak muncul" sampai
"model tampil, penuh, dan bergerak"
**Status**: 🟢 **Tampil dan bergerak** · 🟡 satu cacat Debug + satu kematian proses belum tuntas

---

## 1. Ringkasan

Model Cubism 2D Silver Wolf **sudah tampil** di `SwapChainPanel`, **mengisi
seluruh panel**, **tajam**, dan **bergerak** dengan gerakan idle berulang.

Yang menentukan: tiga cacat bertumpuk yang masing-masing sendirian sudah cukup
membuat layar kosong, dan ketiganya tidak menghasilkan satu pun galat saat
build. Semuanya hanya ketahuan dengan **menjalankan aplikasi lalu membaca
kembali isi buffer gambarnya**.

---

## 2. Bukti terukur (bukan klaim)

Perbandingan sesudah tiap perbaikan. Angka "terisi" adalah jumlah piksel tidak
tembus pandang yang dibaca kembali langsung dari back buffer swap chain.

| Tahap | Ukuran buffer | Piksel terisi | Kotak pembatas | Arti |
|---|---|---|---|---|
| Awal (DLL belum dibangun) | — | — | — | placeholder, jalur degradasi |
| Setelah IID benar | 408×528 | — | — | crash di kompilasi shader |
| Setelah pointer Option diperbaiki | 408×528 | **1** | (0,0)-(0,0) | viewport 1×1 — satu piksel di sudut |
| Setelah `CreateRenderer` ukuran nyata | 408×528 | **5.934** | (159,230)-(272,323) | model muncul tapi kecil & gepeng |
| Setelah koreksi aspek + `SetHeight` | 408×528 | **172.339** | (0,30)-(407,527) | mengisi panel |
| Setelah skala DPI (build akhir) | **511×661** | **270.419** | (0,37)-(510,660) | mengisi panel, piksel fisik, tajam |

Catatan `511×661`: 408 × 1,25 = 510 dan 528 × 1,25 = 660. Layar berjalan pada
**skala 125%**, dan buffer sekarang mengikuti piksel fisik — inilah yang
menghilangkan kesan "piksel jelek".

Gerakan idle terkonfirmasi dari log:
`live2d: idle grup=isyarat indeks=2 -> 0` (0 = berhasil).

---

## 3. Akar masalah yang ditemukan dan diperbaiki

Tiga belas cacat. Nomor 1, 2, dan 5 adalah yang paling menentukan.

### 3.1 IID `ISwapChainPanelNative` salah namespace 🔴

XAML sistem dan WinUI 3 sama-sama punya `SwapChainPanel`, dan keduanya
mengekspos antarmuka native bernama sama, **tetapi IID-nya berbeda**.

| Objek | IID |
|---|---|
| `Windows.UI.Xaml.Controls.SwapChainPanel` | `F92F19D2-3ADE-45A6-A20C-F6F1EA90554B` |
| `Microsoft.UI.Xaml.Controls.SwapChainPanel` | **`63AAD0B8-7C24-40FF-85A8-640D944CC325`** |
| turunan `ISwapChainPanelNative2` | `88FD8248-10DA-4810-BB4C-010DD27FAEA9` |

Header WinUI ada di
`~/.nuget/packages/microsoft.windowsappsdk.winui/2.3.9/include/microsoft.ui.xaml.media.dxinterop.h`
— **bukan** di header SDK Windows (`um/windows.ui.xaml.media.dxinterop.h`).

IID yang salah menghasilkan `E_NOINTERFACE` (0x80004002) dan layar kosong tanpa
pesan apa pun.

**Kesimpulan keliru yang pernah diambil:** memindai byte GUID `F92F19D2...` di
DLL `Microsoft.UI*.dll` tidak menemukannya, lalu disimpulkan "WinUI 3 tidak
mengimplementasikan `ISwapChainPanelNative`". **Itu salah.** Pemindaian byte
GUID tidak bisa dipakai untuk menyimpulkan ada/tidaknya sebuah antarmuka.

Perbaikan: antarmuka dideklarasikan sendiri di `Live2DStage.cpp`
(`ISwapChainPanelNativeWinUI` + `IID_ISwapChainPanelNativeWinUI`) supaya tidak
mungkin tertukar dengan header yang salah.

### 3.2 `CubismFramework::StartUp` menyimpan PENUNJUK, bukan salinan 🔴

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

Gejalanya sangat menyesatkan: pemuatan model dan tekstur **tetap berhasil**
(kode kita memanggil `MuatBerkas` langsung), lalu proses mati mendadak hanya
ketika **Cubism sendiri** memuat berkas — yaitu saat mengompilasi shader.
Jejak tumpukan menunjuk ke `CubismShader_D3D11::GenerateShaders`, tetapi
alamat kesalahannya (`0x5400000000`) di luar semua modul, seolah Cubism yang
rusak.

Perbaikan: `CubismFramework::Option g_opsi;` sebagai variabel statis berkas.
Contoh resmi aman karena Option-nya anggota kelas `LAppDelegate` (singleton).

### 3.3 Viewport 1×1 → model hanya satu piksel 🔴

`CreateRenderer(1, 1)` dipakai saat panggung dibuat (ukuran panel belum
diketahui). Cubism memakai nilai itu untuk **menyetel viewport**:

```cpp
// Framework/src/Rendering/D3D11/CubismRenderer_D3D11.cpp:911-914
void CubismRenderer_D3D11::PreDraw()
{
    SetDefaultRenderState(_modelRenderTargetWidth, _modelRenderTargetHeight);
}
// → GetRenderState()->SetViewport(_context, 0, 0, width, height, 0, 1)
```

Akibatnya seluruh model digambar ke **satu piksel di sudut kiri atas**.
`Present()` tetap `S_OK` dan tidak ada satu pun galat — persis pola
`terisi=1, kotak=(0,0)-(0,0)` yang terukur.

Perbaikan: `ModelPanggung::AturUkuranTarget(lebar, tinggi)` dipanggil dari
`swl2d_stage_resize`. Ia memanggil ulang `CreateRenderer` (bukan sekadar
mengubah field) karena render target offscreen/mask dibuat di dalam
`Initialize()` memakai ukuran saat itu, lalu mengikat ulang tekstur. Pola ini
sama dengan `LAppModel::ReloadRenderer()` di contoh resmi.

### 3.4 `Initialize` dipanggil dua kali

`CubismUserModel::CreateRenderer` **sudah** memanggil
`_renderer->Initialize(_model, maskBufferCount)` secara internal
(`CubismUserModel.cpp:294`). Pemanggilan tambahan di kode kita menghapus dan
membuat ulang seluruh render target tanpa alasan. Dihapus.

### 3.5 Koreksi aspek salah sisi

```cpp
// Kode lama — SELALU bentuk lanskap
proyeksi.Scale(1.0f, lebar / tinggi);
```

Contoh resmi memilih berdasarkan orientasi
(`LAppLive2DManager.cpp:256-263`):

```cpp
if (lebar > tinggi) proyeksi.Scale(1.0f, lebar / tinggi);   // lanskap
else                proyeksi.Scale(tinggi / lebar, 1.0f);   // potret
```

Panel kita potret (408×528). Kode lama memampatkan model ke arah vertikal
sehingga tampak kecil dan gepeng — bukan karena perbesarannya kurang.

### 3.6 `CubismMatrix44::Scale` MENIMPA, bukan mengalikan

```cpp
// Framework/src/Math/CubismMatrix44.cpp:94-98
void CubismMatrix44::Scale(csmFloat32 x, csmFloat32 y)
{
    _tr[0] = x;
    _tr[5] = y;
}
```

Konstruktor `CubismModelMatrix(w, h)` sudah menyetel tinggi model agar
memetakan ke rentang `[-1, 1]` lewat `SetHeight(2.0f)`. Memanggil
`matriks.Scale(1.85, 1.85)` sesudahnya **menghapus pemetaan kanvas itu**.
Perbaikannya memakai `matriks.SetHeight(2.0f * perbesaran)` yang menghitung
sendiri faktornya (`h / _height`).

Kanvas model ini terukur **0,6 × 0,4** (satuan ternormalisasi), jadi
`perbesaran = 1.85` berarti tinggi tampil = 1,85× tinggi panel.

### 3.7 `StartFrame` / `EndFrame` tidak pernah dipanggil

`CubismRenderer_D3D11.hpp:122,128` mendeklarasikan keduanya, dan contoh resmi
SELALU membungkus `DrawModel()` dengannya. Tanpa `StartFrame`, state D3D tidak
disiapkan sebelum menggambar.

### 3.8 Ukuran swap chain dalam DIP, bukan piksel fisik

`ActualWidth/ActualHeight` XAML satuannya DIP. Swap chain bekerja dalam piksel
fisik. Tanpa dikali `XamlRoot.RasterizationScale`, pada layar 125% buffer
gambarnya lebih kecil daripada panelnya sehingga hasilnya buram karena
direntangkan (`DXGI_SCALING_STRETCH`).

### 3.9 Penangkap pengecualian diagnostik merusak runtime .NET

Penangkap yang memanggil balik ke C# (`g_log`) dari dalam proses pengecualian
membuat proses mati dengan:

```
Fatal error. Invalid Program: attempted to call a UnmanagedCallersOnly method from managed code.
```

Terbukti lewat uji A/B: tanpa DLL native aplikasi hidup normal. Perbaikan:
penangkap hanya dipasang bila `SWL2D_DIAG=1`.

### 3.10 Model diam karena tidak ada gerakan idle

Renderer berjalan 30 fps, tetapi bingkainya identik — jadi tampak beku.
Ditambahkan `swl2d_stage_play_idle` → `ACubismMotion::SetLoop(true)` dengan
**prioritas 1** (paling rendah) supaya gerakan lain bisa menyela tanpa perlu
menghentikan loop lebih dulu.

Grup dan indeks dari `silverwolf.model3.json`:
`Motions.isyarat = [berubah-1, berubah-2, siklus, tidur]` → indeks **2** =
`siklus`.

### 3.11 ~~Build Debug mati saat kompilasi shader, Release berhasil~~ ✅ SELESAI

> **DITUTUP 2026-10-07 (sesi lanjutan). Ini BUKAN cacat tersendiri — ia adalah
> §3.2 yang sama, terlihat hanya di build Debug.** Bukti uji A/B di bawah.

Gejala yang dulu tercatat: dengan build **Debug**, proses mati senyap di dalam
`CubismShader_D3D11::GenerateShaders` — tanpa exception, tanpa keluaran
`run.out`, tanpa entri Event Log, dan penangkap VEH pun tidak menyala (ciri
*fast-fail*).

**Uji A/B yang menutupnya** (`native/SilverWolf.Live2D/Live2DStage.cpp`):

| Varian `Option` | Konfigurasi | Hasil |
|---|---|---|
| **Lokal** di `swl2d_init` | Debug | mati senyap tepat setelah kedua `MuatBerkas ... FrameworkShaders/*.fx` dibaca |
| **Statis** (`g_opsi`) | Debug | selesai, panggung aktif, model tampil |

Log varian yang mati — persis sama dengan kutipan lama:

```
[live2d] [swl2d] sebelum GetDeviceInfo
[live2d] [swl2d] MuatBerkas dipanggil: FrameworkShaders/CubismEffect.fx
[live2d] [swl2d] MuatBerkas dipanggil: FrameworkShaders/CubismBlendMode.fx
   ← berhenti di sini, proses mati
```

**Kenapa dulu tampak seperti masalah terpisah:** waktu itu yang diperiksa hanya
build Debug **sebelum** `g_opsi` diperbaiki, lalu kesimpulannya ikut terbawa
setelah perbaikannya masuk — build Debug tidak pernah dijalankan ulang. Build
Debug yang ada di folder keluaran pun **basi** (dibangun 19:17, sumber diubah
19:26), sehingga tidak mewakili kode terkini.

**Kenapa hanya Debug yang tampak:** pada penunjuk menggantung, isi kerangka
stack yang sudah mati dibaca sebagai penunjuk fungsi. Build Debug mengisi
kerangka stack dengan pola racun (`0xCC`/`0xDD`) sehingga penunjuk itu pasti
tidak bisa dipanggil; build Release kebetulan masih menyisakan nilai yang benar
di lokasi itu. Mekanismenya sama, yang berbeda hanya keberuntungan tata letak.

**Hipotesis stack overflow yang dulu dipegang: TIDAK TERBUKTI, dan sekarang
gugur.** Tidak ada `/STACK` yang ditambahkan, tidak ada perubahan optimasi
sumber SDK — build Debug dari sumber yang sama berhasil apa adanya.

### 3.12 `C1041: cannot open program database`

Dua `cl.exe` yang berjalan bersamaan (build sebelumnya belum benar-benar
selesai) berebut berkas PDB yang sama. Diperbaiki dengan `-FS` (serialisasi)
dan `-Fd` (arahkan PDB ke folder obj, sekaligus tidak mengotori akar repo).

### 3.13 `dotnet build` menghasilkan aplikasi framework-dependent

Folder keluaran Visual Studio (`bin/x64/Debug/...`) **self-contained**,
sedangkan `dotnet build` biasa menghasilkan **framework-dependent** yang tidak
bisa dijalankan. Perintah yang benar:

```bash
dotnet build src/SilverWolf.App/SilverWolf.App.csproj \
  -c Debug -p:Platform=x64 -r win-x64 -p:SelfContained=true
```

> **Jebakan yang sempat terjadi:** menyalin `runtimeconfig.json` dan
> `deps.json` dari folder framework-dependent ke folder self-contained
> merusak aplikasi (`You must install or update .NET to run this application`)
> — dan build berikutnya **tidak** memperbaikinya karena MSBuild menganggap
> keluarannya masih baru. Harus `-t:Rebuild`. **Jangan pernah menyalin
> `runtimeconfig.json`/`deps.json` antar folder keluaran.**

### 3.14 Napas, kedip, dan ikut-kursor — DITAMBAHKAN ✅

Dulu belum ada sama sekali. Sekarang memakai kelas Cubism 5 `CubismBreath`,
`CubismEyeBlink`, dan `CubismLook`, disiapkan sekali di
`ModelPanggung::SiapkanEfek()`.

**Sengaja TIDAK memakai `CubismUpdateScheduler`.** Scheduler itu hanya berguna
kalau updater perlu diurutkan otomatis; di sini jumlahnya sedikit dan urutannya
tetap. `CubismLookUpdater` juga dilewati karena ia menuntut `CubismTargetPoint`
yang diurus scheduler — padahal `CubismLook::UpdateParameters(model, x, y)`
bisa dipanggil langsung.

Urutan resmi dari `Framework/src/Motion/ICubismUpdater.hpp`
(`CubismUpdateOrder`) tetap diikuti di `ModelPanggung::Perbarui()`:

```
kedip(200) -> ekspresi(300) -> pandangan(400) -> napas(500) -> fisika(600)
```

**Perubahan urutan yang menyertainya:** `_model->SaveParameters()` kini
dipanggil **sebelum** efek hidup, bukan sesudah — sama dengan
`LAppModel::Update()` (`LAppModel.cpp:384-400`). Kalau efek hidup ikut
tersimpan, nilainya menumpuk terus setiap bingkai.

**Peredaman ikut-kursor — terukur.** `AturPandang()` tidak lagi menulis
parameter langsung; ia hanya mengisi sasaran ke `CubismTargetPoint`, dan
`Perbarui()` membaca nilainya yang sudah dihaluskan. Faktor lama (30 untuk sudut
X/Y, 10 untuk badan) pindah ke `LookParameterData` sehingga perilakunya setara.
Uji dengan sasaran `x=1.0`:

```
uji pandang bingkai 0: x=0.0000     ← sasaran sudah 1.0, nilai masih 0
uji pandang bingkai 1: x=0.1111
uji pandang bingkai 4: x=0.5111
uji pandang bingkai 8: x=0.9587
uji pandang bingkai 9: x=1.0002     ← konvergen, sedikit overshoot (kelembaman)
```

Sebelumnya nilai itu melompat langsung ke 1.0 dalam satu bingkai.

### 3.15 Bingkai visual: kepala kegedean, ternyata perbesaran warisan web ✅

**Gejala.** Kepala memenuhi hampir seluruh panel; hanya kepala dan bahu yang
terlihat.

**Sebab.** `Perbesaran = 1.85` di `StageView.xaml.cs` adalah warisan aplikasi
web (`VITE_AVATAR_ZOOM=1.85`) yang **tidak bisa dipindahkan apa adanya**:

| | Aplikasi web (PIXI) | Di sini (Cubism) |
|---|---|---|
| arti zoom | pengali skala "pas panel" | `SetHeight(2.0 * perbesaran)` langsung |
| jangkar | `anchor.set(x, 0.92)`, `y = H * 0.92` | `SetPosition` pada ruang `[-1, 1]` |

Di web, `zoom=1.85` mengalikan skala yang sudah "pas panel" lalu digeser oleh
jangkar 0.92; di sini 1.85 berarti tinggi tampil 3.7 satuan pada rentang pandang
2.0 satuan — hanya ~54% tinggi model yang masuk panel, dan bagian tengah yang
terlihat kebetulan kepala.

**Nilai akhir yang dipilih dari pengukuran** (bukan tebakan):

| Perbesaran | Piksel terisi | Kotak | Ujung sayap terpotong |
|---|---|---|---|
| 1.85 (lama) | 270.523 | (0,32)-(510,660) | ya, parah (kepala saja) |
| 1.00 | 129.627 | (47,129)-(510,575) | 90 piksel di tepi kanan |
| 0.88 + geser −0.10 | 103.565 | (39,155)-(510,547) | **12 piksel** |
| 0.75 | 75.268 | (99,170)-(510,504) | 3 piksel, tetapi model terlalu kecil |

0.88 dipilih karena seluruh karakter (termasuk ujung sayap mekaniknya yang
asimetris) masuk panel sementara tingginya masih memenuhi sekitar dua pertiga
panel. `GeserX = -0.10` perlu karena seni model ini tidak simetris — sayapnya
jauh lebih panjang ke kanan, jadi tanpa geser ujungnya terpotong tepi.

**Cara menala tanpa membangun ulang** (penimpaan diagnostik di
`swl2d_stage_set_view`, hanya berlaku bila variabelnya diset):

```bash
SWL2D_PERBESARAN=0.95 SWL2D_GESER_X=-0.05 SWL2D_JANGKAR_Y=0.10 ./SilverWolf.App.exe
```

### 3.16 Ketajaman: buffer sudah 1:1, dan tekstur kini punya mipmap

**Keluhan "kok kayak piksel banget" diperiksa, bukan ditebak.**

Pertama, apakah buffer lebih kecil daripada panelnya sehingga direntangkan?
Tidak. Diukur langsung dari aplikasi:

```
live2d: panel 408,8x528,8 DIP, skala=1,250, buffer=511x661
```

408,8 × 1,250 = 511 dan 528,8 × 1,250 = 661 — buffer **tepat** seukuran piksel
fisik panel. Tidak ada perentangan.

> **Koreksi catatan lama.** §2 dan §3.8 dulu menulis "layar berjalan pada skala
> 125%" sebagai kesimpulan dari 511/408. Itu benar, tetapi **alasannya keliru**:
> yang membuat panel selebar 408 DIP bukan pembagian 44% dari jendela 1180 DIP,
> melainkan karena `AppWindow.Resize(1180, 760)` memakai **piksel fisik**, bukan
> DIP. Jadi jendelanya 944×608 DIP; 44% darinya ≈ 415 DIP, dikurangi padding
> menjadi 408,8 DIP. Layar memang 125% (1920×1080 fisik = 1536×864 DIP).

**Cacat paritas yang ditemukan (belum diperbaiki):** aplikasi Electron memakai
`width: 1180, height: 760` dalam DIP, sedangkan `AppWindow.Resize` di sini
menafsirkannya sebagai piksel fisik. Akibatnya jendela ini **20% lebih kecil**
daripada aslinya, dan panggungnya 408 DIP alih-alih ~519 DIP — model pun tampil
lebih kecil dan detail halusnya terasa lebih kasar. Perbaikannya:
`Resize(1180 * skala, 760 * skala)` dengan `GetDpiForWindow`.

**Penyebab yang benar-benar diperbaiki — minifikasi tanpa mipmap.** Tekstur
model 4096×4096, sedangkan di layar seluruh karakter hanya menempati ~580 px
tinggi → teksturnya diperkecil ~6–7 kali. Tekstur dulu dibuat dengan
`MipLevels = 1`, jadi sampler hanya punya level 0 dan detail halus (tulisan di
visor, garis tipis) pecah. `BuatTekstur` sekarang membuat rantai mip penuh
(`MipLevels = 0` + `D3D11_RESOURCE_MISC_GENERATE_MIPS`) lalu memanggil
`GenerateMips`.

Catatan implementasi: tekstur dibuat **tanpa** `pInitialData` karena dengan
`MipLevels = 0` D3D menuntut data untuk semua sub-sumber; level 0 diisi lewat
`UpdateSubresource` lalu `GenerateMips` dipanggil.

Biaya: 6 tekstur 4096² + rantai mip ≈ 536 MB VRAM (naik ~134 MB). Ini perlu
diingat kalau cacat §6.2 (kematian saat llama memuat model) ternyata memang
soal tekanan memori GPU.

---

## 4. Alat diagnosis yang dibuat (dipakai ulang, jangan dihapus)

| Alat | Berkas | Kegunaan |
|---|---|---|
| Skrip build native tanpa Visual Studio | `native/SilverWolf.Live2D/build-cli.sh` | `MSBuild.exe` diblokir kebijakan; `dotnet build` tidak punya `VCTargetsPath` (MSB4019). Pakai: `bash build-cli.sh [Debug\|Release]` |
| Penerjemah RVA → nama fungsi | `native/SilverWolf.Live2D/rva-lookup.py` | `python rva-lookup.py build/x64/Release/SilverWolf.Live2D.map 0x5E4D1` |
| Penangkap pengecualian + pembuang tumpukan | `Live2DStage.cpp` → `CatatPengecualian` | Aktif hanya dengan `SWL2D_DIAG=1`. Mencatat kode, alamat, modul, RVA, dan 32 alamat kembali dari tumpukan |
| Pembacaan kembali piksel | `Live2DStage.cpp` → `PeriksaPiksel` | Membuktikan model benar-benar menulis piksel, bukan sekadar `Present()` sukses |
| Pembuangan bingkai ke BMP | `Live2DStage.cpp` → `SimpanBingkai` | Aktif dengan `SWL2D_TANGKAP=<berkas.bmp>`. **Satu-satunya cara andal menilai pembingkaian secara visual** — memotret layar gagal karena jendela aplikasi sering tertutup jendela lain dan `SetForegroundWindow` dari proses latar ditolak Windows |
| Penimpaan pembingkaian | `Live2DStage.cpp` → `swl2d_stage_set_view` | `SWL2D_PERBESARAN`, `SWL2D_GESER_X`, `SWL2D_JANGKAR_Y` — menala bingkai tanpa membangun ulang aplikasi C# |
| Pemotret jendela | `tools/potret-jendela.py` | `python tools/potret-jendela.py keluar.png "Silver Wolf"`. Memakai `PrintWindow` + `PW_RENDERFULLCONTENT`, jadi **tetap bekerja walau jendela tertutup jendela lain** (memotret layar tidak). Wajib memanggil `SetProcessDpiAwarenessContext` lebih dulu — tanpa itu ukurannya dibagi skala tampilan dan isinya terpotong |
| Jalur log native murni | `Live2DStage.cpp` → `CatatKeBerkasNative` | Aktif dengan `SWL2D_NOLOG=1` → `swl2d-native.log`. Memisahkan masalah panggilan balik terkelola dari masalah Cubism |

**Aturan penting:** `CrashLog.Tulis` memakai `File.AppendAllText` — **tidak
buffered**. Jadi baris terakhir di `crash.log` benar-benar langkah terakhir
yang dijalankan. Penanda tahap bisa dipercaya sepenuhnya.

---

## 5. Perintah verifikasi

```bash
cd "C:/Users/Daffa/Desktop/AI Vtuber Project/AI Vtuber WINUI3"

# 1. Native (pakai Release — Debug masih mati di kompilasi shader)
bash native/SilverWolf.Live2D/build-cli.sh Release

# 2. C# — WAJIB menyertakan Platform=x64 dan SelfContained=true
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
grep -a -E "kanvas model|periksa piksel|idle grup" crash.log | tail -3
```

Yang diharapkan muncul:

```
live2d: idle grup=isyarat indeks=2 -> 0
[live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=1.85, geser=(0.00,0.00)
[live2d] [swl2d] periksa piksel (bingkai ke-10): 511x661, terisi=270419, kotak=(0,37)-(510,660)
```

> Catatan: `kill -0 $PID` **tidak bisa** dipakai untuk mengecek proses hidup di
> lingkungan ini — pakai `tasklist //FI "IMAGENAME eq SilverWolf.App.exe"`.

---

## 6. Yang masih terbuka

1. **✅ SELESAI — Build Debug mati saat kompilasi shader.** Bukan cacat
   tersendiri: itu §3.2 (penunjuk menggantung) yang sama. Bukti uji A/B ada di
   §3.11. Build Debug sekarang menyelesaikan `GenerateShaders` dan menampilkan
   model, tanpa perubahan flag apa pun.
2. **🟡 Proses mati saat llama.cpp memuat model — TERNYATA INTERMITEN.** Bukan
   deterministik: dari 6 kali jalan yang dipantau, 5 bertahan ≥45 detik dan 1
   mati di ~t=42s. Penyelidikan sesi lanjutan (2026-10-07):

   | Uji | Hasil |
   |---|---|
   | `VTUBER_STUB=true` (llama tidak pernah jalan) | hidup 45 s |
   | `VTUBER_LLM_PROVIDER=local` (`-ngl 0`, CPU saja) | hidup 50 s, llama-server **selesai memuat** dan listening di :8788 |
   | Vulkan (`-ngl 99`) | 5 dari 6 hidup; llama sampai `processing task` |

   Yang sudah bisa disingkirkan:
   - **llama-server mati bukan penyebabnya** — ia proses terpisah
     (`bin/llama/llama-server.exe`, dijalankan `LlamaServerProcess.cs`), jadi
     kematiannya tidak mungkin menjatuhkan proses kita.
   - **Bukan kode kita yang memanggil fail-fast** — tidak ada `FailFast` maupun
     `Environment.Exit` di seluruh `src/`.
   - **Bukan entri log yang hilang** — Event Log diperiksa mandiri dengan
     `Get-WinEvent` (Application + Windows Error Reporting): kosong, sama
     seperti catatan lama.
   - Satu kematian yang teramati (22:07) terjadi di **~1 detik**, pada tahap
     pemuatan shader Live2D, **sebelum llama mengeluarkan satu baris pun** —
     jadi pemicunya bukan "llama selesai memuat".

   **Dugaan terkuat: fail-fast di tingkat driver GPU**, akibat rebutan antara
   perangkat D3D11 kita dan konteks Vulkan llama-server (`-ngl 99`,
   `VTUBER_VULKAN_CTX=16384`, KV cache f16), diperparah oleh keluar-masuk
   perangkat yang cepat saat aplikasi dijalankan berulang kali.

   **Langkah berikutnya:** aktifkan WER LocalDumps (`HKCU\Software\Microsoft\
   Windows\Error Reporting\LocalDumps`) untuk mendapat dump, atau kurangi
   tekanan Vulkan (`VTUBER_VULKAN_NGL`/`VTUBER_VULKAN_CTX`) sebagai uji
   konfirmasi, atau serialkan pemanasan llama terhadap inisialisasi Live2D.
3. **✅ SELESAI — Napas, kedip, dan ikut kursor.** Lihat §3.14.
4. **✅ SELESAI — Penalaan bingkai visual.** Lihat §3.15. Nilai akhir:
   `Perbesaran = 0.88`, `GeserX = -0.10`, `JangkarY = 0.10`.
5. **🟡 Instrumentasi penyidikan masih terpasang** (probe D3DCompile, log per
   pemanggilan `MuatBerkas`, `PeriksaPiksel`). Berfungsi dan tidak mengganggu,
   tetapi sebaiknya dibersihkan atau dijadikan opt-in sebelum rilis.
6. **🟡 LipSync belum ada.** Grup `LipSync` (`ParamMouthOpenY`) sudah
   tersedia di `model3.json` dan TTS sudah jalan, tetapi belum ada yang
   menggerakkan mulut. Butuh `CubismLipSyncUpdater` (menuntut
   `CubismUpdateScheduler`) atau jalur sederhana: set `ParamMouthOpenY` dari
   amplitudo audio saat TTS berbicara.
7. **🟡 Ukuran jendela 20% lebih kecil daripada aplikasi lama.**
   `AppWindow.Resize(1180, 760)` memakai piksel fisik, sedangkan Electron
   memakai DIP. Perbaikannya `Resize(1180 * skala, 760 * skala)` dengan
   `GetDpiForWindow`. Belum dikerjakan karena mengubah ukuran jendela yang
   terlihat. Rincian di §3.16.

---

## 7. Berkas yang berubah

| Berkas | Perubahan |
|---|---|
| `native/SilverWolf.Live2D/Live2DStage.cpp` | IID WinUI; `g_opsi` statis; `AturUkuranTarget`; `StartFrame`/`EndFrame`; koreksi aspek; `SetHeight`; `MainkanGerakanBerulang`; `swl2d_stage_play_idle`; `PeriksaPiksel`; `CatatPengecualian`; `CatatKeBerkasNative`; instrumentasi; **`SiapkanEfek()` (napas/kedip/pandangan)**; **urutan `Perbarui()` mengikuti `CubismUpdateOrder`**; **`AturPandang()` beralih ke `CubismTargetPoint`**; **`SimpanBingkai()` + penimpaan pembingkaian lewat lingkungan**; **rantai mip pada `BuatTekstur`** |
| `native/SilverWolf.Live2D/Live2DStage.h` | Deklarasi `swl2d_stage_play_idle`; koreksi komentar IID |
| `native/SilverWolf.Live2D/build-cli.sh` | `-FS`, `-Fd`, `-MAP:` |
| `native/SilverWolf.Live2D/rva-lookup.py` | **Baru** — penerjemah RVA |
| `tools/potret-jendela.py` | **Baru** — pemotret jendela lewat `PrintWindow` (tanpa `Add-Type`) |
| `src/SilverWolf.App/Views/StageView.xaml.cs` | Skala DPI; pemanggilan idle; komentar semantik `Perbesaran`; **pembingkaian ditala ulang: `Perbesaran 1.85 → 0.88`, `GeserX 0 → -0.10`, `JangkarY 0 → 0.10`** |
| `src/SilverWolf.App/Native/Live2DNative.cs` | P/Invoke `swl2d_stage_play_idle`; `MainkanIdle` |
