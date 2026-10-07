import * as ort from "onnxruntime-web";
import { createPiperPhonemize } from "./piper-phonemize.js";
import { float32KeWav } from "./wav.js";

function lockWasmThreads(env) {
  if (!env?.wasm) return;
  try {
    Object.defineProperty(env.wasm, "numThreads", {
      get: () => 1,
      set: () => {},
      configurable: true,
      enumerable: true
    });
  } catch {}
}

lockWasmThreads(ort.env);

class BrowserPiper {
  sessionPromise = null;
  modelUrl;
  configUrl;
  voice;

  options;

  constructor(options = {}) {
    this.options = options;
    this.modelUrl = options.modelUrl ?? "/assets/piper/id_ID-news_tts-medium.onnx";
    this.configUrl = options.configUrl ?? `${this.modelUrl}.json`;
    this.voice = options.voice ?? "id_ID-news_tts-medium";

    if (ort.env?.wasm) {
      ort.env.wasm.wasmPaths = "/onnx/";
      lockWasmThreads(ort.env);
    }
  }

  create() {
    this.sessionPromise ??= (async () => {
      lockWasmThreads(ort.env);
      if (ort.env?.wasm) {
        ort.env.wasm.wasmPaths = "/onnx/";
      }

      try {
        const ortWasm = await import("onnxruntime-web/wasm");
        const instance = ortWasm.default || ortWasm;
        lockWasmThreads(instance.env);
        if (instance.env?.wasm) {
          instance.env.wasm.wasmPaths = "/onnx/";
        }
      } catch {}

      console.log("[piper] Memuat konfigurasi model dari:", this.configUrl);
      const cfgRes = await fetch(this.configUrl);
      if (!cfgRes.ok) throw new Error(`Gagal memuat config model: HTTP ${cfgRes.status}`);
      const config = await cfgRes.json();

      console.log("[piper] Memuat model ONNX dari:", this.modelUrl);
      const modelRes = await fetch(this.modelUrl);
      if (!modelRes.ok) throw new Error(`Gagal memuat model ONNX: HTTP ${modelRes.status}`);
      const modelBuffer = await modelRes.arrayBuffer();

      console.log("[piper] Menginisialisasi ONNX InferenceSession (WASM)...");
      const ortSession = await ort.InferenceSession.create(modelBuffer, {
        executionProviders: ["wasm"]
      });
      console.log("[piper] Piper TTS siap! Sample rate:", config.audio?.sample_rate);

      // Pre-warm phonemizer sekali di background
      const voice = config.espeak?.voice || "id";
      void this.phonemizeChunk("tes", voice).catch((e) => {
        console.warn("[piper] Pre-warm phonemizer warning:", e);
      });

      return { config, ortSession };
    })();
    return this.sessionPromise;
  }

  async phonemizeChunk(text, voice) {
    return new Promise(async (resolve, reject) => {
      try {
        const mod = await createPiperPhonemize({
          print: (data) => {
            try {
              const parsed = JSON.parse(data);
              resolve(parsed.phoneme_ids || []);
            } catch (err) {
              reject(err);
            }
          },
          printErr: (msg) => {
            console.warn("[piper-phonemize]", msg);
          },
          locateFile: (url) => {
            if (url.endsWith(".wasm")) return "/piper/piper_phonemize.wasm";
            if (url.endsWith(".data")) return "/piper/piper_phonemize.data";
            return url;
          }
        });
        mod.callMain([
          "-l", voice,
          "--input", JSON.stringify([{ text: text.trim() }]),
          "--espeak_data", "/espeak-ng-data"
        ]);
      } catch (err) {
        reject(err);
      }
    });
  }

  async synthesize(text) {
    if (!text || !text.trim()) return null;
    try {
      console.log("[piper] Memulai sintesis teks:", JSON.stringify(text));
      const { config, ortSession } = await this.create();
      const voice = config.espeak?.voice || "id";

      // Potong jika teks panjang menjadi potongan kalimat
      const rawChunks = text.trim().match(/[^.!?…\n]+[.!?…]*\s*/g) || [text.trim()];
      const chunks = rawChunks.map((c) => c.trim()).filter(Boolean);

      const pcmList = [];
      for (const chunk of chunks) {
        const phonemeIds = await this.phonemizeChunk(chunk, voice);
        if (!phonemeIds || phonemeIds.length === 0) continue;

        const feeds = {
          input: new ort.Tensor("int64", phonemeIds, [1, phonemeIds.length]),
          input_lengths: new ort.Tensor("int64", [phonemeIds.length]),
          scales: new ort.Tensor("float32", [
            this.options?.noiseScale ?? config.inference?.noise_scale ?? 0.667,
            this.options?.lengthScale ?? config.inference?.length_scale ?? 0.88,
            this.options?.noiseW ?? config.inference?.noise_w ?? 0.8
          ])
        };

        if (config.speaker_id_map && Object.keys(config.speaker_id_map).length > 0) {
          feeds.sid = new ort.Tensor("int64", [0]);
        }

        const result = await ortSession.run(feeds);
        const pcm = result.output?.data;
        if (pcm && pcm.length > 0) {
          pcmList.push(pcm);
        }
      }

      if (pcmList.length === 0) return null;

      const totalLength = pcmList.reduce((sum, p) => sum + p.length, 0);
      const merged = new Float32Array(totalLength);
      let offset = 0;
      for (const p of pcmList) {
        merged.set(p, offset);
        offset += p.length;
      }

      const sampleRate = config.audio?.sample_rate ?? 22050;
      const wav = float32KeWav(merged, sampleRate);
      const blob = new Blob([wav.buffer.slice(wav.byteOffset, wav.byteOffset + wav.byteLength)], {
        type: "audio/wav"
      });

      console.log("[piper] Sintesis sukses! Durasi:", (merged.length / sampleRate).toFixed(2), "detik, Blob size:", blob.size);
      return blob;
    } catch (err) {
      console.error("[piper] Gagal sintesis teks:", JSON.stringify(text), "Error:", err);
      throw err;
    }
  }

  destroy() {
    this.sessionPromise = null;
  }
}

export { BrowserPiper };
