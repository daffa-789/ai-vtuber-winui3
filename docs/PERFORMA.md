# Performa rendering & animasi — angka nyata mesin ini

Semua angka di sini **terukur di mesin Master** (Iris Xe, 16 GB, tanpa GPU
diskrit), bukan teori umum. Angka yang tidak diukur ditandai jelas.

> **Aturan pengukuran proyek ini:** "angka yang nggak diukur bukan hasil, itu
> feeling." Jangan ganti nilai apa pun di dokumen ini tanpa mengukur ulang dan
> menulis hasilnya di sini.

---

## 1. Kondisi mesin (terukur 2026-10-09)

| | Nilai |
|---|---|
| GPU | Intel Iris Xe **terintegrasi** — VRAM = RAM sistem |
| RAM total | 15,8 GB |
| RAM bebas saat idle | ~3,7 GB (sebelum penurunan `.env`), **~4,3 GB** sesudah |
| Model bahasa | `gemma-4-E4B-it-UD-Q4_K_XL.gguf`, **5,12 GB** |
| Waktu muat model | 38–45 detik |

**Konsekuensi paling penting:** karena VRAM berbagi RAM, **setiap MB yang
dipakai renderer mengurangi jatah model bahasa.** Di mesin ini, menghemat memori
renderer **langsung** mengurangi resiko Mode B (memori habis).

---

## 2. Biaya render Live2D (terukur)

| Item | Nilai | Sumber |
|---|---|---|
| Ukuran bingkai | **641 × 851 px** | probe `crash.log` |
| Irama render | **~30 fps** (interval 33 ms) | `StageView.xaml.cs:169` |
| Kanvas model | 0,6 × 0,4 (aspek 1,5:1) | `crash.log` |
| Bagian model | **202 part** | `silverwolf.cdi3.json` |
| Parameter | **358** | `silverwolf.cdi3.json` |
| Tekstur | **6 halaman 4096×4096** | `tekstur/` |

**Interpretasi:** 6 atlas 4096² = **~100 MB tekstur** (RGBA) bila semuanya
dimuat. Ini bagian terbesar dari jejak memori renderer. Model 202 part itu
detail tinggi; biaya utamanya ada di **overdraw** (bagian yang saling
menutupi), bukan jumlah part.

### Sudah dilakukan (jangan diubah tanpa alasan)

- **30 fps, bukan 60.** `DispatcherQueueTimer` 33 ms dipilih sengaja, dengan
  komentar di kode: "jauh lebih hemat daripada `CompositionTarget.Rendering`
  yang menembak tiap bingkai UI." Live2D tidak butuh lebih — gerakannya
  halus di 30 fps.
- **Satu `SwapChainPanel`**, satu renderer D3D11. Tidak ada renderer ganda.
- **Throttle saat tidak fokus** — `VITE_IRAMA_FPS_SAAT_TAK_FOKUS="30"`.
  Naikkan penghematan dengan menurunkannya ke `15` kalau Master sering
  meninggalkan jendela di latar belakang.

---

## 3. Biaya yang berjalan terus (terukur)

| Timer | Interval | Fungsi | Beban |
|---|---|---|---|
| Render | 33 ms | gambar bingkai | ~30 gambar/detik |
| Health | **2000 ms** (mulai/< 8000 ms saat siap) | cek llama-server | ringan |
| Proaktif | **5000 ms** | pancing obrolan | ringan |

**Yang sudah benar:** interval health **naik ke 8000 ms** begitu server siap
(`CompanionViewModel.cs:767`). Itu tepat — tidak perlu mengecek tiap 2 detik
server yang sudah sehat.

**Poin perhatian:** render 33 ms tetap jalan walaupun model benar-benar diam.
Untuk model 202 part di GPU terintegrasi, ini beban konstan. Kalau Master butuh
baterai/CPU untuk hal lain, **menghentikan `_timerRender` saat jendela
di-minimize** adalah penghematan terbesar yang tersisa (belum dikerjakan).

---

## 4. Biaya suara (TTS) — terukur

| Tahap | Terukur |
|---|---|
| Piper (teks → wav) | cepat, bagian kecil dari total |
| **RVC (v2, CPU)** | **RTF 1,1–2,5× realtime** |
| Contoh nyata | kalimat 4,16 detik → wav 4,16 detik, tapi proses ~5–10 detik |
| Perangkat RVC | `cpu` (terpaksa — tidak ada GPU NVIDIA) |

