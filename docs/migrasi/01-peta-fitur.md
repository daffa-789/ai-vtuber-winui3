# M4 — Peta struktur, halaman, dan fitur (Electron/Vue → WinUI 3)

Sumber: `../AI Vtuber Web/` (Electron + Vue 3 JSX + server Node ESM).
Tujuan: `AI Vtuber WINUI3/` (WinUI 3, C#/.NET, XAML, MVVM).

Dokumen ini adalah hasil pemetaan M4. Ia mendahului kode M5–M7 dan menjadi
acuan untuk M9 (UI) dan M13 (integrasi end-to-end).

---

## 1. Bentuk aplikasi lama

```
apps/server-node/src/     backend HTTP  ← sudah dipindah ke SilverWolf.Services
  main.js                 titik masuk, pilih provider, jalankan server
  server.js               rute + penyaji berkas statis
  agent.js                susun prompt, alirkan balasan, simpan memori
  character.js            persona, mood, CharacterVault (Markdown)
  config.js               parser .env + AppConfig
  inference.js            OpenAI-compatible SSE + stub
  llama.js                proses llama-server + pemilihan model GGUF
  kizuna.js               pembungkus @aituber-onair/kizuna
  tiered-memory.js        memori bertingkat
  proactive.js            direktur proaktif gaya Neuro-sama

packages/stage-ui/src/store.js        satu store Pinia  ← jadi CompanionViewModel
packages/stage-ui-live2d/src/         renderer PixiJS + Cubism Web
packages/pipelines-audio/src/         Piper/RVC WASM, antrean suara, pemotong kalimat
apps/stage-tamagotchi/                main/preload/renderer Electron
```

**Fakta penting:** tidak ada IPC Electron yang hidup — preload script mati.
Renderer bicara HTTP ke server Node. Karena itu memindahkan backend ke C#
tinggal mengganti lapisan transport, tanpa menyentuh logika bisnis.

---

## 2. Layar: satu layar, dua panel

Tidak ada router, tidak ada multi-halaman, tidak ada halaman pengaturan.

| Panel | Komponen lama | Padanan WinUI (M9) |
|---|---|---|
| Kiri — **Panggung** | `komponen/Panggung.jsx` (kanvas + HUD ekspresi + tombol mirror) | `Views/StageView.xaml` + `SwapChainPanel` (Cubism native) |
| Kanan — **Konsol** | `komponen/Konsol.jsx` | `Views/ConsoleView.xaml` |

Isi Konsol, berurutan:
1. Header: tombol **NEURO-SAMA ON/OFF**, tombol status **VULKAN ONLINE / MEMUAT VULKAN… / OFFLINE**
2. Kartu HUD Kizuna: `Lv.{level} {stageName} — {stageLabel}`, tombol 🌸 Elus Kepala, tombol ⚡ Pancing Obrolan, baris XP `{points}/{nextPoints} XP ({progress}%)`, bar kemajuan, Kehangatan %, Atmosfer (tren), Sikap (tone)
3. Telemetri: GPU (model), MEM (memori), TTS
4. `DaftarPesan` — riwayat
5. `Komposer` — kotak input + tombol kirim + toggle suara

Ukuran jendela sudah dipetakan di M2: 1180×760, minimum 780×560.

---

## 3. Keadaan (state)

Satu store Pinia `useCompanionStore` → **satu `CompanionViewModel`**.

| Keadaan lama | Padanan | Catatan |
|---|---|---|
| `messages[]` | `ObservableCollection<ChatBubble>` | `{ id, role, content, pending, error }` |
| `health` / `healthError` | `HealthSnapshot` + `string HealthError` | dari `CompanionBackend.GetHealthAsync` |
| `sending` | `bool IsSending` | |
| `expression` | `string Expression` | nilai tag Indonesia apa adanya |
| `autonomousMode` | `bool AutonomousMode` | default `true` |
| `lastUserActivity` | `DateTimeOffset LastUserActivity` | dasar hitung hening proaktif |
| `kizuna` | `BondSnapshot` | |
| komputasi `ready`/`loading` | `bool IsReady` / `bool IsLoading` | dari `health.ok` / `health.loading` |
| `localStorage['silverwolf_mirror_track']` | **`Configuration/UiSettings.cs`** (JSON di `LocalApplicationData\SilverWolf\`) | **bukan** `ApplicationData.Current.LocalSettings` — tidak berlaku untuk aplikasi unpackaged; lihat §5 baris 6 |

---

## 4. Endpoint → pemanggilan yang setara

| Endpoint lama | Bentuk respons | Padanan C# (M7) |
|---|---|---|
| `GET /api/health` | JSON | `CompanionBackend.GetHealthAsync(ct)` |
| `POST /api/chat` | `text/plain` mengalir | `CompanionBackend.ChatAsync(riwayat, ct)` |
| `GET /api/kizuna` | JSON `{ ok, kizuna }` | `CompanionBackend.GetKizuna()` |
| `POST /api/kizuna/touch` | mengalir + header `x-kizuna` | `CompanionBackend.TouchAsync(ct)` |
| `POST /api/autonomous/proactive` | mengalir + header `x-kizuna` | `CompanionBackend.ProactiveAsync(idle, ct)` |
| `GET /assets/*`, `/models/*`, `/live2dcubismcore.min.js`, `/onnx/*`, `/piper/*` | berkas statis | **Dihapus.** Aplikasi desktop membaca berkas langsung dari disk; tidak perlu server statis. |

Perilaku yang harus dijaga:

- **`/api/chat` tidak pernah mengirim header `x-kizuna`.** Hanya touch dan
  proactive yang mengirim. Karena itu `ChatAsync` mengembalikan `Kizuna = null`,
  dan UI harus menyegarkan snapshot setelah aliran selesai — persis seperti
  `store.send()` yang memanggil `fetchKizuna()` di akhir.
- Header `x-kizuna` pada touch diambil **sebelum** `recordTouch()` berjalan
  (generator async belum dieksekusi saat header disusun). `TouchAsync`
  mengambil snapshot sebelum mengalirkan, jadi nilainya sama.
- Polling health: 8000 ms bila siap, 2000 ms bila belum.
- Pekerja proaktif: interval 5000 ms; picu bila `!autonomousMode || sending ||
  audioBusy` dilewati dan hening > 65.000 ms; `idle` yang dikirim =
  `max(10, round((now - lastUserActivity)/1000))`.

---

## 5. Fitur yang tidak bisa dipindah 1:1, dan alternatifnya

| # | Fitur lama | Kenapa tidak bisa 1:1 | Alternatif |
|---|---|---|---|
| 1 | **Live2D** — Cubism Core **Web/WASM** (`live2dcubismcore.min.js`) + PixiJS + `pixi-live2d-display` | Yang tersedia adalah artefak WASM, bukan Cubism Core untuk Native (static lib, berlisensi). Tidak ada WebView2 sesuai keputusan yang dikunci. | **Cubism SDK for Native** dibungkus komponen C++/WinRT `SilverWolf.Live2D`, render D3D11 ke `SwapChainPanel` (M3 + M8). Model dimuat saat runtime dari folder aset pengguna — tidak didistribusikan ulang. |
| 2 | **Piper TTS** lewat `onnxruntime-web` (WASM) | Menambah `Microsoft.ML.OnnxRuntime` ke proyek aplikasi memicu `APPX1101` — dua paket Microsoft mengklaim `onnxruntime.dll` dengan `PathInPackage` identik. | **Exe worker terpisah** `SilverWolf.TtsWorker` tanpa WindowsAppSDK (M11). Bonus: sintesis tidak berebut UI thread maupun perangkat D3D11 milik Cubism. |
| 3 | **Phonemizer `piper_phonemize.wasm`** | WASM; padanan native-nya butuh espeak-ng + toolset C++ | `native/SilverWolf.Phonemizer` (C++ DLL) — **terblokir** sampai MSVC + Windows SDK lengkap |
| 4 | **RVC** (`rvc-onnx-web`, ContentVec `vec-768-layer-12.onnx`) | Sudah mati di aplikasi lama: berkas ContentVec hilang. Suara hari ini = Piper murni @1.15×. | **Dihapus**, bukan dipindah. Kalau dihidupkan lagi nanti, masuk ke worker yang sama dengan Piper. |
| 5 | **Web Audio API** (`AudioContext`, `playbackRate 1.15`, lip-sync) | API browser | **NAudio 2.2.1** — `BufferedWaveProvider` + `BiQuadFilter` untuk EQ |
| 6 | `localStorage['silverwolf_mirror_track']` | API browser | **Bukan** `ApplicationData.Current.LocalSettings` — aplikasi ini **unpackaged**, dan API itu melempar `InvalidOperationException` ("Operation is not valid due to the current state of the object") tanpa identitas paket. Terverifikasi 2026-10-07 dari `crash.log` (sumber `BacaMirror`). Dipakai `Configuration/UiSettings.cs` → berkas JSON di `LocalApplicationData\SilverWolf\ui-settings.json` |
| 7 | Server statis Node (`/assets`, `/models`, `/piper`) | Tidak ada lagi proses HTTP | Baca berkas langsung; jalur lewat `Services/Configuration/AppPaths.cs` |
| 8 | Electron tray / hotkey global / mode "pet" | Tidak ada di aplikasi lama; baru direncanakan | M12 (`AppPaths`, tray, `RegisterHotKey`) |
| 9 | `uno.config.js` | Konfigurasi mati — tidak pernah di-wire ke Vite | Dibuang |
| 10 | `@huggingface/transformers`, `zod` | Tidak dipakai di jalur server | Dibuang |

---

## 6. Yang sudah dipindah pada M4–M7

| Area lama | Lokasi baru | Status |
|---|---|---|
| `config.js` | `Core/Configuration/{EnvFile,EnvSource,AppConfig}.cs` | Selesai |
| `character.js` — mood, persona, vault | `Core/Domain/{Mood,PersonaComposer,CharacterVault}.cs` | Selesai |
| `EmotionParser` (@aituber-onair/voice) | `Core/Text/EmotionParser.cs` | Selesai |
| `kalimat.js` | `Core/Text/SentenceSplitter.cs` | Selesai |
| `ucapan.js` (`lewatiTag`) | `Core/Text/TagSkipper.cs` | Selesai |
| `server.js` (`rapikanRiwayat`) | `Core/Domain/ChatHistory.cs` | Selesai |
| `kizuna.js` + @aituber-onair/kizuna | `Core/Domain/Kizuna/*` | Selesai |
| `tiered-memory.js` + MemoryManager | `Core/Domain/TieredMemoryEngine.cs` | Selesai |
| `proactive.js` | `Core/Domain/ProactiveDirector.cs` | Selesai |
| `inference.js` | `Services/Inference/{OpenAiCompatibleProvider,StubProvider}.cs` | Selesai |
| `llama.js` | `Services/Llama/{ModelLocator,LlamaServerProcess}.cs` | Selesai |
| `agent.js` | `Services/Agent/AgentService.cs` | Selesai |
| `server.js` (rute) + `main.js` (perakitan) | `Services/Backend/CompanionBackend.cs`, `Services/Bootstrap/CompanionRuntime.cs` | Selesai |
| `store.js` | **belum** — `App/ViewModels/CompanionViewModel.cs` | M9 |
| `App.jsx` + 4 komponen | **belum** — `App/Views/*` | M9 |
| Panggung Live2D | **belum** | M8 (terblokir) |
| `antrean.js`, `browser-piper.js`, `wav.js` | **belum** | M11 |
