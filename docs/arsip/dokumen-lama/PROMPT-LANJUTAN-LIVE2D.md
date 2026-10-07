# Prompt lanjutan — menyelesaikan M8 (renderer Live2D native)

> **Cara pakai**: salin seluruh isi berkas ini (mulai dari baris di bawah
> `--- PROMPT MULAI ---`) ke sesi AI berikutnya. Isinya lengkap dan berdiri
> sendiri — AI itu tidak melihat percakapan sebelumnya.
>
> Riwayat lengkap ada di `docs/PROGRES-LIVE2D.md`. Dokumen acuan umum proyek:
> `docs/PROMPT-LANJUTAN.md` dan `docs/RIWAYAT-MIGRASI.md`.

--- PROMPT MULAI ---

# Tugas: selesaikan renderer Live2D native (M8) di AI Vtuber WINUI3

Kamu melanjutkan pekerjaan yang sudah sebagian besar selesai. Model Cubism 2D
Silver Wolf **sudah tampil, mengisi panel, dan bergerak**. Tersisa empat
masalah. Jangan mengulang pekerjaan yang sudah beres — fokus ke bagian yang
masih terbuka di §4.

Akar repo: `C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3`
Riwayat terperinci: **`docs/PROGRES-LIVE2D.md`** — baca ini dulu sebelum
menyentuh apa pun.

---

## 1. Keadaan saat ini (semuanya terverifikasi, jangan diragukan lagi)

Aplikasi C# (`SilverWolf.App`, WinUI 3) memanggil DLL native C++
(`SilverWolf.Live2D.dll`) lewat P/Invoke untuk merender model Live2D Cubism ke
`SwapChainPanel` di XAML.

Bukti terukur dari `crash.log` (angka ini dibaca langsung dari back buffer
swap chain, bukan klaim):

```
live2d: idle grup=isyarat indeks=2 -> 0
[live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=1.85, geser=(0.00,0.00)
[live2d] [swl2d] periksa piksel (bingkai ke-10): 511x661, terisi=270419, kotak=(0,37)-(510,660)
```

- `terisi=270419` dari total 511×661 piksel → model mengisi hampir seluruh panel
- `511×661` = 408×1,25 dan 528×1,25 → skala tampilan 125% sudah ditangani
- gerakan idle berulang aktif

**Yang sudah benar dan tidak boleh diubah tanpa alasan kuat:**

- IID `ISwapChainPanelNative` WinUI 3 = `63AAD0B8-7C24-40FF-85A8-640D944CC325`
  (bukan `F92F19D2-...` milik XAML sistem)
- `CubismFramework::Option` disimpan sebagai variabel **statis** (`g_opsi`),
  karena `StartUp` menyimpan penunjuk, bukan salinan
- `CreateRenderer` dipanggil ulang di `AturUkuranTarget` saat ukuran panel berubah
- `StartFrame`/`EndFrame` mengapit `DrawModel`
- koreksi aspek memilih sisi yang lebih kecil (panel ini potret)
- `matriks.SetHeight(2.0f * perbesaran)` — **bukan** `Scale()`
- ukuran swap chain = DIP × `XamlRoot.RasterizationScale`

---

## 2. Aturan proyek yang WAJIB dipatuhi

1. **Build, lalu JALANKAN.** Build hijau 0 error tidak berarti aplikasi jalan.
   Dua cacat terbesar di proyek ini (XAML `Background` pada `SwapChainPanel`,
   dan viewport 1×1) hanya muncul saat dijalankan.
2. **`MSBuild.exe` DIBLOKIR kebijakan keamanan**, dan `dotnet build` tidak punya
   `VCTargetsPath` (MSB4019) sehingga tidak bisa membangun `.vcxproj`. Bangun
   native lewat `bash native/SilverWolf.Live2D/build-cli.sh Release`.
3. **`dotnet build` tanpa `-p:Platform=x64 -p:SelfContained=true` menghasilkan
   aplikasi framework-dependent yang TIDAK BISA dijalankan.** Selalu pakai
   perintah lengkap di §5.
4. **Jangan pernah menyalin `runtimeconfig.json` atau `deps.json` antar folder
   keluaran.** Pernah terjadi dan merusak aplikasi; build berikutnya tidak
   memperbaikinya karena MSBuild menganggap keluarannya masih baru (perlu
   `-t:Rebuild`).
