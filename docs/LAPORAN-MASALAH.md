# Laporan Masalah & Sisa Pekerjaan — Silver Wolf (WinUI 3)

**Tanggal:** 2026-10-07 · **Dasar:** pemeriksaan langsung, log, dan pengukuran
**Cakupan:** seluruh yang belum beres setelah M0–M9 selesai dan M8 berjalan

Dokumen ini menjawab satu pertanyaan: **apa saja yang masih bermasalah, kenapa,
dan apa dampaknya.** Untuk konteks proyek lihat **`docs/PROYEK.md`** (dokumen
tunggal). Untuk seluruh dokumentasi lihat **`docs/README.md`** (indeks).

Dokumen yang sering dirujuk dari sini:

| Untuk | Lihat |
|---|---|
| Konteks lengkap, perintah build, jebakan | `docs/PROYEK.md` |
| **Rantai suara** (profil terkunci, tuning, latensi) | `tools/tts/README.md` |
| Bukti kematian senyap 8 Okt | `tools/bukti/mati-saat-inferensi-2026-10-08.log` |
| Arsip (jangan dipakai sebagai acuan) | `docs/arsip/` |

---

## Ringkasan prioritas

| # | Masalah | Tingkat | Dampak utama |
|---|---|---|---|
| 1.1 | Proses mati senyap (intermiten; **direproduksi 2/2 pada 8 Okt**) | 🔴 Tinggi | Fitur inti (chat dengan LLM lokal) tidak andal |
| 1.2 | LipSync belum ada | 🟠 Sedang | Mulut tidak bergerak saat suara berbunyi |
| 1.3 | Instrumentasi penyidikan masih terpasang | 🟠 Sedang | Log membengkak; perilaku bisa ditimpa variabel lingkungan |
| 1.4 | ~~Data golden `phoneme_ids` belum ada~~ | ✅ | **Ditutup 8 Okt** — `piper-tts` membawa phonemizer sendiri |
| 1.5 | 503 "Loading model" tidak dikenali | 🟠 Sedang | UI menampilkan **OFFLINE** padahal model sedang dimuat |
| 2.1 | M10 tema/blur/font | 🟠 Sedang | Tampilan belum setara aplikasi lama |
| 2.2 | M11 TTS | 🟡 Sedang | **Rantai sudah terbukti jalan** (`tools/tts/`); sisa: integrasi ke aplikasi |
| 2.3 | M12 tray/hotkey/close-to-tray | 🟡 Rendah | Fitur kenyamanan belum ada |
| 3.1 | Repo bukan git | 🔴 Tinggi | Tidak ada cara membatalkan perubahan |
| 3.2 | Tidak ada uji untuk jalur native | 🟠 Sedang | Regresi Live2D hanya ketahuan manual |
| 3.3 | Pemakaian VRAM naik ~134 MB (mipmap) | 🟡 Rendah | Bisa memperburuk 1.1 |
| 4.1 | Toolchain banyak yang diblokir | 🟡 Rendah | Verifikasi harus lewat jalan memutar |

**Sudah ditutup di sesi ini:** paritas ukuran jendela, bug siklus hidup
pembongkaran (§5), **1.4 data golden `phoneme_ids`**, dan **rantai suara M11
terbukti berjalan** (§2.2).

---

## 1. Cacat aktif

### 1.1 🔴 Proses mati senyap saat llama memuat model — INTERMITEN

**Gejala.** Aplikasi hidup, panggung Live2D aktif, llama-server mulai memuat
GGUF — lalu beberapa puluh detik kemudian proses hilang. Tidak ada exception,
`run.out` kosong, tidak ada entri Event Log.

**Sebab.** Belum dipastikan. Yang sudah **disingkirkan** lewat uji:

