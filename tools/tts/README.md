# tools/tts — Suara Silver Wolf (TTS Indonesia + RVC)

Rantai suara yang sudah **terbukti berjalan** pada 2026-10-08. Ini adalah inti
M11, tetapi dijalankan sebagai skrip Python, belum terpasang di dalam aplikasi
WinUI 3 (lihat "Yang belum dikerjakan" di bawah).

| Untuk | Lihat |
|---|---|
| Indeks seluruh dokumentasi | `../../docs/README.md` |
| Konteks proyek, status milestone, cacat terbuka | `../../docs/PROYEK.md` |
| Prioritas masalah & dampaknya | `../../docs/LAPORAN-MASALAH.md` |
| Orientasi cepat (build, struktur) | `../../README.md` |

---

## 🔒 PROFIL SUARA DIKUNCI — jangan diubah tanpa persetujuan

Profil ini dipilih Master dan **tidak boleh diubah**. Hasil acuannya:

```
contoh/04-transpose-3.wav        (F0 median 218,8 Hz)
```

Semua angkanya hidup di **`.env`** di akar repo — itu **satu-satunya sumber
kebenaran**. Skrip di folder ini membacanya, jadi tidak mungkin ada dua angka
yang berbeda antara skrip dan aplikasi.

| Parameter | Kunci `.env` | Nilai terkunci |
|---|---|---|
| Model RVC | `VTUBER_RVC_MODEL` | `SilverWolfJP` |
| Versi | `VTUBER_RVC_VERSI` | `v2` |
| **Transpose** | `VTUBER_RVC_TRANSPOSE` | **`-3`** |
| **Index rate** | `VTUBER_RVC_INDEKS_LAJU` | **`0.6`** |
| Metode pitch | `VTUBER_RVC_F0` | `rmvpe` |
| Proteksi | `VTUBER_RVC_PROTEKSI` | `0.33` |
| Pencucian | `VTUBER_RVC_PENCUCIAN` | `3` |
| Campur RMS | `VTUBER_RVC_CAMPUR_RMS` | `0.8` |
| Panjang Piper | `VTUBER_TTS_PIPER_PANJANG` | `0.9` |

Diverifikasi ulang pada 2026-10-08: menjalankan `buat_suara.py` **tanpa argumen
apa pun** menghasilkan F0 median **220,1 Hz** — setara acuan 218,8 Hz (selisih
kecil hanya dari penyampelan derau Piper).

Argumen CLI (`--transpose`, `--index-rate`, …) tetap bisa menimpa untuk
eksperimen, tetapi **nilai bawaan selalu mengikuti `.env`**.

## Rantai

```
teks Indonesia
      |
      v
[1] Piper TTS          assets/piper/id_ID-news_tts-medium.onnx
    espeak voice "id"  -> WAV mono 22.050 Hz
      |
      v
[2] RVC v2             assets/rvc/SilverWolfJP/SilverWolfJP.pth
    f0: rmvpe          + added_IVF1442_Flat_nprobe_1_SilverWolfJP_v2.index
    HuBERT: hubert_base.pt (fairseq)
      |
      v
WAV mono 40.000 Hz, suara Silver Wolf
```

## Aset yang dipakai (semuanya sudah ada di repo)

| Peran | Berkas | Ukuran |
|---|---|---|
| TTS Indonesia | `assets/piper/id_ID-news_tts-medium.onnx` + `.json` | 63 MB |
| Checkpoint RVC | `assets/rvc/SilverWolfJP/SilverWolfJP.pth` | 55 MB |
| Indeks FAISS | `assets/rvc/SilverWolfJP/added_IVF1442_Flat_nprobe_1_SilverWolfJP_v2.index` | 178 MB |
| Encoder konten | `hubert_base.pt` (fairseq, bukan ONNX) | 190 MB |
| Ekstraktor pitch | `rmvpe.pt` / `rmvpe.onnx` | 181 / 362 MB |

`assets/voices/silverwolf/model.onnx` (110 MB) adalah **RVC Synthesizer yang
sudah diekspor ke ONNX** (graf `RVC_Synthesizer`, tensor `enc_p.emb_phone.*`).
Berkas itu belum dipakai jalur ini — berguna nanti kalau RVC mau dipindah ke
ONNX Runtime murni tanpa torch.

