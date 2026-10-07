"""Teks Indonesia -> suara Silver Wolf.

Rantai dua tahap, persis seperti aplikasi web lama (piper -> rvc):

    1. Piper TTS  : teks Indonesia -> WAV 22.050 Hz
                    assets/piper/id_ID-news_tts-medium.onnx (espeak voice "id")
    2. RVC v2     : WAV -> suara Silver Wolf, WAV 40.000 Hz
                    assets/rvc/SilverWolfJP/SilverWolfJP.pth (+ indeks FAISS)

PROFIL SUARA DIKUNCI
Semua parameter dibaca dari `.env` di akar repo. Hasil acuan:
    tools/tts/contoh/04-transpose-3.wav
Jangan mengubah angka-angka itu tanpa persetujuan Master.

Pemakaian:
    python buat_suara.py "Halo Master, aku Silver Wolf." keluar.wav
    python buat_suara.py --transpose 0 "Kalimat lain." lain.wav   # menimpa .env
    python buat_suara.py --no-rvc "Teks" piper-saja.wav           # hanya Piper

CATATAN LINGKUNGAN
Skrip ini dijalankan oleh Python yang punya torch + fairseq + rvc-python
(lihat tools/tts/README.md). Piper dipanggil sebagai proses terpisah, karena
piper-tts hidup di lingkungan Python yang berbeda.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from rvc_konversi import AKAR, baca_env, konversi, profil  # noqa: E402

MODEL_PIPER = AKAR / "assets" / "piper" / "id_ID-news_tts-medium.onnx"

# piper.exe dari lingkungan Python yang punya paket piper-tts.
# Bisa ditimpa lewat variabel lingkungan SW_PIPER_EXE.
PIPER_EXE_BAWAAN = os.environ.get(
    "SW_PIPER_EXE",
    r"C:\Users\Daffa\.workbuddy-ai\binaries\python\envs\default\Scripts\piper.exe",
)


def piper(teks: str, keluar: Path, piper_exe: str, panjang: float) -> None:
    if not Path(piper_exe).is_file():
        raise SystemExit(
            f"GAGAL: piper.exe tidak ditemukan di {piper_exe}\n"
            "Setel variabel lingkungan SW_PIPER_EXE ke piper.exe milik lingkungan "
            "yang punya paket piper-tts."
        )
    if not MODEL_PIPER.is_file():
        raise SystemExit(f"GAGAL: model Piper tidak ada: {MODEL_PIPER}")

    keluar.parent.mkdir(parents=True, exist_ok=True)
    perintah = [
        piper_exe,
        "-m", str(MODEL_PIPER),
        "-f", str(keluar),
        "--length-scale", str(panjang),
    ]
    print(f"[piper] {teks[:60]}{'...' if len(teks) > 60 else ''}")
    print(f"[piper] length_scale : {panjang}  (.env VTUBER_TTS_PIPER_PANJANG)")
    hasil = subprocess.run(perintah, input=teks.encode("utf-8"), capture_output=True)
    if hasil.returncode != 0 or not keluar.is_file():
        print(hasil.stderr.decode("utf-8", "replace")[-1500:], file=sys.stderr)
        raise SystemExit(f"GAGAL: piper keluar dengan kode {hasil.returncode}")


def utama() -> None:
    env = baca_env()
    p = profil(env)
    panjang_bawaan = float(env.get("VTUBER_TTS_PIPER_PANJANG", 0.9))

    d = argparse.ArgumentParser(
        description="Teks Indonesia -> suara Silver Wolf. Nilai bawaan dari .env."
    )
    d.add_argument("teks", help="teks Indonesia yang akan diucapkan")
    d.add_argument("keluar", type=Path, help="berkas WAV keluaran")
    d.add_argument("--transpose", type=int, default=None,
                   help=f"semitone; .env VTUBER_RVC_TRANSPOSE = {p['transpose']:+d}")
    d.add_argument("--index-rate", type=float, default=None,
                   help=f".env VTUBER_RVC_INDEKS_LAJU = {p['index_rate']}")
    d.add_argument("--f0", default=None,
                   help=f"metode pitch; .env VTUBER_RVC_F0 = {p['f0']}")
    d.add_argument("--rms-mix", type=float, default=None,
                   help=f".env VTUBER_RVC_CAMPUR_RMS = {p['rms_mix']}")
    d.add_argument("--protect", type=float, default=None,
                   help=f".env VTUBER_RVC_PROTEKSI = {p['protect']}")
    d.add_argument("--panjang", type=float, default=panjang_bawaan,
                   help=f"length_scale Piper; .env VTUBER_TTS_PIPER_PANJANG = {panjang_bawaan}")
    d.add_argument("--piper-exe", default=PIPER_EXE_BAWAAN)
    d.add_argument("--no-rvc", action="store_true", help="lewati tahap RVC")
    a = d.parse_args()

    keluar = a.keluar.resolve()

    with tempfile.TemporaryDirectory(prefix="sw-tts-") as tmp:
        mentah = Path(tmp) / "piper.wav"
        piper(a.teks, mentah, a.piper_exe, a.panjang)

        if a.no_rvc:
            import shutil

            keluar.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(mentah, keluar)
            print(f"[selesai] Piper saja -> {keluar}")
            return

        konversi(
            masuk=mentah,
            keluar=keluar,
            transpose=a.transpose,
            index_rate=a.index_rate,
            f0=a.f0,
            filter_radius=None,
            rms_mix=a.rms_mix,
            protect=a.protect,
            device="cpu",
        )

    print(f"[selesai] suara Silver Wolf -> {keluar}")


if __name__ == "__main__":
    utama()
