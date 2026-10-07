"""Konversi suara ke Silver Wolf memakai RVC (Retrieval-based Voice Conversion).

Skrip ini mengubah WAV apa pun (mis. hasil Piper Indonesia) menjadi suara
Silver Wolf memakai checkpoint RVC v2 milik proyek ini:

    assets/rvc/SilverWolfJP/SilverWolfJP.pth
    assets/rvc/SilverWolfJP/added_IVF1442_Flat_nprobe_1_SilverWolfJP_v2.index

PROFIL SUARA DIKUNCI
Semua nilai bawaan dibaca dari `.env` di akar repo, bukan ditulis ulang di sini.
`.env` adalah satu-satunya sumber kebenaran, supaya skrip ini dan aplikasi tidak
pernah memakai angka yang berbeda. Hasil acuan profil terkunci:

    tools/tts/contoh/04-transpose-3.wav   (transpose -3, index_rate 0,6)

Kunci `.env` yang dibaca:
    VTUBER_RVC_MODEL, VTUBER_RVC_VERSI, VTUBER_RVC_F0,
    VTUBER_RVC_TRANSPOSE, VTUBER_RVC_INDEKS_LAJU, VTUBER_RVC_PENCUCIAN,
    VTUBER_RVC_CAMPUR_RMS, VTUBER_RVC_PROTEKSI

Pemakaian:
    python rvc_konversi.py MASUK.wav KELUAR.wav
    python rvc_konversi.py MASUK.wav KELUAR.wav --transpose 0   # menimpa .env

Catatan penting: skrip ini HARUS dijalankan dengan Python yang punya
torch + fairseq + rvc-python + faiss. Lihat tools/tts/README.md.
"""

from __future__ import annotations

import argparse
import sys
import warnings
from pathlib import Path

AKAR = Path(__file__).resolve().parents[2]


# ── Pembacaan .env ──────────────────────────────────────────────────────────

def baca_env(akar: Path = AKAR) -> dict[str, str]:
    """Pembaca .env sederhana: `KUNCI=nilai`, `#` komentar, kutip dibuang."""
    jalur = akar / ".env"
    hasil: dict[str, str] = {}
    if not jalur.is_file():
        return hasil

    for baris in jalur.read_text(encoding="utf-8", errors="replace").splitlines():
        baris = baris.strip()
        if not baris or baris.startswith("#") or "=" not in baris:
            continue
        kunci, _, nilai = baris.partition("=")
        nilai = nilai.strip().strip('"').strip("'")
        hasil[kunci.strip()] = nilai
    return hasil


def _angka(env: dict[str, str], kunci: str, bawaan, tipe):
    try:
        return tipe(env[kunci])
    except (KeyError, ValueError, TypeError):
        return bawaan


def profil(env: dict[str, str] | None = None) -> dict:
    """Profil suara terkunci, dibaca dari .env dengan cadangan yang aman."""
    env = env if env is not None else baca_env()
    model = env.get("VTUBER_RVC_MODEL", "SilverWolfJP")
    versi = env.get("VTUBER_RVC_VERSI", "v2")
    folder = AKAR / "assets" / "rvc" / model

    return {
        "model": folder / f"{model}.pth",
        "versi": versi,
        "indeks": next(iter(sorted(folder.glob(f"*{model}_{versi}.index"))), None),
        "transpose": _angka(env, "VTUBER_RVC_TRANSPOSE", -3, int),
        "index_rate": _angka(env, "VTUBER_RVC_INDEKS_LAJU", 0.6, float),
        "f0": env.get("VTUBER_RVC_F0", "rmvpe"),
        "filter_radius": _angka(env, "VTUBER_RVC_PENCUCIAN", 3, int),
        "rms_mix": _angka(env, "VTUBER_RVC_CAMPUR_RMS", 0.8, float),
        "protect": _angka(env, "VTUBER_RVC_PROTEKSI", 0.33, float),
    }


# ── Patch torch.load ────────────────────────────────────────────────────────
# PyTorch 2.6+ mengubah bawaan weights_only menjadi True. Checkpoint HuBERT
# dan RVC ditulis sebelum perubahan itu, jadi tanpa patch ini pemuatan gagal
# dengan UnpicklingError. Ini persis solusi yang dipakai proyek
# "voice changer3 glm" (lihat README-nya).
try:
    import torch

    _torch_load_asli = torch.load

    def _torch_load_patch(*args, **kwargs):
        kwargs.setdefault("weights_only", False)
        return _torch_load_asli(*args, **kwargs)

    torch.load = _torch_load_patch
except ImportError:
    print("GAGAL: torch tidak terpasang di Python ini.", file=sys.stderr)
    raise SystemExit(2)

