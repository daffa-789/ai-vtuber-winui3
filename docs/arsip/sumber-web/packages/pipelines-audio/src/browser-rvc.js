import * as ort from "onnxruntime-web";
import { wavKeFloat32, float32KeWav } from "./wav.js";
const resample = (input, from, to) => {
  if (from === to) return input;
  const out = new Float32Array(Math.round(input.length * to / from));
  for (let i = 0; i < out.length; i++) {
    const p = i * from / to, a = Math.floor(p), f = p - a;
    out[i] = (input[a] ?? 0) * (1 - f) + (input[a + 1] ?? input[a] ?? 0) * f;
  }
  return out;
};
function estimateF0(samples, frames, transpose) {
  const out = new Float32Array(frames), hop = samples.length / frames;
  for (let f = 0; f < frames; f++) {
    const center = Math.floor((f + 0.5) * hop), start = Math.max(0, center - 512), end = Math.min(samples.length, center + 512);
    let best = 0, score = 0;
    for (let lag = 32; lag <= 320; lag++) {
      let sum = 0, aa = 0, bb = 0;
      for (let i = start + lag; i < end; i++) {
        const a = samples[i] ?? 0, b = samples[i - lag] ?? 0;
        sum += a * b;
        aa += a * a;
        bb += b * b;
      }
      const corr = sum / Math.sqrt(aa * bb + 1e-9);
      if (corr > score) {
        score = corr;
        best = lag;
      }
    }
    out[f] = score > 0.35 && best ? 16e3 / best * 2 ** (transpose / 12) : 0;
  }
  return out;
}
class BrowserRvc {
  constructor(options = {}) {
    this.options = options;
    ort.env.wasm.wasmPaths = "/onnx/";
  }
  content;
  model;
  _siap = null;

  available() {
    return Boolean(this.options.contentVecUrl && this.options.modelUrl);
  }

  async isModelReady() {
    if (!this.available()) return false;
    if (this._siap !== null) return this._siap;
    try {
      const res = await fetch(this.options.contentVecUrl, { method: "HEAD" });
      this._siap = res.ok;
    } catch {
      this._siap = false;
    }
    return this._siap;
  }
  async convert(blob) {
    if (!this.available()) return blob;
    const parsed = wavKeFloat32(new Uint8Array(await blob.arrayBuffer()));
    const input = resample(parsed.samples, parsed.sampleRate, 16e3);
    this.content ??= ort.InferenceSession.create(this.options.contentVecUrl, { executionProviders: ["wasm"] });
    this.model ??= ort.InferenceSession.create(this.options.modelUrl, { executionProviders: ["wasm"] });
    const cs = await this.content, featureResult = await cs.run({ [cs.inputNames[0]]: new ort.Tensor("float32", input, [1, input.length]) });
    const feature = featureResult[cs.outputNames[0]], dims = feature.dims, baseFrames = Number(dims[dims.length - 2]), width = Number(dims[dims.length - 1]);
    const frames = baseFrames * 2, phone = new Float32Array(frames * width), source = feature.data;
    for (let i = 0; i < frames; i++) phone.set(source.subarray(Math.floor(i / 2) * width, (Math.floor(i / 2) + 1) * width), i * width);
    const f0 = estimateF0(input, frames, this.options.transpose ?? 10), coarse = new BigInt64Array(frames);
    for (let i = 0; i < frames; i++) coarse[i] = BigInt(f0[i] ? Math.max(1, Math.min(255, Math.round(1 + 254 * (1127 * Math.log(1 + f0[i] / 700) - 1127 * Math.log(1 + 50 / 700)) / (1127 * Math.log(1 + 1100 / 700) - 1127 * Math.log(1 + 50 / 700))))) : 1);
    const model = await this.model, feeds = {
      phone: new ort.Tensor("float32", phone, [1, frames, width]),
      phone_lengths: new ort.Tensor("int64", BigInt64Array.of(BigInt(frames)), [1]),
      pitch: new ort.Tensor("int64", coarse, [1, frames]),
      nsff0: new ort.Tensor("float32", f0, [1, frames]),
      sid: new ort.Tensor("int64", BigInt64Array.of(0n), [1])
    };
    const result = await model.run(feeds), output = result.audio ?? result[model.outputNames[0]], sampleRate = Number(result.sr?.data?.[0] ?? 4e4);
    const wav = float32KeWav(output.data, sampleRate);
    return new Blob([wav.buffer.slice(wav.byteOffset, wav.byteOffset + wav.byteLength)], { type: "audio/wav" });
  }
}
export {
  BrowserRvc
};
