const PENUTUP = ".!?\u2026\u3002\uFF01\uFF1F";
const EKOR = `"'\u201D\u2019)]}\xBB`;
import { EmotionParser } from "@aituber-onair/voice";

function buangTag(teks) {
  return EmotionParser.cleanEmotionTags(teks).replace(/[ \t]{2,}/g, " ");
}
function potongKalimat(teks, minimal = 12) {
  return potongBersih(buangTag(teks), minimal);
}
function potongBersih(teks, minimal) {
  const kalimat = [];
  let awal = 0;
  for (let i = 0; i < teks.length; i++) {
    const c = teks[i];
    if (!PENUTUP.includes(c)) continue;
    if (bukanAkhirKalimat(teks, i)) continue;
    let j = i + 1;
    while (j < teks.length && EKOR.includes(teks[j])) j++;
    const berikut = teks[j];
    if (berikut !== void 0 && !/[\s\n]/.test(berikut)) continue;
    const potongan = teks.slice(awal, j).trim();
    if (potongan.length >= minimal || /\s/.test(potongan)) {
      kalimat.push(potongan);
      awal = j;
    }
  }
  return { kalimat, sisa: teks.slice(awal).replace(/^\s+/, "") };
}
function bukanAkhirKalimat(teks, i) {
  const sebelum = teks[i - 1];
  if (sebelum === void 0 || sebelum < "0" || sebelum > "9") return false;
  const sesudah = teks[i + 1];
  if (sesudah !== void 0 && sesudah >= "0" && sesudah <= "9") return true;
  const duaSebelum = teks[i - 2];
  return duaSebelum === void 0 || /[\s\n(]/.test(duaSebelum);
}
function sisaKalimat(teks) {
  return teks.trim();
}
export {
  buangTag,
  potongKalimat,
  sisaKalimat
};