**Catatan penting:** `assets/encoders/` **kosong**. Dokumentasi lama menyebut
hilangnya ContentVec ONNX sebagai sebab RVC mati di aplikasi web. Ternyata itu
tidak lagi menjadi penghalang: `rvc-python` memakai `hubert_base.pt` lewat
fairseq, bukan ONNX. Jadi ContentVec ONNX tidak dibutuhkan.

## Cara pakai

```bash
# butuh Python dengan torch + fairseq + rvc-python (lihat di bawah)
PY="C:/Users/Daffa/Desktop/Folder Space AI/Folder Space Semester 6/voice changer3 glm/venv/Scripts/python.exe"

# teks -> suara Silver Wolf (memakai profil terkunci dari .env)
"$PY" tools/tts/buat_suara.py "Halo Master, aku Silver Wolf." keluar.wav

# hanya Piper (tanpa RVC), untuk membandingkan
"$PY" tools/tts/buat_suara.py --no-rvc "Teks" piper-saja.wav

# konversi WAV yang sudah ada (profil dari .env)
"$PY" tools/tts/rvc_konversi.py masuk.wav keluar.wav

# menimpa .env hanya untuk eksperimen
"$PY" tools/tts/rvc_konversi.py masuk.wav coba.wav --transpose 0
```

Jalankan `--help` pada salah satu skrip untuk melihat nilai `.env` yang sedang
aktif — setiap opsi mencetak nilai bawaannya beserta nama kunci `.env`-nya.

## Lingkungan Python yang dibutuhkan

Tahap RVC menuntut **torch + fairseq + rvc-python + faiss + librosa**. Di mesin
ini semuanya sudah tersedia di venv proyek lain:

```
C:/Users/Daffa/Desktop/Folder Space AI/Folder Space Semester 6/voice changer3 glm/venv
    Python 3.10.11 · torch 2.12.1+cpu · fairseq 0.12.2 · rvc-python 0.1.5 · faiss 1.7.3
```

Piper hidup di lingkungan terpisah dan dipanggil sebagai proses:

```
C:/Users/Daffa/.workbuddy-ai/binaries/python/envs/default
    Python 3.13.14 · piper-tts 1.8.0 · onnxruntime 1.30.0
```

Jalur `piper.exe` bisa ditimpa lewat variabel lingkungan `SW_PIPER_EXE`.

### Dua jebakan yang wajib diketahui

1. **`torch.load` harus dipatch.** PyTorch 2.6+ memakai `weights_only=True`
   secara bawaan, sedangkan checkpoint HuBERT dan RVC ditulis sebelum itu.
   Tanpa patch, pemuatan gagal `UnpicklingError`. Kedua skrip di sini sudah
   memasang patch itu sendiri.
2. **Nama parameter F0 adalah `f0method`.** Proyek `voice changer3 glm`
   menyetel `f0_method` dan `f0_extractor` — keduanya tidak ada, sehingga RVC di
   sana sebenarnya berjalan dengan `harvest` (bawaan), bukan `rmvpe`. Gunakan
   `set_params(f0method=...)`.
3. **Index bisa gagal dipakai tanpa pesan apa pun.** `pipeline.py:313-315`
   memanggil `index.reconstruct_n(0, index.ntotal)`. Untuk index IVF, itu hanya
   berhasil kalau `DirectMap` ada. Kalau gagal, `except` di baris 316-318
   menelan galatnya, menulis traceback ke stderr, lalu menyetel
   `index = big_npy = None` — dan hasilnya **tetap keluar**, hanya tanpa index.
   Karena itu "RVC jalan" belum berarti "index dipakai". Uji dengan
   `index_rate=0` vs `index_rate=0.6` lalu bandingkan keluarannya.

## Verifikasi index (2026-10-08)

`added_IVF1442_Flat_nprobe_1_SilverWolfJP_v2.index` diperiksa langsung:

```
tipe      : IndexIVFFlat
ntotal    : 56.257 vektor
dimensi   : 768
nprobe    : 1
DirectMap : ada
reconstruct_n(0, ntotal) -> (56257, 768) float32   OK
```

A/B pada masukan yang sama (Piper mentah, transpose -3):

| | index_rate |
|---|---|
| A | 0 (index dimatikan) |
| B | 0,6 (`VTUBER_RVC_INDEKS_LAJU`) |

