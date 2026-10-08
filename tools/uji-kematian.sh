#!/usr/bin/env bash
# Uji kematian berulang §8.1 — docs/PROYEK.md §9.
#
# Menjalankan aplikasi berkali-kali, menahan tiap jalan selama beberapa detik,
# lalu MENYIMPAN crash.log tiap jalan ke tools/bukti/. Log kematian itu justru
# buktinya — jangan dihapus (pelajaran §7.19: loop yang menghapus log tiap
# percobaan membuat bukti hilang dan "crash" disalahartikan).
#
# Klasifikasi akhir log (docs/PROYEK.md §9):
#   ada "tipe    :"          -> exception tercatat (crash berjejak)
#   berakhir "[llama] ..."   -> crash senyap (fail-fast)
#   berakhir "[live2d] ..."  -> berhenti di jalur render
#   ada "DisposeAsync"       -> jendela ditutup, BUKAN crash
#
# Pakai:
#   bash tools/uji-kematian.sh [jumlah-jalan] [detik-tahan]
# Contoh:
#   bash tools/uji-kematian.sh 10 130

set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
T="$ROOT/src/SilverWolf.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64"
LOG="$ROOT/tools/bukti"
N="${1:-10}"
DETIK="${2:-130}"

mkdir -p "$LOG"

if [ ! -f "$T/SilverWolf.App.exe" ]; then
  echo "GAGAL: $T/SilverWolf.App.exe tidak ada. Bangun dulu (docs/PROYEK.md §4)." >&2
  exit 2
fi

hidup() {
  tasklist //FI "IMAGENAME eq SilverWolf.App.exe" 2>/dev/null | grep -c "SilverWolf.App.exe"
}

llama_hidup() {
  tasklist //FI "IMAGENAME eq llama-server.exe" 2>/dev/null | grep -c "llama-server.exe"
}

selamat=0
mati=0

for jalan in $(seq 1 "$N"); do
  taskkill //F //IM SilverWolf.App.exe >/dev/null 2>&1
  taskkill //F //IM llama-server.exe >/dev/null 2>&1
  sleep 5

  rm -f "$T/crash.log" "$T/run.out"
  (cd "$T" && ./SilverWolf.App.exe > run.out 2>&1 &)

  mati_pada=0
  for i in $(seq 1 $((DETIK / 4))); do
    sleep 4
    if [ "$(hidup)" = "0" ]; then
      mati_pada=$((i * 4))
      break
    fi
  done

  cp -f "$T/crash.log" "$LOG/jalan-$jalan.log" 2>/dev/null

  if [ "$mati_pada" = "0" ]; then
    selamat=$((selamat + 1))
    status="SELAMAT ${DETIK}dtk"
  else
    mati=$((mati + 1))
    status="MATI pada ${mati_pada}dtk"
  fi

  # Kelas penyebab dari ujung log.
  if [ -f "$T/crash.log" ]; then
    if grep -aq "DisposeAsync" "$T/crash.log"; then
      kelas="jendela-ditutup"
    elif grep -aq "tipe    :" "$T/crash.log"; then
      kelas="crash-berjejak"
    elif grep -aq "\[llama\]" "$T/crash.log"; then
      kelas="crash-senyap(llama)"
    elif grep -aq "\[swl2d\]" "$T/crash.log"; then
      kelas="berhenti-di-render"
    else
      kelas="berhenti-dini"
    fi
  else
    kelas="tanpa-log"
  fi

  echo "jalan $jalan/$N: $status | llama=$(llama_hidup) | $kelas"
done

taskkill //F //IM SilverWolf.App.exe >/dev/null 2>&1
taskkill //F //IM llama-server.exe >/dev/null 2>&1

echo "-----------------------------------------------------------"
echo "HASIL: selamat=$selamat mati=$mati dari $N jalan"
echo "Log tiap jalan: $LOG/jalan-*.log"
