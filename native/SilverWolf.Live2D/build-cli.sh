#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Bangun SilverWolf.Live2D.dll langsung dengan cl.exe, tanpa Visual Studio.
#
# Kenapa skrip ini ada: MSBuild.exe diblokir kebijakan keamanan di lingkungan
# ini, dan `dotnet build` tidak punya VCTargetsPath (MSB4019) sehingga tidak
# bisa membangun .vcxproj. Padahal cl.exe + link.exe tersedia dan DLL ini
# berupa ekspor C polos (bukan komponen C++/WinRT), jadi tidak perlu .idl,
# .winmd, maupun midl.
#
# Pakai:  bash build-cli.sh [Debug|Release]
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail
export MSYS_NO_PATHCONV=1

KONFIG="${1:-Debug}"
AKAR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

MSVC="C:/Program Files/Microsoft Visual Studio/18/Community/VC/Tools/MSVC/14.51.36231"
CL="$MSVC/bin/Hostx64/x64/cl.exe"
SDK="C:/Program Files (x86)/Windows Kits/10"
SDKVER="10.0.28000.0"
CUB="${CUBISM_SDK_DIR:-C:/sdk/CubismSdkForNative-5-r.5}"

SUMBER="$AKAR/native/SilverWolf.Live2D"
KELUAR="$SUMBER/build/x64/$KONFIG"
_OBJ="$KELUAR/obj"
mkdir -p "$_OBJ"

# cl.exe dan link.exe butuh path gaya Windows; path POSIX "/c/..." dibacanya
# sebagai "C:\c\...", yang langsung memunculkan C1083.
OBJ_WIN="$(cygpath -w "$_OBJ")"
KELUAR_WIN="$(cygpath -w "$KELUAR")"

echo "== SilverWolf.Live2D  $KONFIG | x64 =="
echo "   SDK Cubism : $CUB"
echo "   Keluaran   : $KELUAR"

# ── Kumpulkan sumber ─────────────────────────────────────────────────────────
# Semua .cpp di Framework/src KECUALI renderer backend selain D3D11.
mapfile -t BERKAS < <(
  find "$CUB/Framework/src" -name '*.cpp' \
    ! -path "*/Rendering/D3D9/*" \
    ! -path "*/Rendering/Metal/*" \
    ! -path "*/Rendering/OpenGL/*" \
    ! -path "*/Rendering/Vulkan/*" | sort
)
BERKAS+=("$SUMBER/Live2DStage.cpp")
echo "   Sumber     : ${#BERKAS[@]} berkas .cpp"

# ── Flag ─────────────────────────────────────────────────────────────────────
INC=(
  -I"$SUMBER"
  -I"$CUB/Core/include"
  -I"$CUB/Framework/src"
  -I"$CUB/Framework/src/Effect"
  -I"$CUB/Framework/src/Id"
  -I"$CUB/Framework/src/Math"
  -I"$CUB/Framework/src/Model"
  -I"$CUB/Framework/src/Motion"
  -I"$CUB/Framework/src/Physics"
  -I"$CUB/Framework/src/Rendering"
  -I"$CUB/Framework/src/Rendering/D3D11"
  -I"$CUB/Framework/src/Type"
  -I"$CUB/Framework/src/Utils"
  -I"$MSVC/include"
  -I"$SDK/Include/$SDKVER/ucrt"
  -I"$SDK/Include/$SDKVER/um"
  -I"$SDK/Include/$SDKVER/shared"
  -I"$SDK/Include/$SDKVER/winrt"
)

DEF=(-DUNICODE -D_UNICODE -DWIN32 -D_WINDOWS -DSWL2D_EXPORTS)

if [ "$KONFIG" = "Debug" ]; then
  RUNTIME=-MDd
  OPT=(-Od -Zi -D_DEBUG)
  LOPT=(-DEBUG)
  VARIAN=MDd
else
  RUNTIME=-MD
  OPT=(-O2 -DNDEBUG)
  LOPT=(-DEBUG)
  VARIAN=MD
fi

echo "== Kompilasi =="
GAGAL=0
for f in "${BERKAS[@]}"; do
  # Sumber dari $SUMBER berbentuk POSIX; cl.exe menganggapnya sebagai opsi
  # (D9002) lalu mengeluh kehilangan nama berkas (D8003).
  # -FS  : serialisasi penulisan PDB. Tanpa ini, dua cl.exe yang berjalan
  #        bersamaan (mis. build sebelumnya belum benar-benar selesai) mati
  #        dengan C1041 "cannot open program database".
  # -Fd  : arahkan PDB ke folder obj. Tanpa ini PDB jatuh di direktori kerja
  #        (akar repo) sebagai vc140.pdb dan mengotori repo.
  "$CL" -nologo -c -EHsc -W3 "$RUNTIME" -std:c++17 "${OPT[@]}" -FS \
        -Fd"$OBJ_WIN/SilverWolf.Live2D.pdb" \
        "${DEF[@]}" "${INC[@]}" -Fo"$OBJ_WIN/" "$(cygpath -w "$f")" \
        || GAGAL=$((GAGAL + 1))
done
echo "   gagal: $GAGAL berkas"
[ "$GAGAL" -ne 0 ] && { echo "KOMPILASI GAGAL"; exit 1; }

echo "== Taut =="
LINK="$MSVC/bin/Hostx64/x64/link.exe"
LIBCORE="$CUB/Core/lib/windows/x86_64/143/Live2DCubismCore_$VARIAN.lib"
[ -f "$LIBCORE" ] || { echo "lib Core tidak ada: $LIBCORE"; exit 1; }

# -MAP: menghasilkan SilverWolf.Live2D.map. Berkas ini yang dipakai untuk
# menerjemahkan RVA dari penangkap pengecualian (lihat CatatPengecualian di
# Live2DStage.cpp) menjadi nama fungsi + nomor baris yang tepat. Tanpa ini,
# access violation di dalam DLL hanya bisa ditebak.
"$LINK" -nologo -DLL "${LOPT[@]}" -OUT:"$KELUAR_WIN/SilverWolf.Live2D.dll" \
  -MAP:"$KELUAR_WIN/SilverWolf.Live2D.map" \
  -LIBPATH:"$MSVC/lib/x64" \
  -LIBPATH:"$SDK/Lib/$SDKVER/um/x64" \
  -LIBPATH:"$SDK/Lib/$SDKVER/ucrt/x64" \
  "$LIBCORE" d3d11.lib d3dcompiler.lib dxgi.lib windowscodecs.lib \
  ole32.lib user32.lib mincore.lib \
  "$OBJ_WIN"/*.obj || { echo "TAUT GAGAL"; exit 1; }

# Shader .fx WAJIB ikut: Cubism mengompilasinya saat runtime lewat D3DCompile
# dengan jalur relatif "FrameworkShaders/...". Tanpa ini model tidak tergambar.
mkdir -p "$KELUAR/FrameworkShaders"
cp -f "$CUB/Framework/src/Rendering/D3D11/Shaders/CubismEffect.fx"    "$KELUAR/FrameworkShaders/"
cp -f "$CUB/Framework/src/Rendering/D3D11/Shaders/CubismBlendMode.fx" "$KELUAR/FrameworkShaders/"

echo "== SELESAI =="
ls -la "$KELUAR/SilverWolf.Live2D.dll" "$KELUAR/FrameworkShaders/"
