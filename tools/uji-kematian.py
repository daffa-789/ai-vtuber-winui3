#!/usr/bin/env python3
"""Uji kematian berulang - docs/PROYEK.md §8.1 dan §9.

Menjalankan aplikasi berkali-kali, menahan tiap jalan beberapa detik, lalu
menyimpan `crash.log` tiap jalan ke `tools/bukti/`. Log kematian itu justru
buktinya - jangan dihapus (pelajaran §7.19).

Versi 2 (2026-10-08) - kenapa ditulis ulang dari versi bash:

* **Exit code proses dicatat utuh 32 bit.** Bash cuma mengembalikan 8 bit
  bawah, jadi `0xC0000602` (STATUS_FAIL_FAST_EXCEPTION) terbaca sebagai `2`
  dan tidak bisa dibedakan dari "keluar bersih". Dua jalan yang pernah
  berakhir `shutdown: selesai` tanpa exception (jalan-1, jalan-3) tidak bisa
  diklasifikasikan hanya dari log; exit code menyelesaikan ambiguitas itu.
* **Memori fisik bebas disampel selama jalan** dan nilai terendahnya disimpan.
  Dugaan utama sejak 8 Okt adalah tekanan memori bersama (GPU terintegrasi
  memakai RAM sistem), jadi angka ini perlu, bukan sekadar pelengkap.
* **Nasib `llama-server` ikut dicatat.** Kalau dia ikut mati bersama aplikasi,
  itu menguatkan dugaan reset driver; kalau hanya aplikasi yang mati, bukan.

Pakai:
    python tools/uji-kematian.py [jumlah-jalan] [detik-tahan]
Contoh:
    python tools/uji-kematian.py 10 130
"""

import ctypes
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(
    ROOT, "src", "SilverWolf.App", "bin", "x64", "Debug",
    "net8.0-windows10.0.19041.0", "win-x64")
EXE = os.path.join(TARGET, "SilverWolf.App.exe")
LOGDIR = os.path.join(ROOT, "tools", "bukti")

# Exit code yang sering muncul pada mati senyap. Nilai diambil apa adanya
# dari GetExitCodeProcess (signed 32 bit).
KODE_KELUAR = {
    0: "keluar-bersih",
    -1073740286: "FAIL_FAST_EXCEPTION (0xC0000602)",
    -1073740791: "STACK_BUFFER_OVERRUN / fail-fast (0xC0000409)",
    -1073741819: "ACCESS_VIOLATION (0xC0000005)",
    -1073741676: "ILLEGAL_INSTRUCTION (0xC000001D)",
    -1073741571: "STACK_OVERFLOW (0xC00000FD)",
    -532459699: ".NET unhandled exception (0xE0434352)",
    -532462766: ".NET FailFast (0xE0434C4D / COR_E_FAILFAST)",
    1: "keluar 1 (biasanya exception tak tertangani)",
}


class MemoriSistem(ctypes.Structure):
    _fields_ = [
        ("dwLength", ctypes.c_ulong),
        ("dwMemoryLoad", ctypes.c_ulong),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


def memori_bebas_mb():
    """Memori fisik yang masih bebas, dalam MiB."""
    m = MemoriSistem()
    m.dwLength = ctypes.sizeof(MemoriSistem)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m)):
        return None
    return m.ullAvailPhys // (1024 * 1024)


