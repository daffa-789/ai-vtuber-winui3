import { Application, Ticker } from "pixi.js";
import { Live2DModel } from "pixi-live2d-display/cubism4";
import { bacaPanggung, hitungSkala } from "./panggung.js";
class Live2DRenderer {
  app;
  opsi;
  pas;
  model;
  onPointerMove;
  onPointerLeave;
  onWindowBlur;

  constructor(canvas, opsi = {}) {
    this.opsi = { ...bacaPanggung(), ...opsi };
    Live2DModel.registerTicker(Ticker);
    const resolution = Math.min(globalThis.devicePixelRatio || 1, this.opsi.skalaMaks);
    this.app = new Application({
      view: canvas,
      resizeTo: canvas.parentElement ?? window,
      backgroundAlpha: 0,
      antialias: true,
      autoDensity: true,
      resolution
    });
    this.pas = () => this.paskan();
    this.app.renderer.on("resize", this.pas);
    window.addEventListener("resize", this.pas);
  }
  async load(url) {
    const model = await Live2DModel.from(url, { autoInteract: false });
    this.model = model;
    this.app.stage.addChild(model);
    this.paskan();
    this.aktifkanIkutiKursor();
  }

  aktifkanIkutiKursor() {
    if (this.onPointerMove) return;

    this.onPointerMove = (e) => {
      this.arahkanKeLayar(e.clientX, e.clientY);
    };

    this.onPointerLeave = () => {
      this.setFocus(0, 0);
    };

    this.onWindowBlur = () => {
      this.setFocus(0, 0);
    };

    window.addEventListener("pointermove", this.onPointerMove, { passive: true });
    document.addEventListener("mouseleave", this.onPointerLeave, { passive: true });
    window.addEventListener("blur", this.onWindowBlur, { passive: true });
  }

  matikanIkutiKursor() {
    if (this.onPointerMove) {
      window.removeEventListener("pointermove", this.onPointerMove);
      this.onPointerMove = void 0;
    }
    if (this.onPointerLeave) {
      document.removeEventListener("mouseleave", this.onPointerLeave);
      this.onPointerLeave = void 0;
    }
    if (this.onWindowBlur) {
      window.removeEventListener("blur", this.onWindowBlur);
      this.onWindowBlur = void 0;
    }
    this.setFocus(0, 0);
  }

  arahkanKeLayar(clientX, clientY) {
    const focusCtrl = this.model?.internalModel?.focusController;
    if (!focusCtrl) return;

    const canvas = this.app.view;
    if (!canvas) return;

    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return;

    // Titik pusat wajah & mata Silver Wolf di koordinat viewport
    // Karakter berpusat horizontal di panggung (opsi.x = 0.5)
    // dan mata chibi berada di sekitar 46% tinggi panggung (opsi.headY = 0.46)
    const faceX = rect.left + rect.width * (this.opsi.x ?? 0.5);
    const faceY = rect.top + rect.height * (this.opsi.headY ?? 0.46);

    const dx = clientX - faceX;
    const dy = clientY - faceY;

    // Rentang span normalisasi adaptif agar tatapan mata & kepala natural di seluruh layar
    const spanLeft = Math.max(160, faceX * 0.85);
    const spanRight = Math.max(250, (window.innerWidth - faceX) * 0.7);
    const spanUp = Math.max(140, (faceY - rect.top) * 0.85);
    const spanDown = Math.max(160, (rect.bottom - faceY) * 0.85);

    let targetX = dx >= 0 ? dx / spanRight : dx / spanLeft;
    let targetY = dy <= 0 ? -dy / spanUp : -dy / spanDown;

    if (this.opsi.invertX) targetX = -targetX;
    if (this.opsi.invertY) targetY = -targetY;

    // Batasi dalam rentang [-1, 1]
    targetX = Math.max(-1, Math.min(1, targetX));
    targetY = Math.max(-1, Math.min(1, targetY));

    focusCtrl.focus(targetX, targetY);
  }

  setInvert(invertX, invertY) {
    if (invertX !== void 0) this.opsi.invertX = Boolean(invertX);
    if (invertY !== void 0) this.opsi.invertY = Boolean(invertY);
  }

  setFocus(targetX, targetY, instant = false) {
    const focusCtrl = this.model?.internalModel?.focusController;
    if (!focusCtrl) return;
    focusCtrl.focus(
      Math.max(-1, Math.min(1, targetX)),
      Math.max(-1, Math.min(1, targetY)),
      instant
    );
  }

  /**
   * Hitung skala karakter.
   *
   * KUNCI PERBAIKAN: ukuran dasar diambil dari `internalModel`, BUKAN
   * `model.width`. Getter `width` PIXI v6 adalah `scale.x * getLocalBounds().width`
   * -- artinya nilainya ikut berubah oleh skala yang sedang kita hitung, sehingga
   * `fit()` versi lama tidak idempoten: s1 = K (pas), s2 = K/s1 = 1 (ukuran
   * native model, jauh lebih besar dari jendela), s3 = K, dan seterusnya.
   * `internalModel.width` adalah `originalWidth * localTransform.a`, tetap
   * terhadap `model.scale`, jadi aman dipanggil berulang kali.
   */
  paskan() {
    const model = this.model;
    if (!model) return;
    const dasar = model.internalModel;
    if (!dasar?.width || !dasar?.height) return;
    const { width: W, height: H } = this.app.screen;
    if (!W || !H) return;
    const scale = hitungSkala(W, H, dasar.width, dasar.height, this.opsi.zoom);
    if (!scale) return;
    model.scale.set(scale);
    model.anchor.set(this.opsi.x, this.opsi.jangkar);
    model.x = W * this.opsi.x;
    model.y = H * this.opsi.jangkar;
  }
  setExpression(name) {
    void this.model?.expression(name);
  }
  setPose(_name, _on) {
  }
  async startMotion(group, index, priority = 2) {
    return Boolean(await this.model?.motion(group, index, priority));
  }
  setMouth(level) {
    const core = this.model?.internalModel.coreModel;
    core?.setParameterValueById?.("ParamMouthOpenY", Math.max(0, Math.min(1, level)));
  }
  destroy() {
    this.matikanIkutiKursor();
    window.removeEventListener("resize", this.pas);
    this.app.renderer.off("resize", this.pas);
    this.model?.destroy();
    this.app.destroy(false, { children: true, texture: true, baseTexture: true });
  }
}
export {
  Live2DRenderer,
  bacaPanggung,
  hitungSkala
};
