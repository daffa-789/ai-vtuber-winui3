#!/usr/bin/env python3
"""Potret isi jendela lewat PrintWindow — bekerja walau jendela tidak di depan.

Kenapa tidak memakai ImageGrab saja: ImageGrab mengambil isi LAYAR, jadi kalau
jendela aplikasi tertutup jendela lain (atau pemutar video), yang tertangkap
adalah jendela lain itu. `SetForegroundWindow` dari proses latar juga ditolak
Windows. PrintWindow meminta jendela menggambar dirinya sendiri ke bitmap kita,
jadi hasilnya tidak bergantung pada apa yang sedang di depan.

Jendela WinUI 3 memakai DirectComposition, jadi perlu PW_RENDERFULLCONTENT (2).

Pakai:
    python tools/potret-jendela.py keluaran.png ["Silver Wolf"]
"""
import ctypes
import ctypes.wintypes as w
import sys

from PIL import Image

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

# WAJIB sebelum jendela apa pun disentuh: tanpa ini proses Python dianggap
# tidak sadar-DPI, sehingga GetClientRect mengembalikan ukuran VIRTUAL
# (dibagi skala tampilan). Akibatnya potretnya mengecil dan isinya terpotong —
# dan penilaian ketajaman jadi salah. -4 = PER_MONITOR_AWARE_V2.
try:
    ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
except Exception:
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        pass

PW_RENDERFULLCONTENT = 0x00000002
SRCCOPY = 0x00CC0020
DIB_RGB_COLORS = 0


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", w.DWORD), ("biWidth", ctypes.c_long), ("biHeight", ctypes.c_long),
        ("biPlanes", w.WORD), ("biBitCount", w.WORD), ("biCompression", w.DWORD),
        ("biSizeImage", w.DWORD), ("biXPelsPerMeter", ctypes.c_long),
        ("biYPelsPerMeter", ctypes.c_long), ("biClrUsed", w.DWORD),
        ("biClrImportant", w.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", w.DWORD * 3)]


def main():
    keluaran = sys.argv[1] if len(sys.argv) > 1 else "tools/potret.png"
    judul = sys.argv[2] if len(sys.argv) > 2 else "Silver Wolf"

    hwnd = user32.FindWindowW(None, judul)
    if not hwnd:
        print(f"jendela '{judul}' tidak ditemukan")
        return 1

    rect = w.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(rect))
    lebar, tinggi = rect.right - rect.left, rect.bottom - rect.top
    if lebar <= 0 or tinggi <= 0:
        print("ukuran jendela tidak masuk akal")
        return 1

    hdcJendela = user32.GetDC(hwnd)
    hdcMemori = gdi32.CreateCompatibleDC(hdcJendela)
    bitmap = gdi32.CreateCompatibleBitmap(hdcJendela, lebar, tinggi)
    gdi32.SelectObject(hdcMemori, bitmap)

    if not user32.PrintWindow(hwnd, hdcMemori, PW_RENDERFULLCONTENT):
        print("PrintWindow gagal")
        return 1

    info = BITMAPINFO()
    info.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    info.bmiHeader.biWidth = lebar
    info.bmiHeader.biHeight = -tinggi          # negatif = baris atas dulu
    info.bmiHeader.biPlanes = 1
    info.bmiHeader.biBitCount = 32
    info.bmiHeader.biCompression = 0

    penyangga = ctypes.create_string_buffer(lebar * tinggi * 4)
    gdi32.GetDIBits(hdcMemori, bitmap, 0, tinggi, penyangga,
                    ctypes.byref(info), DIB_RGB_COLORS)

    gambar = Image.frombuffer("RGBA", (lebar, tinggi), penyangga.raw,
                              "raw", "BGRA", 0, 1).convert("RGB")
    gambar.save(keluaran)
    print(f"tersimpan: {keluaran} ({lebar}x{tinggi})")

    gdi32.DeleteObject(bitmap)
    gdi32.DeleteDC(hdcMemori)
    user32.ReleaseDC(hwnd, hdcJendela)
    return 0


if __name__ == "__main__":
    sys.exit(main())
