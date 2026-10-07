# Langkah migrasi per proyek + status blocker

Per 2026-10-06. M0–M2 dan M4–M7 selesai; M3 dan seterusnya yang menyentuh
C++/WinRT masih terblokir prasyarat.

---

## SilverWolf.Core — **Selesai** (M4 + M5)

Bebas WinRT (`net8.0` polos) supaya seluruh domain bisa diuji dari mesin apa pun.

Langkah yang sudah dikerjakan:

1. `Configuration/EnvFile.cs` — parser `.env` (aturan potong-komentar yang tidak lazim dipertahankan).
2. `Configuration/EnvSource.cs` — prioritas lingkungan-proses di atas berkas; konversi angka/boolean/daftar + pengumpul peringatan.
3. `Configuration/AppConfig.cs` — `bacaKonfig()` + `temukanPersona()`.
4. `Text/EmotionParser.cs` — pengganti `@aituber-onair/voice`.
5. `Text/SentenceSplitter.cs` — pengganti `kalimat.js`, termasuk aturan "bukan akhir kalimat" untuk desimal dan nomor versi.
6. `Text/TagSkipper.cs` — pengganti `ucapan.js`.
7. `Domain/{EmotionTags,Mood,PersonaComposer,CharacterVault,ChatHistory,ProactiveDirector,TieredMemoryEngine}.cs`.
8. `Domain/Kizuna/*` — port mesin ikatan: `KizunaConfig`, `BondEvaluator`, `BondDynamics`, `BondContextBuilder`, `KizunaEngine`, `KizunaStorage`, model persistensi.

Langkah yang belum: tidak ada. Catatan — `KizunaEngine` sengaja **tidak**
memasang timer pembersihan internal; host yang menjadwalkan `PerformCleanup()`
(24 jam) supaya logika bisa diuji.

## SilverWolf.Services — **Selesai** (M6 + M7)

`net8.0-windows10.0.19041.0` — lapisan yang menyentuh Windows.

Langkah yang sudah dikerjakan:

1. `Configuration/AppPaths.cs` — penemu akar repo, aset, memori, kizuna.
2. `Inference/LlmModels.cs` — kontrak `ILlmProvider`.
3. `Inference/OpenAiCompatibleProvider.cs` — SSE llama-server + Ollama, percobaan ulang 45 detik (1500 ms bila gagal koneksi, 1200 ms bila HTTP 503).
4. `Inference/StubProvider.cs` — jalur `VTUBER_STUB`.
5. `Llama/ModelLocator.cs` — pemilihan model GGUF + pencarian `llama-server.exe`.
6. `Llama/LlamaServerProcess.cs` — argumen Vulkan, `WorkingDirectory` = akar (butuh DLL Vulkan di sebelahnya), polling `/health` 120 × 250 ms.
7. `Agent/AgentService.cs` — susun prompt → alirkan → **baru** simpan memori/kizuna/vault.
8. `Backend/CompanionBackend.cs` — pengganti rute HTTP dengan kontrak setara (status, `{ error }`, snapshot ikatan).
9. `Bootstrap/CompanionRuntime.cs` — pengganti `main.js`.

Langkah yang belum: tidak ada untuk M6–M7.

## SilverWolf.App — **Belum** (M9, M10, M12, M13)

1. `ViewModels/CompanionViewModel.cs` — port `store.js` (satu viewModel).
2. `Views/{StageView,ConsoleView}.xaml` — port `Panggung.jsx` + `Konsol.jsx`.
3. `Services/AssetLocator.cs` — pengganti penyaji berkas statis.
4. Polling health 8000/2000 ms; pekerja proaktif interval 5000 ms dengan ambang hening 65.000 ms.
5. Tema, Mica (feature-detect), font.
6. Tray, hotkey global, single-instance, close-to-tray.

## SilverWolf.TtsWorker — **Belum** (M11)

Exe konsol **tanpa** WindowsAppSDK; satu-satunya pemilik `onnxruntime.dll`.
Bicara lewat stdin/stdout JSON.

## native/SilverWolf.Live2D — **Terblokir** (M3, M8)

## native/SilverWolf.Phonemizer — **Terblokir** (M11)

---

## Yang masih memblokir

