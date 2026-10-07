import { readFileSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import process from "node:process";
import { pthToOnnx } from "rvc-onnx-web";
import { existsSync } from "node:fs";

function cariAkarRepo() {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, "package.json")) || existsSync(join(dir, ".git"))) return dir;
    const naik = dirname(dir);
    if (naik === dir) break;
    dir = naik;
  }
  return process.cwd();
}
async function konversiPthKeOnnx(jalurMasuk, jalurKeluar, opsi = {}) {
  const mentah = readFileSync(jalurMasuk);
  const { onnxBuffer, sampleRate, checkpoint } = await pthToOnnx(new Uint8Array(mentah), {
    opsetVersion: opsi.opsetVersion ?? 17,
    phoneLen: opsi.phoneLen ?? 100
  });
  mkdirSync(dirname(jalurKeluar), { recursive: true });
  writeFileSync(jalurKeluar, onnxBuffer);
  return {
    jalurMasuk,
    jalurKeluar,
    sampleRate,
    jumlahBobot: checkpoint.weights.size,
    versi: String(checkpoint.version),
    useF0: Boolean(checkpoint.useF0),
    byte: onnxBuffer.byteLength
  };
}
function jalurPthBawaan() {
  const akar = cariAkarRepo();
  return join(akar, "assets", "suara", "rvc", "SilverWolfJP", "SilverWolfJP.pth");
}
async function utama() {
  const akar = cariAkarRepo();
  const arg = process.argv.slice(2).filter((a) => !a.startsWith("--"));
  const masuk = resolve(arg[0] ?? jalurPthBawaan());
  const keluar = resolve(arg[1] ?? join(akar, "assets", "voices", "silverwolf", "model.onnx"));
  console.log("[voice:convert] masuk :", masuk);
  console.log("[voice:convert] keluar:", keluar);
  console.log("[voice:convert] catatan: hanya RVC v2; korelasi audio ~78% itu normal.");
  const hasil = await konversiPthKeOnnx(masuk, keluar);
  console.log("[voice:convert] selesai:", JSON.stringify(hasil, null, 2));
}
if (import.meta.url === `file://${process.argv[1]?.replace(/\\/g, "/")}` || process.argv[1]?.endsWith("pth2onnx.js"))
  await utama();
export {
  jalurPthBawaan,
  konversiPthKeOnnx
};