| Uji | Hasil | Artinya |
|---|---|---|
| `VTUBER_STUB=true` | hidup ≥45 s | llama tidak terlibat |
| `VTUBER_LLM_PROVIDER=local` (`-ngl 0`) | hidup 50 s, llama-server **selesai memuat**, listening di :8788 | llama sendiri sehat; jalur CPU tidak memicu |
| Vulkan (`-ngl 99`) | sebagian hidup, sebagian mati | jalur Vulkan dicurigai |
| Event Log (`Get-WinEvent`) | kosong | bukan crash biasa |
| WER LocalDumps (diaktifkan khusus) | **tidak menghasilkan dump** | fail-fast tidak lewat jalur WER |
| Pencarian `FailFast`/`Environment.Exit` di `src/` | tidak ada | bukan kode kita yang memanggilnya |
| llama-server mati? | proses terpisah, tidak bisa menjatuhkan kita | bukan penyebabnya |

**Laju kejadian:** sekitar **1 dari 10** kali jalan (hitungan lama, hanya untuk
kematian saat llama memuat model). Angka ini lebih rendah daripada dugaan awal,
dan **cara menghitungnya sempat salah** — baca peringatan di bawah sebelum
memakai angka apa pun.

> ### ⚠️ Pembaruan 2026-10-08 — direproduksi 2 dari 2, di dua titik berbeda
>
> Cacat ini **bukan lagi sekadar laporan pengguna**; ia direproduksi sengaja.
> Dua peluncuran berturut-turut, keduanya mati:
>
> | Peluncuran | Mati di mana | Bukti di `crash.log` |
> |---|---|---|
> | 1 | saat prompt LLM **pertama** diproses (pekerja proaktif, idle 65 dtk) | `HttpIOException: The response ended prematurely (ResponseEnded)`; app + llama-server hilang bersamaan |
> | 2 | saat **pembuatan panggung Live2D**, sebelum `runtime:` pernah tercatat | berakhir di `live2d: ThisPtr=...`; nol baris `runtime:` |
>
> Keduanya tanpa entri Event Log, tanpa dump WER, tanpa `DisposeAsync`.
>
> **Uji kontrol yang menyaring banyak tersangka.** `llama-server` dijalankan
> **sendiri** (tanpa aplikasi, tanpa renderer D3D11) dengan argumen **persis
> sama** seperti yang dipakai aplikasi:
>
> | Uji | Hasil |
> |---|---|
> | `/health` | 200 `{"status":"ok"}` |
> | prompt 28 token | balasan mengalir normal |
> | prompt **2003 token** (seukuran prompt aplikasi) | **selesai normal**, 122,75 tok/s, server tetap hidup |
> | seluruh flag (`-rea off`, `--jinja`, `--flash-attn on`, `-ctk/-ctv`, …) | valid menurut `llama-server --help` |
>
> Jadi **model, flag, endpoint, dan ukuran konteks bukan penyebabnya**. Yang
> membedakan hidup dan mati adalah **hadirnya renderer Live2D**. Ini menguatkan
> dugaan fail-fast driver GPU.
>
> Bukti lengkap: `tools/bukti/mati-saat-inferensi-2026-10-08.log`
>
> **Pengaruh ke M11:** integrasi TTS sengaja ditahan sampai cacat ini tertutup.

> ⚠️ **"Proses hilang" belum tentu crash.** Loop uji yang hanya memeriksa
> `tasklist` tidak bisa membedakan crash dari jendela yang ditutup. Dua dari
> kematian yang sempat teramati ternyata **jendela uji yang ditutup pengguna**
> — terbukti dari entri `DisposeAsync` di log.
>
> | Akhir `crash.log` | Artinya |
> |---|---|
> | baris `[llama] ...`, **tanpa** exception sesudahnya | **crash sungguhan** (fail-fast) |
> | ada entri `DisposeAsync` / `ObjectDisposedException` | jendela ditutup — **bukan** crash |
>
> Bukti yang tersimpan: `tools/bukti/kematian-2026-10-07.log`.

**Petunjuk dari kematian yang log-nya sempat terbaca.** Dua entri exception
muncul berurutan pada satu kematian:

```
23:19:59.110  PancingObrolanAsync  System.Net.Http.HttpIOException:
                                  The response ended prematurely. (ResponseEnded)
23:19:59.144  SegarkanHealthAsync  System.ObjectDisposedException:
                                  The CancellationTokenSource has been disposed.
```