5. **`CrashLog.Tulis` memakai `File.AppendAllText` — tidak buffered.** Baris
   terakhir di `crash.log` benar-benar langkah terakhir yang dijalankan.
6. **`kill -0 $PID` tidak bisa dipakai** untuk mengecek proses hidup di
   lingkungan ini. Pakai `tasklist //FI "IMAGENAME eq SilverWolf.App.exe"`.
7. Konvensi kode: file ini sudah telanjur memakai identifier **Indonesia**
   (`Panggung`, `Catat`, `Muat`, `Gambar`, `perangkat`, `swapChain`,
   `MainkanGerakanBerulang`). **Ikuti gaya yang sudah ada**, jangan campur
   dengan Inggris di berkas yang sama. Komentar ditulis Indonesia.
8. Jangan menghapus aset apa pun. Subfolder model memakai nama Indonesia
   (`tekstur`, `ekspresi`, `gerakan`) dan diacu literal oleh `.model3.json`.
9. Lingkungan ini punya pengaman **safe-delete** yang menolak hapus permanen
   bila trash gagal — jangan diakali.

---

## 3. Alat diagnosis yang sudah tersedia — pakai ini, jangan bikin baru

| Alat | Cara pakai |
|---|---|
| Penangkap pengecualian + pembuang tumpukan | `SWL2D_DIAG=1 ./SilverWolf.App.exe` → mencatat kode, alamat, modul, RVA, dan 32 alamat kembali |
| Penerjemah RVA → nama fungsi | `python native/SilverWolf.Live2D/rva-lookup.py build/x64/Release/SilverWolf.Live2D.map 0x5E4D1` |
| Pembacaan kembali piksel | otomatis, sekali di bingkai ke-10 → baris `periksa piksel` |
| Log native murni (tanpa .NET) | `SWL2D_NOLOG=1 ./SilverWolf.App.exe` → `swl2d-native.log` |
| Berkas `.map` | dihasilkan otomatis oleh `build-cli.sh` |

**Penting:** jangan aktifkan `SWL2D_DIAG` secara bawaan. Penangkap itu
memanggil balik ke C# dari dalam proses pengecualian dan merusak runtime .NET
(proses mati dengan `Fatal error. Invalid Program: attempted to call a
UnmanagedCallersOnly method from managed code`).

---

## 4. Status tiap pekerjaan

Hanya **§4.2** yang masih terbuka. §4.1, §4.3, dan §4.4 sudah selesai dan
terverifikasi — jangan dikerjakan ulang.

### 4.1 ✅ SELESAI — build Debug mati saat kompilasi shader

**Ini bukan cacat tersendiri.** Ia adalah §1.1 (penunjuk menggantung pada
`CubismFramework::StartUp`) yang sama, hanya terlihat di build Debug. Dengan
`g_opsi` statis, `bash build-cli.sh Debug` selesai dan menampilkan model —
tanpa `/STACK`, tanpa mengubah optimasi sumber SDK.

**Uji A/B yang membuktikannya** (sudah dilakukan, tidak perlu diulang): kembalikan
`Option` menjadi variabel lokal di `swl2d_init` → build Debug mati senyap tepat
setelah kedua `MuatBerkas ... FrameworkShaders/*.fx`; dengan `g_opsi` statis →
selesai.

Hipotesis *stack overflow* yang dulu dipegang **gugur** — tidak ada bukti yang
mendukungnya, dan tidak ada perubahan flag yang diperlukan.

> Pelajaran: build Debug di folder keluaran sempat **basi** (dibangun 19:17,
> sumber diubah 19:26) sehingga tidak mewakili kode terkini. Kalau sebuah
> konfigurasi dilaporkan rusak, **bangun ulang dulu dari sumber terkini**
> sebelum menyimpulkan apa pun.

### 4.2 🔴 Proses mati saat llama.cpp memuat model — INTERMITEN, penyebab belum pasti

**Gejala.** Setelah panggung Live2D aktif dan `MainWindow: runtime siap`
tercatat, llama.cpp mulai memuat GGUF. Beberapa detik kemudian proses mati —
tanpa exception, `run.out` kosong, tanpa entri Event Log. Baris log terakhir
berasal dari llama.

**Ini TIDAK deterministik.** Dari 6 kali jalan yang dipantau (masing-masing
≥45 detik): **5 hidup, 1 mati** di sekitar t=42s. Jangan menganggap satu kali
mati sebagai bukti bahwa ada perubahan yang menyebabkannya.

