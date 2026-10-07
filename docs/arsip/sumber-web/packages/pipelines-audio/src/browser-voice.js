import { BrowserPiper } from "./browser-piper.js";
import { BrowserRvc } from "./browser-rvc.js";

class BrowserVoicePipeline {
  piper;
  rvc;

  constructor(options = {}) {
    this.piper = new BrowserPiper(options.piper);
    this.rvc = new BrowserRvc(options.rvc);
  }

  async synthesize(text) {
    const source = await this.piper.synthesize(text);
    if (!source) return source;

    try {
      if (this.rvc && (await this.rvc.isModelReady?.())) {
        return await this.rvc.convert(source);
      }
    } catch (error) {
      console.warn("RVC tidak dapat diproses; audio langsung diputar dari Piper TTS:", error);
    }
    return source;
  }

  create() {
    return this.piper.create();
  }

  destroy() {
    this.piper.destroy();
  }
}

export { BrowserVoicePipeline };