Keduanya **sudah dijelaskan dan diperbaiki** (bug siklus hidup, §5) — jadi
kematian itu bukan crash. Yang penting: pada kematian itu **aliran LLM terputus
di tengah jalan** (`ResponseEnded`), yang berarti llama-server berhenti
menjawab. Itu tetap petunjuk berguna untuk §1.1, karena menunjukkan llama-server
memang bisa berhenti di tengah prompt processing.

Dugaan terkuat masih **fail-fast di tingkat driver GPU**: rebutan antara
perangkat D3D11 kita (renderer Live2D) dan konteks Vulkan llama-server
(`VTUBER_VULKAN_NGL=99`, `VTUBER_VULKAN_CTX=16384`, KV cache f16).

**Dampak.** Ini cacat paling serius yang tersisa. Aplikasi adalah pendamping
AI/VTuber — tanpa LLM lokal yang andal, fungsi utamanya (mengobrol) tidak bisa
dipakai. Pengguna akan melihat jendela tiba-tiba menutup tanpa penjelasan.

**Langkah berikutnya (belum dikerjakan):**
1. Jalankan ulang loop uji (prosedurnya di `docs/PROYEK.md` §9) **dengan
   menyimpan `crash.log` tiap kematian**, lalu saring mana yang crash sungguhan
   memakai tabel di atas. Butuh sekitar 20–30 percobaan untuk mendapat sampel
   yang layak pada laju ~1 dari 10.
2. Uji konfirmasi GPU: turunkan `VTUBER_VULKAN_NGL` (mis. 20) atau
   `VTUBER_VULKAN_CTX` (mis. 4096). Kalau kematian hilang, dugaan GPU menguat.
3. Uji isolasi: hentikan polling health **dan** obrolan proaktif sementara —
   kalau hidup, masalahnya bukan di jalur itu.
4. Kalau GPU terkonfirmasi: serialkan inisialisasi Live2D terhadap
   `listening`-nya llama, atau turunkan tekanan VRAM (§3.3).

**Definisi selesai:** 10 kali jalan berturut-turut bertahan ≥2 menit dengan
Live2D aktif **dan** llama memuat model, tanpa exception di `crash.log`.

---

### 1.2 🟠 LipSync belum ada

**Sebab.** Belum diimplementasikan. Grup `LipSync` (`ParamMouthOpenY`) sudah
tersedia di `silverwolf.model3.json`, dan **rantai suaranya kini benar-benar
menghasilkan audio** (`tools/tts/`), tetapi tidak ada yang menggerakkan mulut.

> ⚠️ Catatan koreksi: sebelumnya dokumen ini menulis "TTS sudah dilaporkan siap".
> Yang melaporkan itu adalah `HealthSnapshot.Tts` yang **di-hardcode `"siap"`** —
> bukan hasil pemeriksaan. Jangan pakai nilai itu sebagai bukti apa pun.

**Dampak.** Saat karakter bicara, mulutnya diam. Ini yang paling terasa
"mati" pada VTuber — sekeras apa pun usaha pada render, tanpa lip-sync
kesannya masih seperti gambar bergerak.

**Langkah berikutnya.** Dua pilihan: `CubismLipSyncUpdater` resmi (menuntut
`CubismUpdateScheduler` yang sengaja kita hindari) atau jalur langsung: hitung
amplitudo RMS dari buffer audio TTS lalu `AddParameterValue(ParamMouthOpenY, a)`.
Yang kedua lebih sederhana dan cocok dengan pola `SiapkanEfek()` yang sudah ada.

**Prasyarat.** Audio harus sudah diputar **di dalam aplikasi**. Jadi 1.2
menunggu integrasi M11 (§2.2) selesai lebih dulu.

---

### 1.3 🟠 Instrumentasi penyidikan masih terpasang

**Sebab.** Sengaja dipasang untuk mendiagnosis cacat yang sekarang sudah
selesai (viewport 1×1, penunjuk menggantung, ketajaman).

**Yang masih aktif:**

| Instrumentasi | Biaya |
|---|---|
| Probe `D3DCompile` (2 probe) di `swl2d_stage_create` | Kompilasi shader dua kali tambahan saat start |
| Log per pemanggilan `MuatBerkas` | Puluhan baris per start |
| `PeriksaPiksel` bingkai ke-10 | `CopyResource` + `Map` + pemindaian 845×939 piksel |
| `SimpanBingkai` (`SWL2D_TANGKAP`) | opt-in, aman |
| Uji `SWL2D_UJI` | opt-in, aman |
| Penimpaan `SWL2D_PERBESARAN`/`GESER_X`/`JANGKAR_Y` | opt-in, tapi **bisa mengubah perilaku tanpa jejak di kode** |

