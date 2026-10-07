import { existsSync, statSync } from "node:fs";
import { join } from "node:path";
import * as ort from "onnxruntime-node";
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
import { dirname } from "node:path";
import { konversiPthKeOnnx, jalurPthBawaan } from "./pth2onnx.js";
const akar = cariAkarRepo();
function mb(byte) {
  return `${(byte / 1024 / 1024).toFixed(1)} MB`;
}
async function periksaOnnx(label, jalur) {
  if (!existsSync(jalur)) {
    console.log(`
\u2500\u2500 ${label}
   TIDAK ADA: ${jalur}`);
    return;
  }
  const ukuran = statSync(jalur).size;
  console.log(`
\u2500\u2500 ${label}
   ${jalur}  (${mb(ukuran)})`);
  try {
    const sesi = await ort.InferenceSession.create(jalur, {
      executionProviders: ["cpu"],
      graphOptimizationLevel: "disabled"
    });
    console.log("   inputs :", JSON.stringify(sesi.inputNames));
    console.log("   outputs:", JSON.stringify(sesi.outputNames));
    const metaMasuk = sesi.inputMetadata;
    const metaKeluar = sesi.outputMetadata;
    for (const nama of sesi.inputNames) {
      const meta = metaMasuk[nama];
      if (meta)
        console.log(`     in  ${nama}: ${meta.type} isTensor=${meta.isTensor}`);
    }
    for (const nama of sesi.outputNames) {
      const meta = metaKeluar[nama];
      if (meta)
        console.log(`     out ${nama}: ${meta.type} isTensor=${meta.isTensor}`);
    }
    await sesi.release();
  } catch (err) {
    console.log("   GAGAL dibuka:", err.message);
  }
}
async function utama() {
  console.log("=== SPIKE RVC \u2014 memvalidasi asumsi rencana \xA75 ===");
  console.log("akar repo:", akar);
  const pth = jalurPthBawaan();
  const keluaran = join(akar, "assets", "voices", "silverwolf", "model.onnx");
  console.log("\n[1] Konversi .pth \u2192 .onnx");
  console.log("    sumber:", pth, existsSync(pth) ? `(${mb(statSync(pth).size)})` : "(TIDAK ADA)");
  if (existsSync(pth)) {
    try {
      const hasil = await konversiPthKeOnnx(pth, keluaran);
      console.log("    OK \u2014 sampleRate:", hasil.sampleRate, "Hz");
      console.log("    versi:", hasil.versi, "| useF0:", hasil.useF0, "| bobot:", hasil.jumlahBobot);
      console.log("    keluaran:", hasil.jalurKeluar, `(${mb(hasil.byte)})`);
    } catch (err) {
      console.log("    GAGAL:", err.message);
    }
  } else {
    console.log("    Dilewati: checkpoint .pth tidak ada di mesin ini.");
  }
  console.log("\n[2] Metadata tensor model ONNX");
  await periksaOnnx("RMVPE (f0) \u2014 sudah ONNX", join(akar, "assets", "suara", "model-dasar", "rmvpe.onnx"));
  await periksaOnnx("Generator RVC hasil konversi", keluaran);
  await periksaOnnx(
    "ContentVec/HuBERT ONNX (kalau sudah disediakan)",
    join(akar, "assets", "encoders", "contentvec.onnx")
  );
  const hubertPt = join(akar, "assets", "suara", "model-dasar", "hubert_base.pt");
  const contentvec = join(akar, "assets", "encoders", "contentvec.onnx");
  console.log("\n[3] Aset yang masih kurang untuk Fase 4");
  console.log("    hubert_base.pt :", existsSync(hubertPt) ? "ADA (PyTorch, belum ONNX)" : "tidak ada");
  console.log("    contentvec.onnx:", existsSync(contentvec) ? "ADA" : "BELUM \u2014 perlu unduh vec-768-layer-12.onnx");
  console.log("    rmvpe.onnx     :", existsSync(join(akar, "assets", "suara", "model-dasar", "rmvpe.onnx")) ? "ADA" : "tidak ada");
  console.log("\n=== SPIKE selesai ===");
}
await utama();