warnings.filterwarnings("ignore", message=".*weight_norm.*")
warnings.filterwarnings("ignore", category=FutureWarning)

try:
    from rvc_python.infer import RVCInference
except ImportError as galat:
    print(f"GAGAL: rvc-python tidak bisa diimpor ({galat}).", file=sys.stderr)
    raise SystemExit(2)


def konversi(
    masuk: Path,
    keluar: Path,
    transpose: int | None = None,
    index_rate: float | None = None,
    f0: str | None = None,
    filter_radius: int | None = None,
    rms_mix: float | None = None,
    protect: float | None = None,
    device: str = "cpu",
    nprobe: int | None = None,
) -> None:
    p = profil()
    # Argumen yang tidak diberikan (None) memakai nilai dari .env.
    transpose = p["transpose"] if transpose is None else transpose
    index_rate = p["index_rate"] if index_rate is None else index_rate
    f0 = p["f0"] if f0 is None else f0
    filter_radius = p["filter_radius"] if filter_radius is None else filter_radius
    rms_mix = p["rms_mix"] if rms_mix is None else rms_mix
    protect = p["protect"] if protect is None else protect

    if not masuk.is_file():
        raise SystemExit(f"GAGAL: berkas masukan tidak ada: {masuk}")
    if not p["model"].is_file():
        raise SystemExit(f"GAGAL: model RVC tidak ada: {p['model']}")
    if p["indeks"] is None:
        print("PERINGATAN: indeks tidak ditemukan; kualitas turun.", file=sys.stderr)

    print(f"[rvc] perangkat   : {device}")
    print(f"[rvc] model       : {p['model'].name}  (versi {p['versi']})")
    print(f"[rvc] indeks      : {p['indeks'].name if p['indeks'] else '(tidak ada)'}")
    print(f"[rvc] transpose   : {transpose:+d} semitone  (.env VTUBER_RVC_TRANSPOSE)")
    print(f"[rvc] index_rate  : {index_rate}  (.env VTUBER_RVC_INDEKS_LAJU)")
    print(f"[rvc] f0          : {f0}")

    rvc = RVCInference(
        device=device,
        model_path=str(p["model"]),
        index_path=str(p["indeks"]) if p["indeks"] else "",
        version=p["versi"],
    )

    # set_params() adalah satu-satunya jalan yang benar: atribut internalnya
    # bernama f0method (bukan f0_method / f0_extractor).
    rvc.set_params(
        f0up_key=transpose,
        index_rate=index_rate,
        f0method=f0,
        filter_radius=filter_radius,
        rms_mix_rate=rms_mix,
        protect=protect,
        resample_sr=0,
    )

    keluar.parent.mkdir(parents=True, exist_ok=True)
    rvc.infer_file(str(masuk), str(keluar))
    print(f"[rvc] selesai -> {keluar}")


def utama() -> None:
    p = profil()
    d = argparse.ArgumentParser(
        description="Konversi WAV ke suara Silver Wolf (RVC v2). "
                    "Nilai bawaan dibaca dari .env."
    )
    d.add_argument("masuk", type=Path, help="WAV masukan")
    d.add_argument("keluar", type=Path, help="WAV keluaran")
    d.add_argument("--transpose", type=int, default=None,
                   help=f"semitone; .env VTUBER_RVC_TRANSPOSE = {p['transpose']:+d}")
    d.add_argument("--index-rate", type=float, default=None,
                   help=f"0..1; .env VTUBER_RVC_INDEKS_LAJU = {p['index_rate']}")
    d.add_argument("--f0", default=None,
                   choices=["rmvpe", "fcpe", "harvest", "pm", "crepe"],
                   help=f"metode pitch; .env VTUBER_RVC_F0 = {p['f0']}")
    d.add_argument("--filter-radius", type=int, default=None,
                   help=f".env VTUBER_RVC_PENCUCIAN = {p['filter_radius']}")
    d.add_argument("--rms-mix", type=float, default=None,
                   help=f".env VTUBER_RVC_CAMPUR_RMS = {p['rms_mix']}")
    d.add_argument("--protect", type=float, default=None,
                   help=f".env VTUBER_RVC_PROTEKSI = {p['protect']}")
    d.add_argument("--device", default="cpu", help="cpu atau cuda:0")
    a = d.parse_args()

    konversi(
        masuk=a.masuk.resolve(),
        keluar=a.keluar.resolve(),
        transpose=a.transpose,
        index_rate=a.index_rate,
        f0=a.f0,
        filter_radius=a.filter_radius,
        rms_mix=a.rms_mix,
        protect=a.protect,
        device=a.device,
    )


if __name__ == "__main__":
    utama()