| # | Prasyarat | Keadaan saat ini | Yang tertahan |
|---|---|---|---|
| 1 | **Windows SDK** | ✅ **SELESAI.** `Include\10.0.28000.0\` (um, winrt, shared, ucrt, cppwinrt), `Lib\10.0.28000.0\{um,ucrt}\x64\`, `bin\10.0.28000.0\`. | — |
| 2 | **Toolset MSVC** | ✅ **SELESAI.** `14.51.36231\` lengkap — `include\vcruntime.h`, `lib\x64\`, `cl.exe` 19.51.36260 berjalan. | — |
| 3 | **Cubism SDK for Native** | ✅ **SELESAI.** `CubismSdkForNative-5-r.5.zip` diunduh, diekstrak ke `C:\sdk\CubismSdkForNative-5-r.5`, `CUBISM_SDK_DIR` diset (scope User). Probe MSBuild mengonfirmasi `CubismSdkAvailable = true` dan `Live2DCubismCore_MT.lib` benar-benar ada. | — |
| 4 | **Data golden kizuna** | ✅ **SELESAI** (2026-10-07). Diambil dari server Node asli dengan `VTUBER_STUB=true`, tersimpan di `tests/…/TestData/golden/kizuna-setelah-3-interaksi.json`. Port C# tervalidasi persis (4 / 6,8 / 2,89 poin). | — |
| 4b | **Data golden phoneme_ids** | Belum — butuh `piper_phonemize.wasm` berjalan di browser, tidak bisa dari CLI. | Uji paritas M11 |

### Cara mengunduh masing-masing

**#1 + #2 — Windows SDK + MSVC, lewat Visual Studio Installer**

Keadaan terverifikasi 2026-10-06: VS **2026 Community** terpasang,
`VC\Tools\MSVC\14.51.36231\` hanya berisi `Auxiliary`, `bin`, `lib` (tanpa
`include`), dan `C:\Program Files (x86)\Windows Kits\10\` hanya berisi
`UnionMetadata`. Keduanya hilang karena workload C++ belum dipilih.

Langkah:
1. Buka **Visual Studio Installer**. Dari dalam VS: menu **Tools → Get Tools
   and Features…**. Atau dari Start Menu cari "Visual Studio Installer", lalu
   klik **Modify** di sebelah *Visual Studio Community 2026*.
2. Tab **Workloads**, centang ☑ **Desktop development with C++**.
3. Panel **Installation details** di kanan, bagian *Optional*, pastikan
   tercentang:
   - ☑ **Windows 11 SDK (10.0.26100.0)** atau yang lebih baru
   - ☑ **MSVC … x64/x86 build tools** (nomor versi mengikuti VS 2026)
4. Klik **Modify**. Unduhannya beberapa GB.

Setelah selesai, verifikasi dua jalur ini ada:
```
C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\include\
```

**#3 — Cubism SDK for Native, dari situs Live2D**

- Halaman unduh: **https://www.live2d.com/en/download/cubism-sdk/**
- Pilih **Cubism SDK for Native**. Per 2026-10-06 rilis terbaru adalah
  **Cubism 5 SDK for Native R5** (diumumkan 02/04/2026).
- Butuh akun Live2D (gratis) dan menyetujui *Live2D Proprietary Software
  License Agreement*.
- **Cubism Core tidak ada di GitHub.** Dokumen resmi Live2D menyatakan:
  "In accordance with the Live2D Proprietary Software License Agreement, Cubism
  Core is not published on GitHub." Yang ada di `github.com/Live2D` hanya
  `CubismNativeFramework` dan `CubismNativeSamples`. Karena build kita butuh
  `Core\include\Live2DCubismCore.hpp` dan `Core\lib\…\Live2DCubismCore_MT.lib`,
  unduhan dari situs resmi **wajib**.
- Ekstrak ke misalnya `D:\sdk\CubismSdkForNative-5-r.5`, lalu:
  ```
  setx CUBISM_SDK_DIR "D:\sdk\CubismSdkForNative-5-r.5"
  ```
  Tutup dan buka lagi terminal/VS agar variabelnya terbaca.
- Jangan di-commit; `native/third_party/` sudah diabaikan Git.

✅ **Sudah dikerjakan 2026-10-06.** Paket `CubismSdkForNative-5-r.5` diekstrak ke
`C:\sdk\CubismSdkForNative-5-r.5` dan `build/CubismSdk.props` sudah disesuaikan
dengan struktur Cubism 5. Tiga perbedaan dari Cubism 4 yang ditemukan:

| | Cubism 4 | Cubism 5 (terverifikasi) |
|---|---|---|
| Header Core | `Core\include\Live2DCubismCore.hpp` | `Core\include\Live2DCubismCore.h` |
| Folder arsitektur | `Core\lib\windows\x64\<vcver>\` | `Core\lib\windows\x86_64\<vcver>\` |
| Renderer D3D11 | `Framework\src\Rendering\D3D11\` | `Samples\D3D11\` |

`build/CubismSdk.props` sekarang mendeteksi **kedua format**, jadi tidak
bergantung pada versi SDK yang terpasang. Hasil probe MSBuild:
`CubismSdkAvailable = true`, `CubismArchDir = x86_64`, vcver `143`
(cocok dengan toolset MSVC 14.51 di mesin ini).

**#4 — `Microsoft.ML.OnnxRuntime`: sebenarnya ini paket NuGet**

Koreksi: pada daftar di atas OnnxRuntime pernah dikelompokkan sebagai "bukan
NuGet". Itu keliru. Ia **memang paket NuGet**
(`nuget.org/packages/Microsoft.ML.OnnxRuntime`), yang membuatnya istimewa
justru karena **tidak boleh** masuk ke App/Services/Core — ia bertabrakan
dengan `Microsoft.Windows.AI.MachineLearning` yang ikut tertarik
`Microsoft.WindowsAppSDK` (lihat `02-dependensi.md` §4).

Jadi: tidak perlu diunduh sekarang. Nanti pada M11, tambahkan ke proyek
`SilverWolf.TtsWorker` yang berdiri sendiri (tanpa WindowsAppSDK):
```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.22.0" />
```

Catatan: build C# **tidak** terpengaruh ketiganya — ia memakai
`Microsoft.Windows.SDK.NET.Ref` dari NuGet. Yang tidak bisa dibangun hanya
C++/WinRT.

---

## Status uji

```
dotnet build SilverWolf.sln -p:Platform=x64 -c Debug   → 0 error, 0 warning
dotnet test tests/SilverWolf.Core.Tests/...            → 105 lulus, 0 gagal
```

Uji saat ini memakai **hitungan tangan dari rumus asli**, bukan data golden
(lihat blocker #4). Angka yang dikunci antara lain:

- pesan pertama → +4 poin; pesan kedua di hari yang sama → +3,4 (pengali pengulangan 0,85);
- hari berikutnya → +4,12 (streak 2 → bonus konsistensi 1,03);
- sentuhan pertama → +8;
- kehangatan setelah 14 hari → 0,675 (lantai 0,35 + 0,65 × 2⁻¹); atmosfer `neutral`;
- `Mood.Perbarui(Awal, "senyum")` → valensi 0,645; energi 0,8; afinitas 0,92;
- tag yang tidak dikenal memberi delta **0**, bukan delta `netral` (0,05).
