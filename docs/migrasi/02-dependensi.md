# Dependensi pihak ketiga: diganti, dihapus, dan batasan platform Windows

## 1. Dependensi runtime aplikasi lama

| Paket npm | Peran | Nasib di WinUI 3 | Padanan |
|---|---|---|---|
| `@aituber-onair/kizuna` | Mesin ikatan (poin, tahap, kehangatan, kontinuitas) | **Diganti** — reimplementasi penuh | `SilverWolf.Core/Domain/Kizuna/*` |
| `@aituber-onair/voice` (`EmotionParser`) | Parser tag emosi `[senyum]` | **Diganti** | `SilverWolf.Core/Text/EmotionParser.cs` |
| `@aituber-onair/core` (`MemoryManager`) | Memori bertingkat | **Diganti** | `SilverWolf.Core/Domain/TieredMemoryEngine.cs` |
| `@aituber-onair/chat` | Klien chat | **Dihapus** — tidak dipakai jalur server | — |
| `@aituber-onair/kizuna` → `ExternalStorageProvider` | Simpan JSON ikatan | **Diganti** | `Core/Domain/Kizuna/KizunaStorage.cs` (`FileKizunaStorage`) |
| `onnxruntime-web` | Piper + RVC di browser (WASM) | **Dihapus dari aplikasi** | `Microsoft.ML.OnnxRuntime` **hanya** di `SilverWolf.TtsWorker` (M11) |
| `onnxruntime-node` | Piper/RVC di Node | **Dihapus** | sama seperti di atas |
| `@mintplex-labs/piper-tts-web`, `piper-tts-web` | Piper TTS WASM | **Diganti** | Piper ONNX di worker terpisah |
| `rvc-onnx-web` | Konversi suara RVC | **Dihapus** — fitur sudah mati (ContentVec hilang) | — |
| `@huggingface/transformers` | — | **Dihapus** — tidak dipakai | — |
| `pixi.js`, `pixi-live2d-display` | Render Live2D | **Diganti** | Cubism Native + D3D11 (M3/M8) |
| `vue`, `pinia` | UI + state | **Diganti** | WinUI 3 XAML + `CommunityToolkit.Mvvm` 8.4.0 |
| `electron` | Shell desktop | **Diganti** | WinUI 3 (unpackaged) |
| `zod` | Validasi | **Dihapus** — tidak dipakai di jalur server | validasi manual di `ChatHistory` / `CompanionBackend` |

## 2. Dependensi build aplikasi lama

`esbuild`, `vite`, `electron-vite`, `@vitejs/plugin-vue*`, `turbo`, `unocss`,
`vite-plugin-static-copy`, `electron-builder`, `rimraf` — **semuanya dihapus**.
`uno.config.js` adalah berkas konfigurasi mati (tidak pernah di-wire ke Vite),
jadi tidak ada yang hilang.

## 3. Paket NuGet yang dipakai sekarang

| Paket | Versi | Proyek | Catatan |
|---|---|---|---|
| `Microsoft.WindowsAppSDK` | 2.5.1 | App | Sumber konflik `onnxruntime.dll` (lihat §4) |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.28000.2705 | App | |
| `CommunityToolkit.Mvvm` | 8.4.0 | App | `ObservableObject`, `[ObservableProperty]`, `AsyncRelayCommand`, `WeakReferenceMessenger` |
| `NAudio` | 2.2.1 | Services | Playback + EQ pengganti Web Audio API |
| `xunit` / `xunit.runner.visualstudio` / `Microsoft.NET.Test.Sdk` / `coverlet.collector` | 2.9.2 / 2.8.2 / 17.12.0 / 6.0.2 | Tests | |

**Belum ditambahkan (menunggu M11):** `Microsoft.ML.OnnxRuntime` — hanya di
`SilverWolf.TtsWorker`, tidak pernah di App atau Services.

---

## 4. Mengapa ONNX Runtime tidak boleh masuk ke proyek aplikasi

Dua paket Microsoft yang sama-sama sah mengklaim identitas yang sama:

| | `Microsoft.ML.OnnxRuntime` | `Microsoft.Windows.AI.MachineLearning` |
|---|---|---|
| native | `onnxruntime.dll` | `onnxruntime.dll` |
| `PathInPackage` | `runtimes/win-x64/native/onnxruntime.dll` | identik |
| managed | `Microsoft.ML.OnnxRuntime.dll` | `Microsoft.ML.OnnxRuntime.dll` |