**Yang sudah disingkirkan — jangan diulang:**

| Uji | Hasil | Artinya |
|---|---|---|
| `VTUBER_STUB=true` | hidup 45 s | llama tidak terlibat |
| `VTUBER_LLM_PROVIDER=local` (`-ngl 0`) | hidup 50 s, llama-server **selesai memuat**, listening di :8788 | llama sendiri sehat; jalur CPU tidak memicu |
| Vulkan (`-ngl 99`) | 5/6 hidup, sampai `processing task` | jalur Vulkan yang dicurigai, tapi jarang |

- **llama-server mati bukan penyebabnya.** Ia proses terpisah
  (`bin/llama/llama-server.exe`, dijalankan `LlamaServerProcess.cs`) — kematiannya
  tidak mungkin menjatuhkan proses kita.
- **Bukan kode kita.** Tidak ada `FailFast` maupun `Environment.Exit` di
  seluruh `src/`.
- **Bukan log yang hilang.** Event Log diperiksa mandiri (`Get-WinEvent`,
  Application + Windows Error Reporting): kosong.
- **Bukan "llama selesai memuat".** Satu kematian yang teramati terjadi di
  ~1 detik, pada tahap pemuatan shader Live2D, sebelum llama mengeluarkan satu
  baris pun.

**Dugaan terkuat: fail-fast di tingkat driver GPU** — rebutan antara perangkat
D3D11 kita dan konteks Vulkan llama-server (`VTUBER_VULKAN_NGL=99`,
`VTUBER_VULKAN_CTX=16384`, KV cache f16), diperparah keluar-masuk perangkat
yang cepat saat aplikasi dijalankan berulang kali dalam waktu singkat.

**Langkah berikutnya, dari termurah:**

1. Aktifkan WER LocalDumps
   (`HKCU\Software\Microsoft\Windows Error Reporting\LocalDumps`) supaya ada
   dump untuk diperiksa, lalu ulangi sampai kena.
2. Uji konfirmasi: turunkan `VTUBER_VULKAN_NGL` (mis. 20) atau
   `VTUBER_VULKAN_CTX` (mis. 4096). Kalau kematian hilang, dugaan GPU menguat.
3. Kalau terkonfirmasi: serialkan — tunda inisialisasi panggung Live2D sampai
   llama-server melaporkan `listening`, atau tunda pemanasan llama sampai
   panggung Live2D selesai dibuat.

**Definisi selesai:** 10 kali jalan berturut-turut bertahan ≥2 menit dengan
Live2D aktif **dan** llama memuat model, tanpa entri exception di `crash.log`.

### 4.3 ✅ SELESAI — napas, kedip, dan ikut kursor

Sudah terpasang di `ModelPanggung::SiapkanEfek()` dengan `CubismBreath`,
`CubismEyeBlink`, dan `CubismLook`. **`CubismUpdateScheduler` sengaja tidak
dipakai** — urutannya sedikit dan tetap, jadi `UpdateParameters()` dipanggil
langsung dengan urutan mengikuti `CubismUpdateOrder`
(`Framework/src/Motion/ICubismUpdater.hpp`):

```
kedip(200) -> ekspresi(300) -> pandangan(400) -> napas(500) -> fisika(600)
```

`CubismLookUpdater` juga dilewati (ia menuntut `CubismTargetPoint` yang diurus
scheduler); peredamannya diurus sendiri lewat `CubismTargetPoint` di
`Perbarui()`, dengan sasaran diisi `AturPandang()`.

Bukti log:

```
[live2d] [swl2d] efek: kedip aktif, 2 parameter
[live2d] [swl2d] efek: napas aktif
[live2d] [swl2d] efek: pandangan aktif (dengan peredaman CubismTargetPoint)
```

Peredaman terbukti terukur (sasaran `x=1.0`, ~9 bingkai untuk konvergen):
`0.0000 → 0.1111 → 0.2444 → 0.5111 → 0.9587 → 1.0002`.

**Catatan penting:** `_model->SaveParameters()` sekarang dipanggil **sebelum**
efek hidup, bukan sesudah (sama dengan `LAppModel::Update()`), supaya nilai
efek tidak menumpuk setiap bingkai.

**Yang belum:** LipSync. Grup `LipSync` (`ParamMouthOpenY`) sudah ada di
`model3.json` dan TTS sudah jalan, tetapi mulut belum digerakkan.