**Artinya:** suara **tidak mungkin realtime**. Kalimat 5 detik butuh 6–12 detik
diproses. Karena itu:

- **Dipecah per kalimat** (`VTUBER_TTS_PER_KALIMAT=true`) → kalimat pertama bisa
  diputar sementara berikutnya masih diproses. Persepsi latensi turun drastis.
- **`VTUBER_TTS_BATAS_DETIK=40`** — RVC yang macet tidak menggantung antrean
  selamanya.
- **`VTUBER_TTS_CACHE=ya`** — kalimat berulang tidak dihitung ulang. Kunci cache
  memuat **teks + seluruh parameter suara**, jadi mengubah transpose tetap
  menghasilkan wav baru (bukan bug "perubahan tidak berefek").

**Kalau suara terasa terlalu lambat**, urutan tindakan dari termurah:

1. **Matikan RVC** (`VTUBER_RVC=tidak`) → hanya Piper. Suara jadi suara Piper,
   bukan Silver Wolf, tapi **hampir instan**. Pakai untuk menguji alur.
2. Biarkan cache bekerja — sapaan berulang ("halo", "iya") jadi instan.
3. RVC di CPU tidak bisa dipercepat tanpa GPU NVIDIA. Jangan buang waktu
   menyetel `-ngl` untuk RVC; itu untuk model bahasa, bukan RVC.

---

## 5. Tips yang SUDAH diterapkan (jangan diutak-atik)

| Setelan | Nilai | Kenapa |
|---|---|---|
| `VTUBER_VULKAN_CTX` | **4096** | cocok dengan `VTUBER_LOCAL_MODEL_CTX=4096`. Nilai 16384 lama menahan **~2,3 GB** KV cache f16 tanpa manfaat → memicu Mode B |
| `VTUBER_VULKAN_NGL` | **40** | 40 (bukan 99) supaya tidak berebut dengan D3D11 Cubism. **`0` BUKAN penghemat memori** — justru membebani CPU |
| `VTUBER_LOCAL_MODEL_THREADS` | **4** | CPU ini punya sedikit inti performa; 4 thread menghindari pertengkaran dengan renderer |
| `PublishTrimmed` | **False** | WinUI 3 + Cubism tidak selamat dari trimming |
| Rilis | **x64 saja** | tidak ada alasan membangun ARM/x86 |

---

## 6. Sisa penghematan (belum dikerjakan, diurut dari termudah)

1. **Hentikan render saat minimize.** Menyimpan `_timerRender` dan
   `Stop()`/`Start()` dari event `Window.Activated`/visibility. Penghematan
   terbesar yang paling murah — render berhenti total saat tidak terlihat.
2. **Turunkan `VITE_IRAMA_FPS_SAAT_TAK_FOKUS` ke `15`.** Sudah ada mekanismenya,
   cuma tinggal ubah angkanya.
3. **`VTUBER_VULKAN_CACHE_TYPE=q8_0`.** f16 → q8_0 memangkas cache KV separuh.
   Risiko: mutu jawaban bisa turun tipis. Ukur sebelum dan sesudah.
4. **Kurangi overdraw model.** 202 part itu detail tinggi; kalau Master tidak
   memakai efek `Part196 | 银狼+特效` atau `Part38 | 划卡特效`, mematikannya
   menghapus beberapa lapisan transparan besar. **Jangan lakukan tanpa
   persetujuan** — ini mengubah tampilan.

---

## 7. Cara mengukur ulang (jangan menebak)

```bash
# Frame & kotak model (probe renderer)
grep -a "periksa piksel" crash.log

# Memori bebas terendah selama uji
"$PY" tools/uji-kematian.py 5 60

# Apakah suara benar-benar keluar (RMS > 200 = ada suara)
"$PY" -c "import wave,struct,math;w=wave.open('uji.wav');n=w.getnframes();s=struct.unpack('<%dh'%n,w.readframes(n));print('RMS',round(math.sqrt(sum(x*x for x in s)/len(s)),1))"
```

**Peringatan:** `audioop` **tidak ada** di Python 3.13 — pakai `struct.unpack`
manual seperti di atas. Dan probe piksel **≠** yang dilihat mata; untuk keputusan
visual, ukur tangkapan layar Master dengan PIL.
