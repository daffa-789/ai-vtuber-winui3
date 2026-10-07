# Font — aset yang harus lokal, bukan dari internet

## Kenapa folder ini ada

Aplikasi Electron lama **tidak menyimpan font di dalam proyek**. Ia mengambilnya
dari internet saat runtime, lewat `apps/stage-tamagotchi/renderer/index.html`:

```html
<link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600;700
      &family=Orbitron:wght@600;700;800;900
      &family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
```

Untuk aplikasi desktop yang harus bisa jalan **offline**, ini tidak bisa
dipertahankan. Font harus dibundel bersama aplikasi.

Tiga keluarga yang dipakai, dari `style.css` baris 10–12:

```css
--font-main:    'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
--font-mono:    'JetBrains Mono', 'Fira Code', monospace;
--font-display: 'Orbitron', 'Space Grotesk', var(--font-main);
```

| Berkas | Ukuran | Dipakai untuk |
|---|---|---|
| `PlusJakartaSans.ttf` | 176 KB | `--font-main` — teks isi |
| `JetBrainsMono.ttf` | 184 KB | `--font-mono` — terminal, kode, angka telemetri |
| `Orbitron.ttf` | 40 KB | `--font-display` — judul, label HUD |

## Kenapa hanya 3 berkas, bukan 12

Ketiganya **variable font** — satu berkas memuat seluruh sumbu `wght`. Jadi
`PlusJakartaSans.ttf` mencakup bobot 400/500/600/700 sekaligus, bukan satu
berkas per bobot.

DirectWrite (mesin font di balik WinUI 3) menerapkan instance sumbu secara
otomatis saat `FontWeight` diminta, sehingga `FontWeight="SemiBold"` akan
mengambil bobot 600 dari berkas variable yang sama.

> ⚠️ **Perlu diuji saat M10.** Kalau ternyata WinUI 3 hanya memakai instance
> bawaan dan bobot tidak berubah, jalannya adalah memakai instance statis:
> ambil dari `ofl/<keluarga>/static/` di repo `google/fonts`, atau dari
> rilis resmi tiap keluarga. Jangan diasumsikan berhasil — uji dulu.

## Sumber

Diunduh 2026-10-07 dari repo resmi Google Fonts
(`github.com/google/fonts`, lisensi SIL Open Font License 1.1):

```
ofl/plusjakartasans/PlusJakartaSans[wght].ttf
ofl/jetbrainsmono/JetBrainsMono[wght].ttf
ofl/orbitron/Orbitron[wght].ttf
```

SIL OFL 1.1 mengizinkan pembundelan ke dalam aplikasi, termasuk komersial.

## Catatan cara mengunduh

Endpoint `fonts.googleapis.com/css2` **tidak bisa** dipakai untuk mengambil
berkas font secara langsung:

- Dengan User-Agent modern, Google menyajikan **woff2** — dan WinUI 3
  **tidak mendukung woff2**.
- Dengan User-Agent lama (IE6) untuk memaksa TTF, endpoint
  `fonts.gstatic.com/l/font?kit=…` mengembalikan berkas yang **header-nya bukan
  `00 01 00 00`**, jadi bukan TTF yang sah meskipun isinya memuat nama font.

Yang bekerja: ambil langsung dari repo `google/fonts`. Verifikasi hasilnya dengan
membaca 4 byte pertama — harus `00010000` (TrueType) atau `4f54544f` (OpenType):

```bash
od -An -tx1 -N4 berkas.ttf      # harus 00 01 00 00
```

Folder `referensi/` menyimpan CSS asli dari Google Fonts, sebagai catatan bobot
mana saja yang dipakai aplikasi lama.