### 4.4 ✅ SELESAI — penalaan bingkai visual

`StageView.xaml.cs` sekarang:

```csharp
private const float Perbesaran = 0.88f;   // tinggi tampil 0.88x tinggi panel
private const float GeserX      = -0.10f; // geser kiri; seni model asimetris
private const float JangkarY    = 0.10f;  // angkat sedikit dari dasar panel
```

**Nilai lama 1.85 salah pindah dari web.** Di web (`PIXI`) `VITE_AVATAR_ZOOM`
mengalikan skala "pas panel" lalu digeser jangkar 0.92; di sini
`SetHeight(2.0 * perbesaran)` membuat model setinggi itu secara langsung —
akibatnya pada 1.85 hanya kepala dan bahu yang terlihat. `GeserX = -0.10` perlu
karena sayap mekanik model jauh lebih panjang ke kanan.

**Menala tanpa membangun ulang** (penimpaan diagnostik, hanya berlaku bila
diset):

```bash
SWL2D_PERBESARAN=0.95 SWL2D_GESER_X=-0.05 SWL2D_JANGKAR_Y=0.10 ./SilverWolf.App.exe
```

**Melihat hasilnya tanpa memotret layar** — jendela aplikasi sering tertutup
jendela lain dan `SetForegroundWindow` dari proses latar ditolak Windows, jadi
minta renderer sendiri yang membuang bingkainya:

```bash
SWL2D_TANGKAP="C:/.../tools/bingkai.bmp" ./SilverWolf.App.exe
# lalu ubah BMP 32-bit itu ke PNG (Pillow) dan lihat
```

### 4.5 🟢 Bersihkan instrumentasi

Sebelum rilis, bersihkan atau jadikan opt-in: probe `D3DCompile` (probe 1 dan
probe 2) di `swl2d_stage_create`, log per pemanggilan di `MuatBerkas`, dan
pemanggilan `PeriksaPiksel`. Semuanya berfungsi dan tidak mengganggu, tetapi
tidak layak ikut ke produksi.

---

## 5. Perintah (sudah terbukti jalan)

```bash
cd "C:/Users/Daffa/Desktop/AI Vtuber Project/AI Vtuber WINUI3"

# 1. Bangun native — pakai Release sampai §4.1 selesai
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

Keluaran yang diharapkan (angka boleh berbeda, yang penting urutan besarnya):

```
live2d: panggung 1 aktif pada percobaan 1
live2d: idle grup=isyarat indeks=2 -> 0
[live2d] [swl2d] kanvas model = 0.6 x 0.4, perbesaran=1.85, geser=(0.00,0.00)
[live2d] [swl2d] periksa piksel (bingkai ke-10): 511x661, terisi=270419, kotak=(0,37)-(510,660)
```

---

## 6. Definisi selesai untuk keseluruhan tugas

1. ✅ Build **Debug** maupun **Release** sama-sama menampilkan model
2. ⬜ Aplikasi bertahan hidup ≥2 menit dengan Live2D aktif dan llama memuat
   model — **intermiten**, lihat §4.2
3. ✅ Model bernapas, berkedip, dan mengikuti kursor dengan halus
4. ✅ `crash.log` bersih dari exception tak tertangani di semua jalur di atas
5. ⬜ Instrumentasi penyidikan sudah dibersihkan atau dijadikan opt-in
6. ⬜ LipSync (mulut mengikuti suara TTS) — belum ada sama sekali

**Laporkan** dengan format: (a) apa yang diubah — berkas + inti perubahan,
(b) bukti terukur sebelum/sesudah (kutip baris log relevan), (c) apa yang
masih belum selesai beserta langkah berikutnya yang konkret. Sebutkan secara
eksplisit bila ada klaim yang belum kamu verifikasi sendiri.

> **Pelajaran yang mahal di sesi ini:** build Debug yang dilaporkan rusak
> ternyata **basi** (dibangun sebelum sumber terakhir diubah), dan kesimpulan
> "stack overflow" ikut terbawa berbulan-bulan sesudahnya. **Bangun ulang dari
> sumber terkini sebelum menyimpulkan apa pun tentang sebuah konfigurasi.**
> Kesalahan yang sama muncul lagi di §4.2: satu kali mati bukan bukti — butuh
> beberapa kali jalan sebelum menyalahkan sebuah perubahan.

--- PROMPT SELESAI ---