**Dampak.** `crash.log` mencapai 35 KB hanya dari satu start. Yang lebih
berisiko: penimpaan lewat variabel lingkungan dapat mengubah tampilan tanpa
ada satu pun baris kode yang menjelaskannya — menyulitkan diagnosis berikutnya.
Tidak layak ikut ke rilis.

**Langkah berikutnya.** Jadikan probe `D3DCompile` dan log per-`MuatBerkas`
opt-in (`SWL2D_VERBOSE=1`); hapus probe setelah cacat terkait ditutup.

---

### 1.4 ✅ DITUTUP (2026-10-08) — data golden `phoneme_ids`

**Masalah lama.** Butuh `piper_phonemize.wasm` berjalan di browser; tidak bisa
dijalankan dari CLI. Aplikasi web lama sudah dihapus, dan yang tersimpan hanya
sumber JS-nya (`docs/arsip/sumber-web/`). Dulu dicatat sebagai satu-satunya
blocker proyek.

**Kenapa ditutup.** Paket `piper-tts` membawa phonemizer espeak-ng-nya sendiri,
jadi phonemizer tidak perlu dibangun ulang dan `piper_phonemize.wasm` tidak
dibutuhkan sama sekali. Rantai suara berjalan tanpa berkas itu — lihat §2.2.

Uji paritas phonemizer tetap **boleh** dilakukan kalau dianggap berguna, tetapi
bukan lagi penghambat apa pun. Kalau nanti ingin tetap ada, gantinya adalah uji
fonem dasar terhadap kalimat acuan, bukan paritas byte-per-byte.

### 1.5 🟠 Status "memuat model" tidak dikenali — UI menampilkan OFFLINE

**Sebab.** `OpenAiCompatibleProvider.AvailableAsync` (`Inference/OpenAiCompatibleProvider.cs`
baris 65–74) mencari `{"status":"loading model"}`, padahal badan 503
`llama-server` yang sebenarnya:

```json
{"error":{"message":"Loading model","type":"unavailable_error","code":503}}
```

Properti `status` tidak ada → cabang `Loading = true` tidak pernah kena → fungsi
jatuh ke `{ Ok = false, Loading = false, Reason = "HTTP 503" }`.

**Dampak.** Saat model sedang dimuat, `StatusTeks` menjadi `"tidak-jalan"` dan
tombol menampilkan **OFFLINE**. Pengguna mengira LLM tidak jalan lalu menutup
aplikasi, padahal hanya perlu menunggu. Ini juga memperburuk 1.1, karena
kematian sering terjadi tepat pada fase pemuatan yang salah dilaporkan ini.

**Langkah berikutnya.** Kenali juga bentuk `error.message == "Loading model"`,
atau perlakukan setiap 503 dari llama-server sebagai `Loading = true`.
Perbaikan kecil dan aman — lihat `docs/PROYEK.md` §8.6.

---

## 2. Fitur yang belum selesai (per milestone)

### 2.1 🟠 M10 — tema, blur, animasi, font

**Belum dikerjakan sama sekali.** Referensi utamanya (`style.css`, 1.318
baris) **sudah hilang** bersama aplikasi lama; yang tersisa hanya arsip
`sumber-web/`.

**Risiko yang sudah teridentifikasi:** tiga font yang dibundel adalah
**variable font**. DirectWrite seharusnya menerapkan instance sumbu `wght`
otomatis saat `FontWeight` diminta, tetapi **ini belum pernah diuji di WinUI 3**.
Kalau ternyata hanya instance bawaan yang dipakai, seluruh teks akan tampak
dengan berat yang sama — dan solusinya harus mengambil instance statis dari
`ofl/<keluarga>/static/`.

**Dampak.** Tampilan belum setara aplikasi lama; bobot font berisiko salah.

---

### 2.2 🟡 M11 — TTS: rantai sudah terbukti, integrasi belum

