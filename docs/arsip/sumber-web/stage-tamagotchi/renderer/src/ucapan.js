import { EmotionParser } from "@aituber-onair/voice";

/**
 * Mendeteksi posisi akhir tag emosi [tag] dalam teks streaming.
 * Didukung langsung oleh EmotionParser dari @aituber-onair/voice.
 */
export function lewatiTag(teks) {
  if (!teks) return -1;
  const depan = teks.replace(/^[\s`'"]*/, "");
  if (!depan) return -1;
  if (depan[0] !== "[") return 0;
  const tutup = depan.indexOf("]");
  if (tutup < 0) return -1;
  return teks.length - depan.length + tutup + 1;
}

export function uraikanEmosi(teks) {
  return EmotionParser.extractEmotion(teks);
}

export function bersihkanEmosi(teks) {
  return EmotionParser.cleanEmotionTags(teks);
}