Yang kedua ikut tertarik otomatis oleh
`Microsoft.WindowsAppSDK` → `Microsoft.WindowsAppSDK.ML`, jadi **selalu ada di
setiap aplikasi WinUI 3**. MSIX mengelompokkan payload berdasarkan destinasi,
bukan sumber, sehingga:

```
error APPX1101: Payload contains two or more files with the same destination
path 'onnxruntime.dll'.
```

Mengganti nama berkas tidak menyelesaikannya — `PathInPackage`-nya tetap sama.
Memaksa keduanya hidup berarti dua ONNX Runtime berbeda versi dalam satu proses
dengan nama assembly yang sama; kegagalannya muncul sebagai crash P/Invoke di
runtime, bukan error build.

**Konsekuensi arsitektur:** `SilverWolf.Services` tidak mereferensikan ONNX
Runtime sama sekali. Piper TTS pindah ke `SilverWolf.TtsWorker` (M11).

---

## 5. Batasan platform Windows

| Batasan | Nilai / dampak |
|---|---|
| TargetFramework | `net8.0-windows10.0.19041.0` |
| `TargetPlatformMinVersion` | `10.0.17763.0` (RS5) |
| **Arsitektur rilis** | **x64 saja.** Build llama.cpp Vulkan di `AI Vtuber Web/bin/llama/` khusus x64; ARM64 hanya bisa membangun UI, tidak bisa menjalankan LLM lokal. |
| `PublishTrimmed` | **Harus `False`.** Trimming + XAML (`XamlTypeInfo.g.cs`, `IXamlMetadataProvider`) + aktivasi WinRT adalah sumber kerusakan yang sudah dikenal. |
| `PublishSingleFile` | `False` — tidak didukung untuk WinUI. |
| Packaging | **Unpackaged** (`WindowsPackageType=None`) + **WASDK self-contained**. Aset ~7 GB tidak mungkin masuk MSIX, dan folder instalasi MSIX bersifat read-only padahal `llama-server.exe` butuh CWD yang bisa ditulis dan harus menemukan ~20 DLL Vulkan di sebelahnya. |
| Mica / backdrop | Mica butuh Windows 11 22000 — harus di-feature-detect, bukan diasumsikan ada. |
| Cubism Core | Dikirim sebagai **static library** (`Live2DCubismCore_MT.lib`), berlisensi Live2D. Tidak boleh di-commit, tidak boleh didistribusikan sebagai berkas lepas. Ditunjuk lewat `CUBISM_SDK_DIR`. |
| Berkas model Live2D | Tidak boleh didistribusikan ulang. Dimuat saat runtime dari folder aset pengguna. |
| Subfolder model | `tekstur`, `ekspresi`, `gerakan` diacu literal oleh `.model3.json` — jangan diterjemahkan atau diganti nama. |
| Penyimpanan lokal | `ApplicationData.Current.LocalSettings` untuk preferensi; `silver_wolf_memory/` untuk memori karakter; `silver_wolf_memory/kizuna/` untuk data ikatan. |

---

## 6. Catatan kompatibilitas berkas data

Data yang sudah ada di mesin pengguna **harus tetap terbaca**:

| Berkas | Ditulis oleh | Penulis baru | Kompatibel? |
|---|---|---|---|
| `silver_wolf_memory/Fakta.md`, `Mood.md`, `Riwayat/YYYY-MM-DD.md` | `CharacterVault` (Node) | `Core/Domain/CharacterVault.cs` | Ya — format front-matter dijaga identik |
| `silver_wolf_memory/kizuna/silverwolf_kizuna_v1.json` | `ExternalStorageProvider` | `Core/Domain/Kizuna/KizunaStorage.cs` | Ya — nama berkas = `safeKey + ".json"`, kunci camelCase, indentasi 2 spasi, `Date` ditulis sebagai ISO 8601 |

Satu perbedaan yang disengaja: pustaka asli menulis properti bersyarat
(`...(x && { x })`), port C# memakai `JsonIgnoreCondition.WhenWritingNull`.
Hasilnya setara untuk semua data yang pernah ditulis aplikasi lama.