Hasil: beda maksimum **0,905**, beda RMS **0,077**, korelasi **0,925** — jadi
index benar-benar berpengaruh, bukan sekadar dimuat lalu diabaikan.

**Catatan kualitas:** `nprobe` tersimpan = **1**, artinya hanya 1 klaster yang
ditelusuri per kueri. `pipeline.py` tidak menaikkannya, jadi nilai itu yang
dipakai. Menaikkan `nprobe` (mis. 8–16) sebelum `infer_file` biasanya
memperbaiki kemiripan timbre, dengan biaya waktu. Belum diuji di sini.

## Tuning: transpose (sudah dikunci di -3)

Nilai lama `.env` adalah `+9`, dikalibrasi untuk pipeline lama yang sumbernya
bersuara lebih rendah. **Piper `id_ID-news_tts-medium` bersuara tinggi (F0 median
264,8 Hz)**, sehingga +9 menghasilkan 442 Hz — terlalu tinggi untuk Silver Wolf
yang santai dan datar.

Hasil pengukuran nyata (kalimat uji yang sama, `librosa.pyin`):

| Transpose | F0 median keluaran | Catatan |
|---|---|---|
| +9 (nilai lama) | 442,7 Hz | terlalu tinggi, terdengar seperti gadis anime ceria |
| 0 | 261,7 Hz | setara Piper, belum turun |
| **-3** | **218,8 Hz** | **dipilih & dikunci** — paling dekat dengan 223,8 Hz yang dicatat 29 Sep |

**`VTUBER_RVC_TRANSPOSE` sekarang `-3`.** Ini bukan perubahan selera, melainkan
penerus nilai +9 dari pipeline lama setelah sumber suaranya berganti. Kalau
nanti memakai model Piper lain, ukur ulang dengan cara yang sama.

## Latensi terukur (CPU, tanpa GPU CUDA)

| Tahap | Waktu |
|---|---|
| Muat model RVC (setelah impor) | 0,5 s |
| Piper, 1 kalimat | ~1 s |
| RVC | **RTF 1,1–2,5× realtime** |

Artinya klip 5 detik butuh 6–12 detik untuk dikonversi. Belum realtime, tetapi
layak kalau diproses **per kalimat** dan diputar berurutan sambil kalimat
berikutnya dikonversi. Memakai GPU CUDA akan memangkasnya drastis.

## Berkas contoh

| Berkas | Isi |
|---|---|
| `contoh/01-piper-indonesia.wav` | Piper mentah, 22.050 Hz, 4,95 s |
| `contoh/02-silverwolf.wav` | Hasil RVC +9 dari berkas 01 (sebelum tuning ulang) |
| `contoh/03-silverwolf-lengkap.wav` | Kalimat penuh, RVC +9, 7,20 s (sebelum tuning ulang) |
| `contoh/04-transpose0.wav` | Kalimat penuh, RVC 0, 7,32 s (pembanding) |
| **`contoh/04-transpose-3.wav`** | **ACUAN PROFIL TERKUNCI** — RVC -3, 7,18 s, F0 218,8 Hz |
| `contoh/05-verifikasi-profil.wav` | Dijalankan tanpa argumen apa pun; membuktikan `.env` terbaca (F0 220,1 Hz) |

Bukti bahwa konversi benar-benar terjadi (bukan sekadar ganti laju):
jarak MFCC relatif Piper→RVC = **0,224**, centroid spektral 1.512 → 3.432 Hz.

## Yang belum dikerjakan

- **Integrasi ke aplikasi WinUI 3.** Belum ada `SilverWolf.TtsWorker`, belum ada
  pemutaran NAudio, dan `CompanionViewModel` belum memanggil TTS setelah balasan.
  Selama ini belum dikerjakan karena **aplikasi masih mati senyap** (lihat
  `docs/LAPORAN-MASALAH.md` §1.1 dan `tools/bukti/mati-saat-inferensi-2026-10-08.log`).
- **LipSync** (`ParamMouthOpenY`) belum ada; butuh amplitudo audio per bingkai.
- **`HealthSnapshot.Tts` masih di-hardcode `"siap"`** di `CompanionBackend`, jadi
  UI menyatakan TTS siap padahal jalur ini belum tersambung ke aplikasi.
- **Cache** (`VTUBER_TTS_CACHE=ya` di `.env`) belum diterapkan di jalur baru.
