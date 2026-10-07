class WavRusak extends Error {
  name = "WavRusak";
}
const AMBANG_DENGAR = 400;
function pcmKeWav(pcm, laju, kanal = 1) {
  const header = new ArrayBuffer(44);
  const dv = new DataView(header);
  const tulisStr = (offset, s) => {
    for (let i = 0; i < s.length; i++)
      dv.setUint8(offset + i, s.charCodeAt(i));
  };
  tulisStr(0, "RIFF");
  dv.setUint32(4, 36 + pcm.length, true);
  tulisStr(8, "WAVE");
  tulisStr(12, "fmt ");
  dv.setUint32(16, 16, true);
  dv.setUint16(20, 1, true);
  dv.setUint16(22, kanal, true);
  dv.setUint32(24, laju, true);
  dv.setUint32(28, laju * kanal * 2, true);
  dv.setUint16(32, kanal * 2, true);
  dv.setUint16(34, 16, true);
  tulisStr(36, "data");
  dv.setUint32(40, pcm.length, true);
  const keluar = new Uint8Array(44 + pcm.length);
  keluar.set(new Uint8Array(header), 0);
  keluar.set(pcm, 44);
  return keluar;
}
function sudahWav(bin) {
  return bin.length > 12 && bin[0] === 82 && bin[1] === 73 && bin[2] === 70 && bin[3] === 70 && bin[8] === 87 && bin[9] === 65 && bin[10] === 86 && bin[11] === 69;
}
function lajuKanal(mime) {
  const angka = [...mime.matchAll(/(?:rate|channels)=(\d+)/g)].map((m) => Number(m[1]));
  return [angka[0] ?? 24e3, angka[1] ?? 1];
}
function bacaHeader(bin) {
  if (!sudahWav(bin))
    throw new WavRusak("bukan WAV: header RIFF/WAVE tidak ada");
  const dv = new DataView(bin.buffer, bin.byteOffset, bin.byteLength);
  let pos = 12;
  let laju = 0;
  let kanal = 0;
  let bitDepth = 16;
  let offsetData = -1;
  let panjangData = 0;
  while (pos + 8 <= bin.length) {
    const id = String.fromCharCode(bin[pos], bin[pos + 1], bin[pos + 2], bin[pos + 3]);
    const ukuran = dv.getUint32(pos + 4, true);
    const isi = pos + 8;
    if (id === "fmt " && isi + 16 <= bin.length) {
      kanal = dv.getUint16(isi + 2, true);
      laju = dv.getUint32(isi + 4, true);
      bitDepth = dv.getUint16(isi + 14, true);
    } else if (id === "data") {
      offsetData = isi;
      panjangData = Math.min(ukuran, bin.length - isi);
    }
    pos = isi + ukuran + ukuran % 2;
  }
  if (!laju)
    throw new WavRusak("laju sampel nol atau header fmt tidak ditemukan");
  if (offsetData < 0)
    throw new WavRusak("blok data tidak ditemukan");
  const bytePerFrame = Math.max(kanal, 1) * Math.max(bitDepth / 8, 1);
  const jumlahFrame = Math.floor(panjangData / bytePerFrame);
  return {
    laju,
    kanal,
    detik: jumlahFrame / laju,
    bitDepth,
    jumlahFrame,
    offsetData,
    panjangData
  };
}
function puncak(bin, batasFrame = 4e6) {
  let h;
  try {
    h = bacaHeader(bin);
  } catch (err) {
    throw new WavRusak(`isi WAV tidak terbaca: ${err.message}`);
  }
  if (h.bitDepth !== 16)
    return 1;
  const dv = new DataView(bin.buffer, bin.byteOffset, bin.byteLength);
  const kanal = Math.max(h.kanal, 1);
  const frameTerbaca = Math.min(h.jumlahFrame, batasFrame);
  const bytePerFrame = kanal * 2;
  let tertinggi = 0;
  for (let f = 0; f < frameTerbaca; f++) {
    const dasar = h.offsetData + f * bytePerFrame;
    for (let c = 0; c < kanal; c++) {
      const s = dv.getInt16(dasar + c * 2, true);
      const a = Math.abs(s);
      if (a > tertinggi)
        tertinggi = a;
      if (tertinggi >= 32767)
        return 32767;
    }
  }
  return tertinggi;
}
function wavKeFloat32(bin) {
  const h = bacaHeader(bin);
  if (h.bitDepth !== 16)
    throw new WavRusak(`pipeline browser hanya mendukung PCM16, menerima ${h.bitDepth}-bit`);
  const view = new DataView(bin.buffer, bin.byteOffset, bin.byteLength);
  const samples = new Float32Array(h.jumlahFrame);
  for (let frame = 0; frame < h.jumlahFrame; frame++) {
    let value = 0;
    for (let channel = 0; channel < h.kanal; channel++)
      value += view.getInt16(h.offsetData + (frame * h.kanal + channel) * 2, true) / 32768;
    samples[frame] = value / h.kanal;
  }
  return { samples, sampleRate: h.laju };
}
function float32KeWav(samples, sampleRate) {
  const pcm = new Uint8Array(samples.length * 2);
  const view = new DataView(pcm.buffer);
  for (let i = 0; i < samples.length; i++) {
    const value = Math.max(-1, Math.min(1, samples[i] ?? 0));
    view.setInt16(i * 2, Math.round(value * (value < 0 ? 32768 : 32767)), true);
  }
  return pcmKeWav(pcm, sampleRate, 1);
}
function adaBunyi(bin) {
  return puncak(bin) > AMBANG_DENGAR;
}
export {
  AMBANG_DENGAR,
  WavRusak,
  adaBunyi,
  bacaHeader,
  float32KeWav,
  lajuKanal,
  pcmKeWav,
  puncak,
  sudahWav,
  wavKeFloat32
};
