import { defineComponent } from "vue";
import { useCompanionStore } from "@silverwolf/stage-ui";
import { DaftarPesan } from "./DaftarPesan.jsx";
import { Komposer } from "./Komposer.jsx";

const Konsol = defineComponent({
  name: "Konsol",
  props: {
    nilai: { type: String, required: true },
    sibukAudio: { type: Boolean, required: true },
    status: { type: String, required: true },
    suaraAktif: { type: Boolean, required: true },
    ubah: { type: Function, required: true },
    kirim: { type: Function, required: true },
    toggleSuara: { type: Function, required: true },
    onHeadpat: { type: Function, default: () => {} },
    onProactive: { type: Function, default: () => {} },
  },
  setup(props) {
    const store = useCompanionStore();

    return () => (
      <section class="console">
        <header class="console-head">
          <div>
            <span class="eyebrow">STELLARON TERMINAL // PRIVATE NEURAL LINK</span>
            <h1>Catatan Lapangan & Status Relasi</h1>
          </div>
          <div class="head-controls">
            <button
              class={[
                "autonomous-toggle-btn",
                { active: store.autonomousMode },
              ]}
              onClick={() => {
                store.autonomousMode = !store.autonomousMode;
              }}
              title="Neuro-sama Mode: Ketika aktif, Silver Wolf akan berinisiatif mengajak ngobrol atau berkomentar secara otonom saat kamu hening!"
            >
              <span class="toggle-dot" />
              <span class="toggle-label">
                {store.autonomousMode ? "NEURO-SAMA ON" : "NEURO-SAMA OFF"}
              </span>
            </button>
            <button
              class={["status", { online: store.ready, loading: store.loading }]}
              onClick={() => void store.checkHealth()}
              title={store.healthError || store.health?.model}
            >
              <i /> {store.ready ? "VULKAN ONLINE" : store.loading ? "MEMUAT VULKAN..." : "OFFLINE"}
            </button>
          </div>
        </header>

        {/* Kizuna Bond Status HUD Card */}
        {store.kizuna && (
          <div class="kizuna-card">
            <div class="kizuna-card-header">
              <div class="kizuna-info">
                <span class="kizuna-tag">KIZUNA BOND MODEL</span>
                <strong class="kizuna-title">
                  Lv.{store.kizuna.level} {store.kizuna.stageName} — {store.kizuna.stageLabel}
                </strong>
              </div>
              <div class="kizuna-actions">
                <button
                  class="btn-kizuna-touch"
                  onClick={() => props.onHeadpat?.()}
                  title="Elus kepala Silver Wolf untuk menambah afeksi dan membuatnya tersipu!"
                >
                  🌸 Elus Kepala (+XP)
                </button>
                <button
                  class="btn-kizuna-proactive"
                  onClick={() => props.onProactive?.()}
                  title="Minta Silver Wolf untuk berbicara spontan (Inisiatif Neuro-sama)"
                >
                  ⚡ Pancing Obrolan
                </button>
              </div>
            </div>

            {/* XP Progress Bar */}
            <div class="kizuna-progress-row">
              <div class="kizuna-progress-meta">
                <span>Poin Hubungan</span>
                <b class="xp-counter">
                  {store.kizuna.points} / {store.kizuna.nextPoints} XP ({store.kizuna.progress}%)
                </b>
              </div>
              <div class="kizuna-bar-track">
                <div
                  class="kizuna-bar-fill"
                  style={{ width: `${Math.min(100, Math.max(0, store.kizuna.progress))}%` }}
                />
              </div>
            </div>

            {/* Warmth & Atmosphere Row */}
            <div class="kizuna-meta-row">
              <div class="meta-item">
                <span class="meta-label">Kehangatan Hubungan:</span>
                <span class="meta-value warmth">
                  💖 {Math.round((store.kizuna.warmth ?? 1) * 100)}%
                </span>
              </div>
              <div class="meta-item">
                <span class="meta-label">Atmosfer:</span>
                <span class="meta-value atmosphere">
                  {store.kizuna.atmosphere} ({store.kizuna.trend})
                </span>
              </div>
              <div class="meta-item tone" title={store.kizuna.tone}>
                <span class="meta-label">Sikap:</span>
                <span class="meta-value">{store.kizuna.tone}</span>
              </div>
            </div>
          </div>
        )}

        {store.health ? (
          <div class="telemetry">
            <div class={["telemetry-chip", { loading: store.loading }]}>
              <strong>GPU:</strong> {store.health.model}
            </div>
            <div class="telemetry-chip">
              <strong>MEM:</strong> {store.health.memori}
            </div>
            <div class="telemetry-chip">
              <strong>TTS:</strong> Piper TTS (id_ID) + Pitch Mod
            </div>
          </div>
        ) : store.healthError ? (
          <div class="banner">Server tidak menjawab — jalankan <code>npm run dev</code></div>
        ) : null}

        <DaftarPesan pesan={store.messages} />

        <Komposer
          nilai={props.nilai}
          terkirim={store.sending}
          sibukAudio={props.sibukAudio}
          status={props.status}
          suaraAktif={props.suaraAktif}
          ubah={props.ubah}
          kirim={props.kirim}
          toggleSuara={props.toggleSuara}
        />
      </section>
    );
  },
});

export { Konsol };
