#!/usr/bin/env python3
"""Probe satu jalan - menyingkap APA yang sebenarnya mengakhiri proses.

Dipakai buat §8.1 (docs/PROYEK.md). Yang dicari:

* **exit code utuh 32 bit** - `0` berarti keluar bersih, BUKAN fail-fast.
  (0xC0000602 = fail-fast, 0xC0000005 = access violation, dst.)
* **apakah proses menggantung sebelum mati** - `tasklist /V` melaporkan status
  `Running` vs `Not Responding`. Kalau log berhenti 15 detik sebelum proses
  keluar, itu menggantung, bukan mati mendadak.
* **kapan `crash.log` terakhir ditulis** dibanding kapan proses keluar -
  selisihnya adalah lama menggantung.
* **nasib `llama-server`** - ikut mati atau tidak.

Pakai:
    python tools/probe-keluar.py [detik-maks]
"""

import os
import subprocess
import sys
import time
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(
    ROOT, "src", "SilverWolf.App", "bin", "x64", "Debug",
    "net8.0-windows10.0.19041.0", "win-x64")
EXE = os.path.join(TARGET, "SilverWolf.App.exe")
CRASH = os.path.join(TARGET, "crash.log")


def jalankan(cmd):
    return subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                          text=True, encoding="utf-8", errors="replace").stdout


def status_proses():
    """Baris tasklist /V untuk aplikasi, atau None kalau sudah hilang."""
    out = jalankan(["tasklist", "/V", "/FI", "IMAGENAME eq SilverWolf.App.exe"])
    for baris in out.splitlines():
        if "SilverWolf.App.exe" in baris:
            return " ".join(baris.split())
    return None


def main():
    maks = int(sys.argv[1]) if len(sys.argv) > 1 else 60

    subprocess.run(["taskkill", "/F", "/IM", "SilverWolf.App.exe"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run(["taskkill", "/F", "/IM", "llama-server.exe"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(4)
    if os.path.exists(CRASH):
        os.remove(CRASH)

    t0 = time.time()

    def detik():
        return time.time() - t0

    run_out = open(os.path.join(TARGET, "run.out"), "w", encoding="utf-8")
    proc = subprocess.Popen([EXE], cwd=TARGET, stdout=run_out,
                            stderr=subprocess.STDOUT)

    print(f"jalan pada {datetime.now().isoformat(timespec='seconds')}")
    print(f"{'dtk':>6}  {'status tasklist':<44} {'crash.log':>10}")
    terakhir_log = None
    terakhir_status = None

    while True:
        kode = proc.poll()
        st = status_proses()
        if st is not None:
            terakhir_status = st
        ukuran = os.path.getsize(CRASH) if os.path.exists(CRASH) else 0
        if ukuran:
            terakhir_log = (detik(), ukuran)
        if st is not None:
            print(f"{detik():6.1f}  {(st or '-')[:44]:<44} {ukuran:>10}", flush=True)
        else:
            print(f"{detik():6.1f}  {'-- proses hilang --':<44} {ukuran:>10}", flush=True)
        if kode is not None:
            break
        if detik() >= maks:
            proc.kill()
            proc.wait()
            break
        time.sleep(2)
    run_out.close()

    print("-" * 70)
    print(f"exit code     : {kode}")
    if kode == 0:
        print("                -> KELUAR BERSIH. Bukan fail-fast, bukan exception.")
    print(f"lama hidup    : {detik():.1f} dtk")
    if terakhir_log:
        print(f"log terakhir  : pada {terakhir_log[0]:.1f} dtk "
              f"({detik() - terakhir_log[0]:.1f} dtk sebelum keluar), "
              f"{terakhir_log[1]} bita")
    print(f"status akhir  : {terakhir_status}")
    print(f"llama-server  : {'hidup' if 'llama-server.exe' in jalankan(['tasklist', '/FI', 'IMAGENAME eq llama-server.exe']) else 'MATI'}")

    if os.path.exists(CRASH):
        teks = open(CRASH, encoding="utf-8", errors="replace").read().strip()
        print("--- 6 baris sumber terakhir crash.log ---")
        for b in teks.splitlines()[-6:]:
            print("   ", b)
    return 0


if __name__ == "__main__":
    sys.exit(main())