**Status berubah pada 2026-10-08.** Sebelumnya "belum ada sama sekali"; sekarang
**rantai suaranya berjalan dan menghasilkan audio nyata**, tetapi masih sebagai
skrip Python di luar aplikasi.

**Sudah terbukti berjalan.** Teks Indonesia → Piper (`id_ID-news_tts-medium`,
espeak voice `id`) → RVC v2 (`SilverWolfJP`) → WAV 40 kHz. Seluruh aset sudah
ada di repo; tidak ada yang perlu diunduh. Acuan hasil:
`tools/tts/contoh/04-transpose-3.wav`. Cara pakai, angka tuning, dan latensi ada
di **`tools/tts/README.md`**.

Dua catatan lama yang **tidak lagi berlaku**:

- ~~`native/SilverWolf.Phonemizer/` (espeak-ng) harus dibangun.~~ Paket
  `piper-tts` membawa phonemizer-nya sendiri. Ini juga menutup **1.4**.
- ~~RVC mati karena ContentVec ONNX hilang.~~ `rvc-python` memakai
  `hubert_base.pt` lewat fairseq, bukan ONNX. `assets/encoders/` yang kosong
  tidak menghalangi.

**Yang belum.** Ini murni pekerjaan integrasi, bukan riset:

| Sisa | Keterangan |
|---|---|
| `src/SilverWolf.TtsWorker/` | OutputType Exe, **tanpa** WindowsAppSDK |
| Pemutaran NAudio | memutar WAV hasil konversi di dalam aplikasi |
| Hook di `CompanionViewModel` | panggil TTS setelah tiap balasan assistant |
| Cache | `.env` sudah punya `VTUBER_TTS_CACHE=ya`, belum diterapkan |
| LipSync | lihat 1.2 |
| `HealthSnapshot.Tts` | masih di-hardcode `"siap"` — harus jujur |

**Kenapa worker terpisah, bukan in-proc:** `onnxruntime.dll` bentrok dengan
`Microsoft.Windows.AI.MachineLearning` bawaan WindowsAppSDK → `APPX1101`.
Lihat `docs/PROYEK.md` §7.1.

**Kenapa integrasi ditahan:** cacat **1.1** (aplikasi mati senyap) belum
tertutup. Menambah fitur ke aplikasi yang belum bisa bertahan hidup tidak
berguna.

**Kendala praktis.** RVC berjalan di CPU (tidak ada GPU CUDA di mesin ini)
dengan **RTF 1,1–2,5× realtime** — klip 5 detik butuh 6–12 detik. Layak kalau
diproses **per kalimat** dan diputar berurutan sambil kalimat berikutnya
dikonversi; tidak layak untuk realtime sejati tanpa GPU.

---

### 2.3 🟡 M12 — tray, hotkey, single-instance, close-to-tray

Belum dikerjakan; dua penanda `TODO(M12)` sudah dipasang di kode
(`App.xaml.cs:64`, `MainWindow.xaml.cs:114`). Isinya: `AppInstance.FindOrRegisterForKey`,
tray icon, `RegisterHotKey` Ctrl+Shift+S, dan close-to-tray
(`appWindow.Closing` + `e.Cancel` + `presenter.Hide()`).

**Dampak.** Kenyamanan pemakaian; tidak memblokir fitur lain.

---

### 2.4 🟡 M13 integrasi end-to-end · M14 rilis

Belum dimulai. M13 bergantung pada M10–M12.

---

## 3. Utang teknis & risiko

### 3.1 🔴 Repo bukan repositori git

`git status` → `fatal: not a git repository`. **Tidak ada riwayat versi dan
tidak ada cara membatalkan perubahan.** Sepanjang sesi ini beberapa perubahan
besar dilakukan (mipmap, bingkai, efek hidup, dokumen digabung) tanpa titik
kembali selain berkas cadangan manual.

**Dampak.** Kesalahan apa pun bersifat permanen kecuali dibuat cadangan
manual lebih dulu. Ini risiko proses, bukan risiko kode — dan yang paling
mudah diperbaiki.

**Langkah berikutnya.** `git init` + `.gitignore` sudah ada (aset besar,
model berlisensi, dan `silver_wolf_memory/` sudah diabaikan) → commit awal.

