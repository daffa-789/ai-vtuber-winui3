# Ringkasan jawaban — model full body, performa, suara, memori

Empat permintaan Master, dijawab satu per satu dengan bukti. Diringkas; rincian
ada di dokumen yang ditunjuk.

---

## 1. Model full body + semua aksesori terlihat

**Status: pembingkaian diterapkan & diukur. Konfirmasi visual terakhir milik Master.**

Nilai final di `src/SilverWolf.App/Views/StageView.xaml.cs`:

```csharp
private const float Perbesaran = 0.70f;   // dulu 0.78, 0.80, 0.88, 1.85 (warisan web)
private const float GeserX     = -0.20f;  // model ini asimetris: sayap kanan lebih panjang
private const float JangkarY   = 0.30f;   // angkat agar ruang terisi merata
```

### Kenapa 0.78 tidak cukup

Karakter mengisi **524 px dari panel 672 px** pada 0.78 → hanya **~88 px** sisa
untuk **dua** margin. Permintaan "geser 80 px ke kiri sambil semua kelihatan"
**mustahil** di angka itu — harus diperkecil dulu. Di 0.70 lebar turun ~496 px,
sisa ~145 px, baru geser bisa aman.

### Hasil terukur (dua jalan berbeda)

| Jalan | kotak di `crash.log` | margin kiri | margin kanan |
|---|---|---|---|
| Sebelum (0.78, dari tangkapan Master) | — | 147 px | **1 px (terpotong)** |
| Sesudah, jalan A | (46,151)-(543,555) | 46 px | 98 px |
| Sesudah, jalan B | (45,153)-(532,555) | 45 px | **109 px** |

Dua jalan cocok (selisih < 2 px), dan **tidak ada clamp** (`x0` = 45/46 bukan 0).
**Sayap tidak lagi mentok** — dari 1 px jadi ~100 px ruang.

### Yang ada di model (202 part, 6 tekstur 4096²)

Aksesori utama yang sekarang masuk bingkai:
`Part192 正常翅膀` (sayap normal) · `Part171 变身翅膀` (sayap transformasi) ·
`Part78 头饰` + `Part158 头饰后` (hiasan kepala) · `Part81 正常版眼镜` (kacamata) ·
`Part132 外套` (jaket) · `Part143 腰甲前` (armor pinggang) · `Part146 腰带` (sabuk) ·
`Part147 腿` (kaki) · `Part154 后摆` (rok belakang) · `Part7 泡泡糖` (permen karet)

> **Tetap butuh mata Master.** Sesi otomatis ini **tidak bisa** menangkap jendela
> (aplikasi dari Bash hidup di desktop berbeda — `EnumWindows` melihat 158 jendela
> milik Master, nol "Silver Wolf"). Kalau masih ada bagian yang terpotong, kirim
> tangkapan layar — angka `Perbesaran`/`GeserX` tinggal disetel dengan aritmetika
> yang sama. Ada juga tombol cepat tanpa build ulang: `SWL2D_PERBESARAN`,
> `SWL2D_GESER_X`, `SWL2D_JANGKAR_Y`.

---

## 2. Tips performa rendering & animasi

**Rincian + angka terukur di `docs/PERFORMA.md`.** Intinya:

**Sudah benar (jangan diubah):**
- **30 fps** (timer 33 ms), sengaja bukan 60 — Live2D halus di 30 fps, dan ini
  jauh lebih hemat daripada `CompositionTarget.Rendering` yang menembak tiap
  bingkai UI.
- **`VTUBER_VULKAN_CTX` = 4096** (dulu 16384) — nilai lama menahan **~2,3 GB**
  KV cache tanpa manfaat. Putuskan Mode B.
- **`VTUBER_VULKAN_NGL` = 40** — `0` **bukan** penghemat memori, malah membebani
  CPU. 40 (bukan 99) supaya tidak berebut dengan D3D11 Cubism.
- **Interval health naik ke 8000 ms** begitu server siap.

**Sisa penghematan (termurah dulu):**
1. **Hentikan render saat jendela di-minimize** — penghematan terbesar yang
   belum dikerjakan.
2. Turunkan `VITE_IRAMA_FPS_SAAT_TAK_FOKUS` dari `30` ke `15`.
3. `VTUBER_VULKAN_CACHE_TYPE` f16 → `q8_0` (pangkas cache KV separuh; ukur mutu
   jawaban sebelum/sesudah).
4. Matikan efek yang tak dipakai (`Part196`, `Part38`) **hanya dengan izin** —
   ini mengubah tampilan.

**Kenapa RAM begitu penting di sini:** Iris Xe terintegrasi → **VRAM = RAM
sistem**. Tiap MB renderer langsung mengurangi jatah model bahasa 5,12 GB.

---

## 3. Penyebab suara tidak keluar + solusinya

### Penyebab (terbukti)

**Tidak ada satu pun kode pemutaran audio di seluruh proyek.**

Pencarian `WaveOutEvent|AudioFileReader|using NAudio` di `src/` mengembalikan
**nol hasil**. NAudio dirujuk di csproj sehingga DLL-nya ikut tersalin ke folder
keluaran — **tetapi tidak pernah dipanggil**. `KirimAsync` mengalirkan balasan
ke gelembung chat lalu **berhenti**. Tidak ada langkah setelahnya.

Proyek ini bahkan sudah mencatatnya sendiri di `tools/tts/README.md`:
> "Belum ada `SilverWolf.TtsWorker`, belum ada pemutaran NAudio, dan
> `CompanionViewModel` belum memanggil TTS setelah balasan."

