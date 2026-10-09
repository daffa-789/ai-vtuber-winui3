#!/usr/bin/env python3
"""Uji kematian berulang - docs/PROYEK.md §8.1 dan §9.

Menjalankan aplikasi berkali-kali, menahan tiap jalan beberapa detik, lalu
menyimpan `crash.log` tiap jalan ke `tools/bukti/`. Log kematian itu justru
buktinya - jangan dihapus (pelajaran §7.19).

Versi 3 (2026-10-09) - yang ditambahkan, semuanya demi satu pertanyaan:
**siapa yang mengakhiri prosesnya?**

* **Penanda `PROSES KELUAR` dibaca.** Ini pemisah yang paling menentukan dan
  sebelumnya terlewat. `AppDomain.ProcessExit` dipasang di `CrashLog.Pasang()`,
  jadi:
      ada `PROSES KELUAR`   -> CLR berhenti sendiri (Main kembali / Environment.Exit)
      tidak ada             -> proses diakhiri dari LUAR CLR (TerminateProcess)
  Exit code 0 pada kedua kasus terlihat sama, jadi tanpa penanda ini
  "keluar bersih" dan "dibunuh dari luar" tidak bisa dibedakan.
* **Baris-baris terakhir `crash.log` dicatat apa adanya.** Sebelumnya hanya
  dicocokkan pola kasar; titik matinya justru yang paling berharga.
* **Ukuran akhir `crash.log` dicatat.** Mode A punya tanda tangan khas:
  beku di **13.449 bita**.
* **Working set puncak aplikasi disampel** (ctypes, tanpa psutil) - Mode B
  adalah soal memori, dan angka prosesnya lebih tajam daripada memori sistem.
* **Memori sistem disampel tiap 1 dtk**, bukan 2, supaya lembahnya tertangkap.
* **llama-server pernah hidup atau tidak** ikut dicatat. Mode C mati saat Vulkan
  bekerja; kalau llama tidak pernah menyala sama sekali, itu petunjuk lain.
* **Ringkasan per jalan disimpan sebagai JSON** supaya bisa dihitung ulang
  tanpa menjalankan ulang uji.

Pakai:
    python tools/uji-kematian.py [jumlah-jalan] [detik-tahan]
Contoh:
    python tools/uji-kematian.py 10 130
"""

import ctypes
import json
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

# Ukuran crash.log tempat Mode A membeku (docs/PROYEK.md §8.1).
UKURAN_MODE_A = 13449

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


class PenghitungMemori(ctypes.Structure):
    _fields_ = [
        ("cb", ctypes.c_ulong),
        ("PageFaultCount", ctypes.c_ulong),
        ("PeakWorkingSetSize", ctypes.c_size_t),
        ("WorkingSetSize", ctypes.c_size_t),
        ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
        ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
        ("PagefileUsage", ctypes.c_size_t),
        ("PeakPagefileUsage", ctypes.c_size_t),
    ]


def memori_bebas_mb():
    """Memori fisik yang masih bebas, dalam MiB."""
    m = MemoriSistem()
    m.dwLength = ctypes.sizeof(MemoriSistem)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m)):
        return None
    return m.ullAvailPhys // (1024 * 1024)