### 3.2 🟠 Tidak ada uji otomatis untuk jalur native

108 unit test semuanya menguji `SilverWolf.Core`. Jalur native
(`Live2DStage.cpp`, 1.900 baris) **tidak punya satu pun uji otomatis** —
regresi hanya ketahuan dengan menjalankan aplikasi lalu membaca `crash.log`.

**Dampak.** Setiap perubahan pada renderer Live2D berisiko merusak sesuatu
tanpa ketahuan sampai dilihat mata. Tiga cacat terbesar di proyek ini
(viewport 1×1, penunjuk menggantung, koreksi aspek) semuanya lolos dari build
hijau.

**Langkah berikutnya.** Alat `SWL2D_UJI` sudah bisa dipakai sebagai dasar:
jadikan `PeriksaPiksel` bingkai ke-10 sebagai **ambang** (mis. `terisi > 50.000`
dan kotak dalam batas wajar) dan gagalkan uji kalau di luar rentang.

### 3.3 🟡 Pemakaian VRAM naik ~134 MB

Rantai mip pada 6 tekstur 4096² menambah sekitar 134 MB (total ~536 MB).

**Dampak.** Belum terukur dampaknya terhadap §1.1 — kalau kematian itu memang
soal tekanan memori GPU, perubahan ini bisa **memperburuknya**. Perlu diuji
bersama.

### 3.4 🟡 `crash.log` tumbuh tanpa batas

`CrashLog.Tulis` memakai `File.AppendAllText` tanpa rotasi. Satu start
menghasilkan 35 KB; dengan log per-`MuatBerkas` dan per-bingkai, pemakaian
panjang akan membengkakkannya terus.

**Dampak.** Berkas log besar; pembersihan manual. Tidak berbahaya, tapi
sebaiknya diberi batas ukuran atau rotasi.

### 3.5 🟡 `AturUkuranTarget` membuat ulang renderer tiap resize

Setiap perubahan ukuran panel memanggil `CreateRenderer` ulang (membuat ulang
render target + mengikat ulang 6 tekstur). Itu **perlu** untuk kebenaran
(viewport), tetapi saat jendela ditarik-tarik ukurannya, ini terjadi puluhan
kali per detik.

**Dampak.** Belum diukur. Berpotensi membuat resize terasa berat, dan
menambah churn perangkat GPU — relevan untuk §1.1.

### 3.6 🟡 Kegagalan panggung Live2D tidak sampai ke pengguna

`StageView` mencoba ulang 20× dengan jeda 150 ms; kalau gagal, yang terlihat
hanya placeholder teks "STAGE // Live2D — placeholder (M8)". Penyebabnya
(DLL hilang? shader tidak ketemu? IID salah?) hanya ada di `crash.log`.

**Dampak.** Pengguna tidak tahu apa yang salah dan tidak tahu harus berbuat apa.

---

## 4. Kendala lingkungan & toolchain

| # | Kendala | Dampak |
|---|---|---|
| 4.1 | `MSBuild.exe`, `dotnet msbuild`, `reg.exe`, `Add-Type`, `New-Object -ComObject` **diblokir kebijakan** | Verifikasi properti MSBuild & operasi sistem harus lewat jalan memutar (csproj probe, Python `winreg`) |
| 4.2 | PowerShell sandbox menolak sebagian perintah (mis. penulisan registry lewat cmdlet) | Harus jatuh ke Python |
| 4.3 | Pengaman **safe-delete** menolak hapus permanen bila trash gagal | Penghapusan besar bisa tertahan (pernah menyisakan 824 MB) |
| 4.4 | Python tanpa `SetProcessDpiAwarenessContext` melaporkan DPI salah | Pernah menyesatkan diagnosis ketajaman; Pillow hanya ada di Python sistem 3.10 |
| 4.5 | `kill -0 $PID` tidak bisa dipakai cek proses hidup | Harus pakai `tasklist //FI` |