Jadi ketika Master kirim pesan: **Teks masuk → llama jawab → gelembung terisi →
selesai.** Tidak ada yang membacakan. Persis keluhan "pesannya sudah jalan, TTS
tidak balas."

### Solusi (sudah dikerjakan)

| Berkas | Peran |
|---|---|
| `src/SilverWolf.Services/Tts/TtsWorker.cs` | jalankan `tools/tts/buat_suara.py` sebagai proses; potong per kalimat; cache SHA-256 atas **teks + seluruh parameter suara**; batas 40 dtk |
| `src/SilverWolf.Services/Tts/PcmPlayer.cs` | **`WaveOutEvent` + `AudioFileReader`** — inilah bagian yang hilang; `LevelBerubah` tiap 16 ms untuk gerak mulut |
| `src/SilverWolf.Services/Tts/TtsPipeline.cs` | gabungkan produksi + pemutaran; balasan terbaru selalu menang |
| `CompanionViewModel` | `SiapkanTts()`, `BacakanAsync()`, pemicu di `AlirkanAsync` |

**Pemicunya sengaja ditaruh SETELAH aliran selesai**, bukan di tengah:
```csharp
gelembung.Pending = false;

var untukDibacakan = gelembung.Content;
if (!string.IsNullOrWhiteSpace(untukDibacakan))
{
    _ = BacakanAsync(untukDibacakan);
}
```
Memotong kalimat butuh titik akhir yang jelas; memulai RVC sebelum teks lengkap
membuat kalimat terakhir hilang. Tidak di-`await` supaya gelembung langsung
terlihat selesai sementara suara menyusul.

### Bukti sudah jalan (bukan asumsi)

- `dotnet build SilverWolf.sln -p:Platform=x64 -c Debug` → **0 warning, 0 error**
- `dotnet test` → **114/114 lulus**
- Rantai dijalankan dengan perintah **persis** seperti yang dikirim aplikasi →
  `sw-e2e.wav` = **40.000 Hz mono 16 bit, 4,16 detik, RMS 6.105, puncak
  28.877/32.767** → **ADA SUARA** (RMS 0 = sunyi)
- `crash.log` aplikasi nyata memuat: **`tts: siap (rantai=piper+rvc,piper, rvc=True)`**
- Semua kunci `.env` ↔ `AppConfig.cs` dicocokkan satu per satu: **cocok semua**

### Kalau nanti masih bisu, cek berurutan

1. **Lihat baris TTS di UI** — `TeksTts` sekarang menampilkan alasan sebenarnya,
   bukan lagi `"siap"` palsu. Dulu rantai MATI tampil sama dengan rantai HIDUP.
2. `VTUBER_TTS=ya` di `.env`?
3. Berkas `tools/tts/buat_suara.py` ada?
4. Python RVC ada? `VTUBER_PY_RVC` kosong = jatuh ke venv "voice changer3 glm".
5. Kalau lambat (bukan bisu): RVC di CPU RTF 1,1–2,5× realtime. Untuk uji cepat,
   `VTUBER_RVC=tidak` → Piper saja, hampir instan.

---

## 4. Variasi memori Silver Wolf

**Ditambahkan atas izin Master untuk sesi ini** (folder `silver_wolf_memory/`
adalah pulau terpisah; tidak ada tautan ke catatan proyek, `Riwayat/` dan
`kizuna/` tidak disentuh).

| Berkas | Ditambahkan |
|---|---|
| `persona.md` | **25 contoh nada baru** dalam 5 register: manja & mesra, usil & ledekan, saat kerja/ngoding, Master capek/sedih, situasi tak terduga |
| `Scenario_Library.md` | **8 skenario baru** (13–20): dipanggil "sayang", koreksi fakta salah, minta ditenangkan bukan diselesaikan, hasil bagus, menyalahkan diri berlebihan, diminta kaku, kembali setelah lama hilang, kenapa suka ngeledek |
| `Quotes.md` | **4 tabel baru**: sapaan & perpisahan, ditanya soal dirinya, tentang Trailblazer |
| `Preferences.md` | Selera suasana (hujan+game, ice bath, basement), kebiasaan (nyapa duluan, tutup tiap babak "The game called X ended"), tabel tertarik vs bosen |

Semua mengikuti konvensi berkasnya: tag wajah dari daftar tertutup
(`[netral]`/`[senyum]`/`[semangat]`/`[kaget]`/`[bingung]`/`[lelah]`/`[goda]`/`[sebal]`/`[sedih]`),
tanpa emoji, satu baris satu ide, bahasa Indonesia gaul yang enak dibaca TTS.

**Batas graph tetap utuh** — dicek: semua sebutan "Qoder Memory" cuma ada di teks
larangan (sudah ada sebelumnya), dan tidak ada tautan `[[...]]` baru ke luar pulau.

---

## Catatan jujur

- **Mode A (mati saat kompilasi shader) terbukti INTERMITEN.** Empat jalan
  berturut-turut: 1 mati (13.449 bita), 2 hidup, 2 hidup + probe. Biner **sama
  persis**. Jadi bukan data rusak — race condition. Konsekuensi: satu
  keberhasilan tidak membuktikan perbaikan; uji minimal 5× dan laporkan rasio.
- **Mode B dan C belum tertutup.** Mode C (mati saat Vulkan mulai inferensi)
  adalah yang paling menyerupai keluhan awal Master.
- **Konfirmasi visual pembingkaian masih milik Master** — tangkapan jendela
  mustahil dari sesi ini.