def working_set_mb(pid):
    """Working set proses dalam MiB, atau None kalau tidak terbaca."""
    if pid is None:
        return None

    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    handle = ctypes.windll.kernel32.OpenProcess(
        PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        return None

    try:
        c = PenghitungMemori()
        c.cb = ctypes.sizeof(PenghitungMemori)
        if not ctypes.windll.psapi.GetProcessMemoryInfo(
                handle, ctypes.byref(c), c.cb):
            return None
        return c.WorkingSetSize // (1024 * 1024)
    finally:
        ctypes.windll.kernel32.CloseHandle(handle)


def bunuh(nama):
    subprocess.run(["taskkill", "/F", "/IM", nama],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def hidup(nama):
    r = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {nama}"],
                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                       text=True, encoding="utf-8", errors="replace")
    return r.stdout.count(nama)


def baris_terakhir(teks, jumlah=3):
    """Beberapa baris 'sumber  : ...' terakhir dari crash.log."""
    hasil = [b.strip() for b in teks.splitlines() if b.strip()]
    return hasil[-jumlah:]


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
    rincian = []

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
        ws_puncak = None
        llama_pernah = False
        kode = None
        while True:
            kode = proc.poll()
            if kode is not None:
                break

            m = memori_bebas_mb()
            if m is not None and (mem_terendah is None or m < mem_terendah):
                mem_terendah = m

            ws = working_set_mb(proc.pid)
            if ws is not None and (ws_puncak is None or ws > ws_puncak):
                ws_puncak = ws

            if not llama_pernah and hidup("llama-server.exe"):
                llama_pernah = True

            if time.time() - mulai >= detik:
                break
            time.sleep(1)

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
            ukuran = os.path.getsize(sumber)
        else:
            teks = ""
            ukuran = 0

        # ── Kelas penyebab ────────────────────────────────────────────────
        #
        # Pemisah terpenting. `swl2d_shutdown` HANYA dipanggil dari
        # `MainWindow.OnClosed`, jadi `shutdown: selesai` berarti jendelanya
        # memang DITUTUP - exit 0 yang teratur, BUKAN kematian §8.1.
        ditutup = "shutdown: selesai" in teks
        # `PROSES KELUAR` ditulis AppDomain.ProcessExit. Ada = CLR berhenti
        # sendiri; tidak ada = diakhiri dari LUAR CLR.
        keluar_clr = "PROSES KELUAR" in teks

        if kode is None:
            kelas = "masih-hidup"
        elif ditutup:
            kelas = "DITUTUP-bersih(bukan-mati)"
        elif keluar_clr:
            kelas = "MATI-tapi-CLR-berhenti-sendiri"
        elif kode == 0:
            kelas = "MATI-dari-LUAR-CLR(0)"
        else:
            kelas = KODE_KELUAR.get(kode, f"kode-{kode}")

        if "DisposeAsync" in teks:
            kelas += " +jejak-DisposeAsync"
        if "tipe    :" in teks:
            kelas += " +exception-tercatat"
        if ukuran == UKURAN_MODE_A:
            kelas += " +TANDA-MODE-A"

        akhir = baris_terakhir(teks)
        jejak = " | ".join(a.replace("sumber  : ", "")[:58] for a in akhir)

        nama_kode = "-" if kode is None else str(kode)
        ringkas = (f"jalan {jalan}/{n}: {status:20s} | kode={nama_kode:>12s} | "
                   f"RAM min={mem_terendah}MiB | ws puncak={ws_puncak}MiB | "
                   f"llama={'pernah' if llama_pernah else 'tidak'} | "
                   f"log={ukuran}B | {kelas}")
        print(ringkas, flush=True)
        print(f"           akhir: {jejak or '-'}", flush=True)
        baris.append(ringkas)

        rincian.append({
            "jalan": jalan,
            "status": status,
            "kode_keluar": kode,
            "lama_detik": round(lama, 1),
            "ram_bebas_terendah_mib": mem_terendah,
            "working_set_puncak_mib": ws_puncak,
            "llama_pernah_hidup": llama_pernah,
            "ukuran_crash_log": ukuran,
            "ditutup_bersih": ditutup,
            "proses_keluar_clr": keluar_clr,
            "kelas": kelas,
            "baris_terakhir": akhir,
        })

        with open(os.path.join(LOGDIR, "ringkasan-uji.txt"), "w",
                  encoding="utf-8") as f:
            f.write(f"waktu: {datetime.now().isoformat(timespec='seconds')}\n")
            f.write(f"konfigurasi: {n} jalan x {detik} dtk\n")
            f.write("\n".join(baris) + "\n")
            f.write("-" * 78 + "\n")
            f.write(f"HASIL: selamat={selamat} mati={mati} dari {n} jalan\n")

        with open(os.path.join(LOGDIR, "ringkasan-uji.json"), "w",
                  encoding="utf-8") as f:
            json.dump(rincian, f, indent=2, ensure_ascii=False)

    bunuh("SilverWolf.App.exe")
    bunuh("llama-server.exe")

    print("-" * 78)
    print(f"HASIL: selamat={selamat} mati={mati} dari {n} jalan")
    print(f"Ringkasan: {os.path.join(LOGDIR, 'ringkasan-uji.txt')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