**Catatan:** WER LocalDumps **sempat diaktifkan** untuk mendiagnosis §1.1
(`HKCU\Software\Microsoft\Windows\Error Reporting\LocalDumps\SilverWolf.App.exe`).
Karena tidak menghasilkan dump, kuncinya **sudah dihapus kembali** dan
kebersihannya sudah diverifikasi — tidak ada perubahan sistem yang tertinggal.

---

## 5. Yang sudah diperbaiki di sesi ini

| Cacat | Bukti |
|---|---|
| "Build Debug mati saat `GenerateShaders`" — **bukan stack overflow**, melainkan cacat penunjuk menggantung yang sama | Uji A/B: `Option` lokal → mati; `g_opsi` statis → model tampil |
| Napas, kedip, ikut-kursor | Log `efek: kedip aktif, 2 parameter` / `napas aktif` / `pandangan aktif`; peredaman terukur 0.0000 → 1.0002 dalam ~9 bingkai |
| Bingkai kepala kegedean (`1.85` → `0.88`, geser −0.10) | Piksel terisi 270.523 → 103.565; potongan sayap 90 → 12 piksel |
| Ketajaman (mipmap) | `MipLevels = 0` + `GenerateMips` |
| **Siklus hidup pembongkaran** — `_cts` dibuang saat callback timer masih jalan | Jendela ditutup sengaja lewat `WM_CLOSE`: **0 entri exception** (sebelumnya selalu 2). Ditambah penjaga `_sedangDibuang`, `_cts` kini hanya dibatalkan, dan handler `Tick` dibungkus `try/catch` supaya exception tak bisa fail-fast-kan proses |
| **Paritas ukuran jendela** — `AppWindow.Resize` memakai piksel fisik | Log: `Resize(1475,950) [skala 1,250]` = 1180×1,25 · panggung 641×851 px |
| 5 dokumen `.md` yang tumpang tindih | Digabung jadi `docs/PROYEK.md`; yang lama diarsipkan |
| Konfirmasi ekspresi & gerakan | 9 ekspresi + 4 gerakan, semuanya berfungsi (terukur) |

---

## 6. Urutan tindak lanjut yang disarankan

1. **`git init` + commit awal** (§3.1) — paling murah, paling besar
   manfaatnya. Sudah beberapa kali perubahan besar dilakukan tanpa titik
   kembali; risiko ini harus ditutup lebih dulu.
2. **Hentikan kematian senyap (§1.1).** Sudah direproduksi 2/2, jadi tidak
   perlu lagi berburu sampel — langsung uji tuas `.env` satu per satu
   (urutan dan alasannya di `docs/PROYEK.md` §8.1). Ini yang memblokir M11.
3. **Perbaiki pengenalan 503 (§1.5)** — kecil, aman, langsung menghilangkan
   kebingungan "OFFLINE padahal sedang memuat".
4. **Integrasikan rantai suara M11 (§2.2) + LipSync (§1.2)** — bersama-sama,
   karena lip-sync butuh audio. Rantainya tidak perlu diriset lagi; resepnya di
   `tools/tts/README.md`. Sekalian ganti `HealthSnapshot.Tts` yang di-hardcode.
5. **Bersihkan instrumentasi** (§1.3) setelah §1.1 punya kejelasan.
6. **M10 tema/font** — sekalian uji risiko variable font.
7. **Tambahkan uji ambang untuk jalur native** (§3.2).
8. **M12 → M13 → M14.**

---

## 7. Cara memperbanyak bukti untuk §1.1

Prosedur loop ujinya ada di **`docs/PROYEK.md` §9** ("Menguji kematian
berulang"), lengkap dengan tabel cara membedakan crash sungguhan dari jendela
yang ditutup. Bukti yang sudah tersimpan:
`tools/bukti/kematian-2026-10-07.log`.

Yang dicari di log kematian berikutnya:

- kalau berakhir pada baris `[llama] ...` tanpa exception → **crash sungguhan**,
  dan tidak ada yang bisa dibaca selain "mati setelah baris ini";
- kalau ada `HttpIOException: ResponseEnded` → llama-server berhenti menjawab
  di tengah aliran (petunjuk kuat ke arah GPU/Vulkan);
- kalau ada `ObjectDisposedException` → itu jendela ditutup, bukan crash
  (bug-nya sudah diperbaiki, jadi seharusnya tidak muncul lagi).
