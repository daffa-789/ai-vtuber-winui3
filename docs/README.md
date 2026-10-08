# Indeks Dokumentasi — Silver Wolf

Pintu masuk ke seluruh dokumentasi proyek. Kalau Anda baru membuka proyek ini,
**baca dokumen ini dulu**, lalu ikuti salah satu jalur di bawah.

**Dokumen tunggal proyek adalah `PROYEK.md`.** Segala konteks, keputusan,
perintah, jebakan, dan riwayat ada di sana. Dokumen lain adalah pelengkap yang
punya satu tugas masing-masing — bukan salinan.

---

## Jalur membaca sesuai kebutuhan

| Kalau Anda mau… | Baca |
|---|---|
| Tahu seluruh konteks proyek | **`PROYEK.md`** (dokumen tunggal) |
| Melanjutkan pekerjaan dari sesi AI sebelumnya | **`PROYEK.md` §0** — blok siap tempel |
| Tahu apa yang masih rusak dan seberapa parah | **`LAPORAN-MASALAH.md`** |
| Menyetel rendering/anima agar tetap ringan | **`PERFORMA.md`** — angka terukur mesin ini |
| Membangun, menjalankan, atau menguji | `README.md` (akar) + **`PROYEK.md` §4** |
| Mengerjakan suara (TTS + RVC) | **`../tools/tts/README.md`** |
| Mengerjakan fitur yang dipindah dari aplikasi web | `migrasi/01-peta-fitur.md` → `02-dependensi.md` → `03-langkah-migrasi.md` |
| Mencari riwayat lama | `arsip/` — **arsip, jangan dipakai sebagai acuan** |

---

## Dokumen aktif

Semuanya di `docs/`, kecuali yang ditandai lain.

| Dokumen | Tugas | Diperbarui saat |
|---|---|---|
| **`PROYEK.md`** | **Dokumen tunggal.** Status milestone, perintah, angka golden, kontrak perilaku, seluruh masalah nyata + penyelesaiannya, cacat terbuka, alat diagnosis, jebakan toolchain & Cubism 5, konvensi, peta migrasi, riwayat revisi | Ada perubahan besar / milestone selesai |
| `LAPORAN-MASALAH.md` | Prioritas: apa yang rusak, tingkat, sebab, dampak, dan urutan tindak lanjut | Ada cacat baru atau cacat tertutup |
| `PERFORMA.md` | Angka **terukur** render/anima/suara di mesin Master + tips + sisa penghematan | Setelan performa berubah / ada pengukuran baru |
| `README.md` *(akar repo)* | Orientasi cepat: prasyarat, struktur folder, cara build & jalan, keputusan arsitektur, batasan | Struktur atau prasyarat berubah |
| `../tools/tts/README.md` | **Rantai suara**: cara pakai, aset, profil suara terkunci, angka tuning, latensi, jebakan | Parameter suara berubah |
| `migrasi/01-peta-fitur.md` | Pemetaan layar/panel/state/endpoint → padanan C#, fitur yang tidak bisa dipindah 1:1 | Ada keputusan pemetaan baru |
| `migrasi/02-dependensi.md` | Penggantian/penghapusan dependensi pihak ketiga, alasan `APPX1101`, batasan platform | Ada dependensi baru |
| `migrasi/03-langkah-migrasi.md` | Langkah per proyek + status + blocker | Milestone migrasi bergerak |
| `../assets/fonts/README.md` | Asal-usul & lisensi font yang dibundel | Font berubah |

---

## Batas yang harus dihormati

### `arsip/` — arsip, bukan acuan

Berisi dokumen lama (`dokumen-lama/`), memori pengembangan aplikasi web
(`memori-web/`), dan **sumber lengkap aplikasi web lama** (`sumber-web/`).
Isinya **tidak diperbarui** dan bisa bertentangan dengan kenyataan sekarang.

Beberapa catatan lama sudah terbukti keliru dan **jangan dijadikan dasar** —
lihat daftar koreksinya di `PROYEK.md` §17 dan `LAPORAN-MASALAH.md` §2.2.
Arsip tetap disimpan karena berguna sebagai pembanding perilaku dan sumber
angka golden.

### `../silver_wolf_memory/` — BUKAN dokumentasi proyek

Folder itu adalah **memori karakter** (data runtime yang dibaca-tulis aplikasi
lewat `CharacterVault`), bukan catatan kerja. Aturannya ada di
`../silver_wolf_memory/_PETUNJUK.md` dan tidak boleh dilanggar dari sini:

- **Jangan menaut** apa pun dari dokumentasi proyek ke folder itu, dan
  sebaliknya. Satu tautan saja cukup menyeret seluruh folder waifu ke graph
  proyek — dan itu pernah terjadi.
- **Jangan mengonsolidasi, memindahkan, mengganti nama, atau menghapus** berkas
  di sana saat merapikan dokumentasi kerja.
- Isinya fakta nyata tentang Master dan folder itu di-`gitignore` seluruhnya.

Karena itu **tidak ada satu pun tautan** dari indeks ini ke sana. Itu disengaja.

### `../.workbuddy-ai/memory/` — catatan kerja sesi AI

Log harian dan catatan jangka panjang untuk sesi AI. Berguna untuk kesinambungan,
tetapi **bukan acuan proyek** — kalau bertentangan dengan `PROYEK.md`, yang
menang adalah `PROYEK.md`.

---

## Aturan menjaga dokumen tetap satu kesatuan

1. **Satu fakta, satu tempat.** Jangan menyalin isi `PROYEK.md` ke dokumen lain.
   Taut saja. Kalau sebuah angka berubah, ubah di tempat asalnya lalu pastikan
   rujukannya masih benar.
2. **Setiap dokumen punya satu tugas.** Kalau sebuah dokumen mulai menjawab
   pertanyaan yang bukan tugasnya, pindahkan jawabannya ke dokumen yang tepat.
3. **Sebutkan dokumen lain dengan namanya.** Saat menulis tentang sesuatu yang
   dijelaskan di dokumen lain, tulis jalurnya secara eksplisit (mis.
   `` `tools/tts/README.md` ``) supaya bisa ditemukan.
4. **Tandai arsip sebagai arsip.** Jangan hapus dokumen lama; pindahkan ke
   `arsip/` dan pastikan tidak ada dokumen aktif yang masih merujuknya sebagai
   acuan.
5. **Kalau menemukan dokumen lama yang keliru, koreksi di tempat yang berlaku**
   dan catat koreksinya — jangan diamkan. Beberapa kekeliruan di proyek ini
   bertahan lama karena tidak ada yang mencatat bantahannya.

---

## Riwayat indeks ini

| Tanggal | Perubahan |
|---|---|
| 2026-10-08 | Dibuat. Menyatukan peta seluruh `.md` proyek, menetapkan batas arsip dan pulau `silver_wolf_memory/`, dan mencatat aturan agar dokumen tetap saling terhubung |
