"""Pekerja suara menetap: muat model SEKALI, lalu layani banyak kalimat.

MASALAH YANG DIPERBAIKI
Rantai lama (buat_suara.py) dipanggil sebagai proses Python baru untuk SETIAP
kalimat. Setiap proses harus memuat ulang HuBERT + RVC + rmvpe. Diukur di
mesin ini (4 core, CPU saja):

    piper                     3,9 - 13,6 s
    muat RVC + HuBERT             0,6 - 1,0 s
    inferensi PERTAMA        21,0 - 24,0 s   <-- muat rmvpe di sini
    inferensi kedua dan seterusnya  6,1 - 13,3 s

Jadi setiap kalimat membayar ~20 detik hanya untuk memuat rmvpe. Balasan dua
kalimat menghabiskan ~45 detik dan menembus batas 40 detik
(VTUBER_TTS_BATAS_DETIK), sehingga antrean dibatalkan sebelum satu pun WAV
sampai ke pemutar -> gejalanya "menyiapkan suara..." tanpa henti, tidak ada
suara.

Dengan pekerja menetap, biaya muat dibayar sekali di awal sesi; kalimat
berikutnya hanya ~6-13 detik.

PROTOKOL — LEWAT fd 3, BUKAN stdout
Ini keputusan penting. Pustaka pihak ketiga mencetak langsung ke stdout:
`rvc_python/configs/config.py:93` memanggil `print("overwrite preprocess and
configs.json")`, dan Piper/torch mencetak hal serupa. Semua itu terjadi
SEBELUM kita sempat mengalihkan apa pun, jadi stdout tidak pernah bisa
dipercaya sebagai jalur data.

Maka:
    fd 0 (stdin)   induk -> anak   JSON satu baris per permintaan
    fd 3           anak -> induk   JSON satu baris per peristiwa
    fd 1 (stdout)  dialihkan ke stderr sejak baris pertama; hanya untuk log
    fd 2 (stderr)  log diagnostik

Induk (TtsWorker.cs) membuka fd 3 lewat `ProcessStartInfo` + handle pipe.
Bila fd 3 tidak ada (mis. dijalankan manual dari terminal), pekerja tetap
hidup dan hanya menulis ke stderr — berguna untuk menguji.

    induk -> anak   {"teks":"...","keluar":"C:\\...\\a.wav",
                     "piper":"C:\\...\\piper.exe","tanpa_rvc":false}
                    {"keluar_akhir": true}          <- tutup dengan rapi
    anak  -> induk  {"jenis":"siap"}                          sekali di awal
                    {"jenis":"progres","tahap":"piper"}
                    {"jenis":"selesai","keluar":"...","detik":12.3}
                    {"jenis":"galat","pesan":"..."}

Pemakaian mandiri (untuk menguji tanpa aplikasi):

    echo '{"teks":"halo","keluar":"C:/tmp/a.wav"}' | python pekerja_tts.py
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path

# ── Saluran protokol ────────────────────────────────────────────────────────
# Pustaka pihak ketiga mencetak ke stdout (rvc_python/configs/config.py:93).
# Jadi stdout DIKORBANKAN sebagai jalur log.
#
# Di Windows, "fd 3" tidak ada. Induk membuat pipe BERNAMA dan memberi tahu
# namanya lewat SW_PROTO_PIPE. Kita membukanya sebagai berkas biasa dengan
# mode "r+b" — cara yang didukung langsung oleh Windows.
def _buka_saluran():
    """Buka pipa protokol ke induk. Mengembalikan None kalau gagal.

    Dua hal yang WAJIB benar di sini — keduanya pernah salah dan gejalanya
    sangat menyesatkan (pekerja memuat model dengan benar, lalu induk tidak
    pernah menerima sapaan "siap" sehingga SEMUA kalimat gagal setelah
    menunggu batas waktu penuh):

    1. Jalurnya harus lengkap, ``\\\\.\\pipe\\<nama>``. Tanpa awalan itu
       Windows menganggapnya berkas biasa di direktori kerja, bukan pipa.
    2. Mode buka harus cocok dengan arah pipa. Induk membuat server dengan
       ``PipeDirection.InOut``, jadi O_RDWR aman; O_WRONLY dipakai sebagai
       cadangan kalau arahnya suatu saat diubah.
    """
    nama = os.environ.get("SW_PROTO_PIPE")
    if not nama:
        return None

    jalur = "\\\\.\\pipe\\" + nama
    terakhir = None
    for _ in range(50):  # tunggu ~10 dtk kalau server belum siap
        for mode, bendera in (("r+b", os.O_RDWR), ("wb", os.O_WRONLY)):
            try:
                # buffering=0: setiap baris harus langsung sampai ke induk.
                # os.open dipakai supaya mode bukanya persis seperti yang
                # diminta — open() tingkat tinggi menambahkan O_CREAT/O_TRUNC
                # yang tidak sah untuk pipa.
                return os.fdopen(os.open(jalur, bendera), mode, buffering=0)
            except OSError as galat:
                terakhir = galat
        time.sleep(0.2)

    print(f"[pekerja] pipa protokol gagal dibuka: {jalur} ({terakhir})",
          file=sys.stderr, flush=True)
    return None


_PROTO = _buka_saluran()

# Pindahkan stdout -> stderr supaya print() pustaka tidak mengotori apa pun.
sys.stdout = sys.stderr


def log(pesan: str) -> None:
    print(pesan, file=sys.stderr, flush=True)


AKAR = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))

MODEL_PIPER = AKAR / "assets" / "piper" / "id_ID-news_tts-medium.onnx"
PIPER_EXE_BAWAAN = os.environ.get(
    "SW_PIPER_EXE",
    r"C:\Users\Daffa\.workbuddy-ai\binaries\python\envs\default\Scripts\piper.exe",
)


def kirim(obyek: dict) -> None:
    """Tulis satu baris JSON ke saluran protokol dan paksa keluar segera.

    Tanpa flush, induk akan menunggu tanpa batas karena buffer pipa menahan
    baris terakhir. Ini pernah menjadi bug "TTS tidak pernah selesai".
    """
    baris = json.dumps(obyek, ensure_ascii=False) + "\n"
    if _PROTO is not None:
        try:
            _PROTO.write(baris.encode("utf-8"))
            return
        except (OSError, ValueError):
            # Pipa tertutup (induk sudah pergi). Jatuh ke stderr di bawah.
            pass
    # Cadangan: tulis ke stderr dengan penanda supaya uji manual tetap terbaca.
    print(f"[proto] {baris.rstrip()}", file=sys.stderr, flush=True)


def pasang_patch_torch() -> None:
    """PyTorch 2.6+ mengubah bawaan weights_only menjadi True.

    Checkpoint HuBERT dan RVC ditulis sebelum perubahan itu, jadi tanpa patch
    ini pemuatan gagal dengan UnpicklingError.
    """
    import torch

    _asli = torch.load

    def _patch(*args, **kwargs):
        kwargs.setdefault("weights_only", False)
        return _asli(*args, **kwargs)

    torch.load = _patch


class Pekerja:
    """Menyimpan model di memori dan melayani permintaan satu per satu."""

    def __init__(self) -> None:
        self.piper = PIPER_EXE_BAWAAN
        self.rvc = None
        self.tmp = Path(tempfile.mkdtemp(prefix="sw-pekerja-"))

    def siapkan(self) -> None:
        """Muat model sekali. Dipanggil sebelum loop menerima permintaan."""
        from rvc_konversi import baca_env, profil

        env = baca_env()
        p = profil(env)
        self.panjang = float(env.get("VTUBER_TTS_PIPER_PANJANG", 0.9))
        self.p = p

        if not MODEL_PIPER.is_file():
            raise RuntimeError(f"model Piper tidak ada: {MODEL_PIPER}")
        if not p["model"].is_file():
            raise RuntimeError(f"model RVC tidak ada: {p['model']}")
        if not Path(self.piper).is_file():
            raise RuntimeError(f"piper.exe tidak ada: {self.piper}")

        pasang_patch_torch()

        t = time.time()
        from rvc_python.infer import RVCInference

        self.rvc = RVCInference(
            device="cpu",
            model_path=str(p["model"]),
            index_path=str(p["indeks"]) if p["indeks"] else "",
            version=p["versi"],
        )
        self.rvc.set_params(
            f0up_key=p["transpose"],
            index_rate=p["index_rate"],
            f0method=p["f0"],
            filter_radius=p["filter_radius"],
            rms_mix_rate=p["rms_mix"],
            protect=p["protect"],
            resample_sr=0,
        )
        log(f"[pekerja] model siap dalam {time.time()-t:.1f} s")

    def _piper(self, teks: str, keluar: Path) -> None:
        keluar.parent.mkdir(parents=True, exist_ok=True)
        hasil = subprocess.run(
            [self.piper, "-m", str(MODEL_PIPER), "-f", str(keluar),
             "--length-scale", str(self.panjang)],
            input=teks.encode("utf-8"), capture_output=True,
        )
        if hasil.returncode != 0 or not keluar.is_file():
            pesan = hasil.stderr.decode("utf-8", "replace")[-800:]
            raise RuntimeError(f"piper keluar {hasil.returncode}: {pesan}")

    def layani(self, permintaan: dict) -> None:
        teks = str(permintaan.get("teks", "")).strip()
        keluar = Path(str(permintaan["keluar"]))
        if permintaan.get("piper"):
            self.piper = str(permintaan["piper"])

        if not teks:
            raise RuntimeError("teks kosong")

        mulai = time.time()
        kirim({"jenis": "progres", "tahap": "piper"})

        mentah = self.tmp / f"{os.getpid()}-{int(mulai*1000)}.wav"
        self._piper(teks, mentah)

        # Lewati RVC kalau induk memintanya (VTUBER_RVC=tidak).
        if permintaan.get("tanpa_rvc"):
            import shutil

            keluar.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(mentah, keluar)
        else:
            kirim({"jenis": "progres", "tahap": "rvc"})
            keluar.parent.mkdir(parents=True, exist_ok=True)
            self.rvc.infer_file(str(mentah), str(keluar))

        try:
            mentah.unlink(missing_ok=True)
        except OSError:
            pass

        kirim({
            "jenis": "selesai",
            "keluar": str(keluar),
            "detik": round(time.time() - mulai, 2),
        })


def utama() -> None:
    pekerja = Pekerja()
    try:
        pekerja.siapkan()
    except Exception as galat:
        kirim({"jenis": "galat", "pesan": f"gagal menyiapkan model: {galat}"})
        raise SystemExit(3)

    kirim({"jenis": "siap"})
    log("[pekerja] menunggu permintaan")

    for baris in sys.stdin:
        # Buang BOM UTF-8 kalau ada. Induk pernah mengirimnya di awal baris
        # pertama, dan json.loads menolaknya dengan "Unexpected UTF-8 BOM" —
        # satu-satunya permintaan pada sesi itu pun hilang tanpa suara.
        baris = baris.lstrip("\ufeff").strip()
        if not baris:
            continue
        try:
            permintaan = json.loads(baris)
        except json.JSONDecodeError as galat:
            kirim({"jenis": "galat", "pesan": f"JSON tidak sah: {galat}"})
            continue

        if permintaan.get("keluar_akhir"):
            log("[pekerja] permintaan berhenti")
            break

        try:
            pekerja.layani(permintaan)
        except Exception as galat:
            # Satu kalimat gagal tidak boleh mematikan pekerja; induk akan
            # melanjutkan ke kalimat berikutnya.
            kirim({"jenis": "galat", "pesan": str(galat)})
            log(f"[pekerja] galat: {galat}")


if __name__ == "__main__":
    utama()
