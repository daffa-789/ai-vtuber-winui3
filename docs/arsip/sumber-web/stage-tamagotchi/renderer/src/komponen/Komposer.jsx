import { defineComponent } from "vue";
const Komposer = defineComponent({
  name: "Komposer",
  props: {
    nilai: { type: String, required: true },
    terkirim: { type: Boolean, required: true },
    sibukAudio: { type: Boolean, required: true },
    status: { type: String, required: true },
    suaraAktif: { type: Boolean, required: true },
    ubah: { type: Function, required: true },
    kirim: { type: Function, required: true },
    toggleSuara: { type: Function, required: true }
  },
  setup(props) {
    const saatKirim = (event) => {
      event.preventDefault();
      props.kirim();
    };
    const saatTombol = (event) => {
      if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
        event.preventDefault();
        props.kirim();
      }
    };
    return () => <form class="composer" onSubmit={saatKirim}>
        <div class="compose-head">
          <label for="message">TERMINAL INPUT // TRANSMISI</label>
          <div class="audio-controls">
            <button
              type="button"
              class={["tool", { active: props.suaraAktif }]}
              onClick={() => props.toggleSuara()}
              title="Aktifkan/nonaktifkan audio suara (Piper TTS)"
            >
              {props.suaraAktif ? "🔊 SUARA ON" : "🔇 BISU"}
            </button>
          </div>
        </div>
        <div class="input-row">
          <textarea
            id="message"
            rows={2}
            maxlength={4e3}
            placeholder="Tulis pesan ke Silver Wolf..."
            value={props.nilai}
            disabled={props.terkirim}
            onInput={(event) => props.ubah(event.target.value)}
            onKeydown={saatTombol}
          />
          <button class="send-btn" disabled={!props.nilai.trim() || props.terkirim} aria-label="Kirim">
            ↗
          </button>
        </div>
        <div class="composer-footer">
          <span class="hint">{props.status || "Tekan ENTER untuk kirim · SHIFT+ENTER baris baru · 100% Offline"}</span>
          <span class="counter">{props.nilai.length} / 4000</span>
        </div>
      </form>;
  }
});
export {
  Komposer
};
