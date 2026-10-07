#!/usr/bin/env python3
"""Terjemahkan RVA dari crash.log menjadi nama fungsi memakai .map hasil taut.

Kenapa ini ada: penangkap pengecualian di Live2DStage.cpp mencatat RVA saat
terjadi access violation. Kalau alamat kesalahannya di luar semua modul
(penunjuk fungsi rusak), hanya alamat-alamat kembali di tumpukan yang bisa
menunjukkan di mana kejadiannya. RVA itu tidak berguna tanpa tabel simbol —
berkas .map yang dihasilkan `link -MAP:` menyediakan tabel itu.

Pakai:
    python rva-lookup.py <berkas.map> <rva_hex> [<rva_hex> ...]

Contoh:
    python rva-lookup.py build/x64/Debug/SilverWolf.Live2D.map 0x1a2b0
"""

import re
import sys

# Baris tabel di .map MSVC, misalnya:
#   0001:0001a2b0       ?Initialize@CubismRenderer_D3D11@@...  000000018001b2b0     obj:file.obj
BARIS = re.compile(
    r"^\s+([0-9a-fA-F]{4}):([0-9a-fA-F]{8})\s+(\S+)\s+([0-9a-fA-F]{8,16})\s+(\S+)"
)
PREFERRED = re.compile(
    r"Preferred load address is\s+([0-9a-fA-F]+)", re.IGNORECASE
)


def baca_map(jalur):
    """Kembalikan (preferred_base, [(alamat_absolut, simbol, objek), ...])."""
    dasar = 0
    entri = []

    with open(jalur, "r", encoding="utf-8", errors="replace") as berkas:
        for baris in berkas:
            cocok_dasar = PREFERRED.search(baris)
            if cocok_dasar:
                dasar = int(cocok_dasar.group(1), 16)
                continue

            cocok = BARIS.match(baris)
            if cocok:
                simbol = cocok.group(3)
                absolut = int(cocok.group(4), 16)
                objek = cocok.group(5)
                entri.append((absolut, simbol, objek))

    entri.sort(key=lambda e: e[0])
    return dasar, entri


def cari(entri, alamat):
    """Entri terakhir yang alamatnya <= alamat (pencarian biner)."""
    rendah, tinggi = 0, len(entri) - 1
    hasil = None
    while rendah <= tinggi:
        tengah = (rendah + tinggi) // 2
        if entri[tengah][0] <= alamat:
            hasil = entri[tengah]
            rendah = tengah + 1
        else:
            tinggi = tengah - 1
    return hasil


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    jalur_map = sys.argv[1]
    dasar, entri = baca_map(jalur_map)
    if not entri:
        print(f"tidak ada simbol terbaca dari {jalur_map}")
        return 1

    print(f"Preferred load address : 0x{dasar:X}")
    print(f"Jumlah simbol          : {len(entri)}")
    print()

    for argumen in sys.argv[2:]:
        rva = int(argumen, 16)
        absolut = dasar + rva
        temuan = cari(entri, absolut)

        print(f"RVA 0x{rva:X}  (absolut 0x{absolut:X})")
        if temuan is None:
            print("  -> di bawah simbol pertama; kemungkinan di luar .text")
        else:
            alamat, simbol, objek = temuan
            print(f"  -> {simbol}")
            print(f"     +0x{absolut - alamat:X} dari awal simbol")
            print(f"     {objek}")
        print()

    return 0


if __name__ == "__main__":
    sys.exit(main())