def bunuh(nama):
    subprocess.run(["taskkill", "/F", "/IM", nama],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def hidup(nama):
    r = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {nama}"],
                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                       text=True, encoding="utf-8", errors="replace")
    return r.stdout.count(nama)


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 10
    detik = int(sys.argv[2]) if len(sys.argv) > 2 else 130

    os.makedirs(LOGDIR, exist_ok=True)
    if not os.path.isfile(EXE):
        print(f"GAGAL: {EXE} tidak ada. Bangun dulu (docs/PROYEK.md §4).")
        return 2

    print(f"Menjalankan {n} x {detik} dtk. Log tiap jalan -> {LOGDIR}")
    print("-" * 78)

    selamat = 0
    mati = 0
    baris = []

    for jalan in range(1, n + 1):
        bunuh("SilverWolf.App.exe")
        bunuh("llama-server.exe")
        time.sleep(5)

        for bekas in ("crash.log", "run.out"):
            p = os.path.join(TARGET, bekas)
            if os.path.exists(p):
                os.remove(p)

        run_out = open(os.path.join(TARGET, "run.out"), "w", encoding="utf-8")
        mulai = time.time()
        proc = subprocess.Popen([EXE], cwd=TARGET,
                                stdout=run_out, stderr=subprocess.STDOUT)

        mem_terendah = None
        kode = None
        while True:
            kode = proc.poll()
            if kode is not None:
                break
            m = memori_bebas_mb()
            if m is not None and (mem_terendah is None or m < mem_terendah):
                mem_terendah = m
            if time.time() - mulai >= detik:
                break
            time.sleep(2)

        lama = time.time() - mulai

        if kode is None:
            proc.kill()
            proc.wait()
            status = f"SELAMAT {detik}dtk"
            selamat += 1
        else:
            status = f"MATI pada {lama:.0f}dtk"
            mati += 1
        run_out.close()

        sumber = os.path.join(TARGET, "crash.log")
        tujuan = os.path.join(LOGDIR, f"jalan-{jalan}.log")
        if os.path.exists(sumber):
            shutil.copyfile(sumber, tujuan)
            teks = open(sumber, encoding="utf-8", errors="replace").read()
        else:
            teks = ""

        # Kelas penyebab: exit code dulu, baru isi log.
        #
        # Pemisah yang paling penting (ditemukan 2026-10-08): `swl2d_shutdown`
        # HANYA dipanggil dari `MainWindow.OnClosed`. Jadi kalau log memuat
        # baris `shutdown: selesai`, jendelanya memang DITUTUP - proses keluar
        # teratur dengan exit 0, dan ini BUKAN kematian yang dicari §8.1.
        # Tanpa pemisah ini, jendela yang ditutup pengguna terus terhitung
        # sebagai crash dan mengacaukan laju kejadian.
        ditutup = "shutdown: selesai" in teks
        if kode is None:
            kelas = "masih-hidup"
        elif ditutup:
            kelas = "DITUTUP-bersih(bukan-mati)"
        elif kode == 0:
            kelas = "MATI-tanpa-jejak(0)"
        else:
            kelas = KODE_KELUAR.get(kode, f"kode-{kode}")

        if "DisposeAsync" in teks:
            kelas += " +jejak-DisposeAsync"
        if "tipe    :" in teks:
            kelas += " +exception-tercatat"

        # Di mana log berhenti - penanda terakhir yang paling berguna.
        jejak = ""
        for pola in ("[llama]", "[swl2d]", "TAHAP: runtime:", "TAHAP: live2d:"):
            if pola in teks:
                jejak = pola
        llama = hidup("llama-server.exe")

        nama_kode = "-" if kode is None else str(kode)
        ringkas = (f"jalan {jalan}/{n}: {status:20s} | kode={nama_kode:>12s} | "
                   f"RAM bebas min={mem_terendah}MiB | llama={llama} | "
                   f"{kelas} | log berakhir: {jejak or '-'}")
        print(ringkas, flush=True)
        baris.append(ringkas)

        with open(os.path.join(LOGDIR, "ringkasan-uji.txt"), "w",
                  encoding="utf-8") as f:
            f.write(f"waktu: {datetime.now().isoformat(timespec='seconds')}\n")
            f.write(f"konfigurasi: {n} jalan x {detik} dtk\n")
            f.write("\n".join(baris) + "\n")
            f.write("-" * 78 + "\n")
            f.write(f"HASIL: selamat={selamat} mati={mati} dari {n} jalan\n")

    bunuh("SilverWolf.App.exe")
    bunuh("llama-server.exe")

    print("-" * 78)
    print(f"HASIL: selamat={selamat} mati={mati} dari {n} jalan")
    print(f"Ringkasan: {os.path.join(LOGDIR, 'ringkasan-uji.txt')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
