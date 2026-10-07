import { defineComponent, ref } from "vue";

const Panggung = defineComponent({
  name: "Panggung",
  props: {
    kanvas: { type: Object, required: true },
    modelHilang: { type: Boolean, required: true },
    ekspresi: { type: String, required: true },
    kizuna: { type: Object, default: () => ({}) },
    mirrorTracking: { type: Boolean, default: false },
    onHeadpat: { type: Function, default: () => {} },
    onToggleMirror: { type: Function, default: () => {} },
  },
  setup(props) {
    const efekSentuhan = ref(false);
    let efekTimeout = null;

    const pemicuHeadpat = () => {
      if (props.modelHilang) return;
      efekSentuhan.value = true;
      if (efekTimeout) clearTimeout(efekTimeout);
      efekTimeout = setTimeout(() => {
        efekSentuhan.value = false;
      }, 1800);
      props.onHeadpat?.();
    };

    return () => (
      <section class="stage" aria-label="Panggung Silver Wolf">
        <header class="brand">
          <span class="sigil">SW</span>
          <div>
            <b>SILVER WOLF</b>
            <small>STELLARON // LOCAL LINK</small>
          </div>
        </header>

        {/* Kizuna Live HUD Badge */}
        <div class="kizuna-stage-badge" title={props.kizuna?.tone || "Sistem Ikatan"}>
          <div class="kizuna-stage-pill">
            <span class="kizuna-dot" />
            <span class="kizuna-badge-lv">Lv.{props.kizuna?.level ?? 1}</span>
            <span class="kizuna-badge-title">{props.kizuna?.stageName ?? "Stranger"}</span>
          </div>
          <div class="kizuna-stage-warmth">
            <span class="kizuna-heart">💖</span>
            <span>{Math.round((props.kizuna?.warmth ?? 1) * 100)}%</span>
          </div>
        </div>

        <div class="stage-hud" />

        {/* Live2D Canvas Container with Headpat Hitbox */}
        <div class="canvas-wrap">
          <canvas ref={props.kanvas} class={{ hidden: props.modelHilang }} />

          {/* Hitbox Elus Kepala (Area Kepala/Rambut Atas) */}
          <div
            class="headpat-zone"
            onClick={pemicuHeadpat}
            title="Klik di sini untuk mengelus kepala Silver Wolf (Headpat) 🌸"
          />

          {/* Floating Sparkle/Heart Animation saat di-headpat */}
          {efekSentuhan.value && (
            <div class="headpat-effect" aria-hidden="true">
              <span class="heart-pulse">💕</span>
              <span class="xp-float">+8 Kizuna XP!</span>
            </div>
          )}
        </div>

        {/* Tombol Cepat Headpat & Mode Pandangan */}
        <div class="stage-floating-actions">
          <button
            class="headpat-floating-btn"
            onClick={pemicuHeadpat}
            title="Elus kepala pacarmu untuk menambah poin Kizuna dan membuatnya salting!"
          >
            <span class="btn-icon">🌸</span>
            <span class="btn-text">Elus Kepala</span>
          </button>
          <button
            class="track-mode-btn"
            onClick={props.onToggleMirror}
            title={
              props.mirrorTracking
                ? "Mode Cermin (Mirror) Aktif: Gerakan kepala membalik kiri/kanan seperti cermin. Klik untuk Mode Tatap Langsung!"
                : "Mode Tatap Langsung (Direct) Aktif: Kepala & mata menatap persis ke kursor mouse. Klik untuk Mode Cermin!"
            }
          >
            <span class="btn-icon">{props.mirrorTracking ? "🪞" : "👁️"}</span>
            <span class="btn-text">{props.mirrorTracking ? "Mirror" : "Tatap"}</span>
          </button>
        </div>

        {props.modelHilang && (
          <div class="avatar-fallback" aria-label="Model Live2D belum dipasang">
            <div class="glitch" data-text="404">
              404
            </div>
            <strong>MODEL OFFLINE</strong>
            <span>Pasang aset Live2D di public/models/silverwolf</span>
          </div>
        )}

        <div class="expression">
          <span>EMOSI // RAUT</span>
          <b>{props.ekspresi}</b>
        </div>
      </section>
    );
  },
});

export { Panggung };
