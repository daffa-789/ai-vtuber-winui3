import { defineComponent, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useCompanionStore } from "@silverwolf/stage-ui";
import { AntreanSuara, BrowserVoicePipeline, dapatkanAudioContext } from "@silverwolf/pipelines-audio";
import { EmotionParser } from "@aituber-onair/voice";
import { bacaPanggung } from "@silverwolf/stage-ui-live2d/panggung.js";
import { Panggung } from "./komponen/Panggung.jsx";
import { Konsol } from "./komponen/Konsol.jsx";
import { lewatiTag } from "./ucapan.js";

const MODEL_URL = import.meta.env.VITE_MODEL_URL || "/models/silverwolf/silverwolf.model3.json";

var stdin_default = defineComponent({
  name: "SilverWolfApp",
  setup() {
    const panggung = bacaPanggung();
    const store = useCompanionStore();
    const input = ref("");
    const kanvas = ref();
    const modelState = ref("loading");
    const audioBusy = ref(false);
    const audioStatus = ref("");
    const voiceEnabled = ref(true);
    const mirrorTracking = ref(
      typeof localStorage !== "undefined" && localStorage.getItem("silverwolf_mirror_track") === "true"
    );
    let voice;
    let antrean;
    let renderer;
    let lepasKunciAudio;
    let autoWatcherTimer = null;

    function suara() {
      voice ??= new BrowserVoicePipeline({
        piper: {
          modelUrl: "/assets/piper/id_ID-news_tts-medium.onnx",
          configUrl: "/assets/piper/id_ID-news_tts-medium.onnx.json",
          lengthScale: 0.88,
        },
        rvc: {
          contentVecUrl: "/assets/encoders/vec-768-layer-12.onnx",
          modelUrl: "/assets/voices/silverwolf/model.onnx",
          transpose: 10,
        },
      });
      return voice;
    }

    onMounted(async () => {
      store.startPolling();
      void store.fetchKizuna();

      const bukaKunciAudio = () => {
        const ctx = dapatkanAudioContext();
        if (ctx && ctx.state === "suspended") {
          void ctx.resume().catch(() => {});
        }
      };
      window.addEventListener("pointerdown", bukaKunciAudio, { passive: true });
      window.addEventListener("keydown", bukaKunciAudio, { passive: true });
      lepasKunciAudio = () => {
        window.removeEventListener("pointerdown", bukaKunciAudio);
        window.removeEventListener("keydown", bukaKunciAudio);
      };

      // Pre-warm Piper TTS model & phonemizer di latar belakang
      void suara()
        .create?.()
        .then(() => {
          console.log("[App] Piper TTS siap digunakan.");
        })
        .catch((err) => {
          console.warn("[App] Pre-warm Piper TTS gagal:", err);
        });

      // Neuro-sama Autonomous Proactive Loop
      autoWatcherTimer = setInterval(() => {
        if (!store.autonomousMode || store.sending || audioBusy.value) return;
        const heningMs = Date.now() - store.lastUserActivity;
        // Jika hening lebih dari 65 detik, picu dialog proaktif Neuro-sama
        if (heningMs > 65000) {
          console.log("[Autonomous] Memicu dialog proaktif Neuro-sama...");
          void handleProactive();
        }
      }, 5000);

      if (!kanvas.value) return;
      try {
        const { Live2DRenderer } = await import("@silverwolf/stage-ui-live2d");
        renderer = new Live2DRenderer(kanvas.value, {
          ...panggung,
          invertX: mirrorTracking.value || panggung.invertX,
        });
        await renderer.load(MODEL_URL);
        await renderer.startMotion("isyarat", 2, 1);
        modelState.value = "ready";
      } catch (error) {
        console.warn("Live2D belum tersedia:", error);
        modelState.value = "missing";
        renderer?.destroy();
        renderer = void 0;
      }
    });

    onBeforeUnmount(() => {
      lepasKunciAudio?.();
      if (autoWatcherTimer) {
        clearInterval(autoWatcherTimer);
        autoWatcherTimer = null;
      }
      store.stopPolling();
      antrean?.berhenti();
      renderer?.destroy();
      voice?.destroy();
    });

    watch(
      () => store.expression,
      (value) => renderer?.setExpression(value)
    );

    watch(mirrorTracking, (val) => {
      try {
        if (typeof localStorage !== "undefined") {
          localStorage.setItem("silverwolf_mirror_track", val ? "true" : "false");
        }
      } catch {}
      renderer?.setInvert(val, false);
    });

    // Fungsi inti eksekusi suara & teks beriringan (karaoke typewriter + lip sync)
    async function jalankanAliranUcapan(aksiFn) {
      if (store.sending || audioBusy.value) return;
      antrean?.berhenti();

      let activeReply = null;
      let ketikTimer = null;

      const ketikKalimat = (teks, durasi) => {
        if (!activeReply) return;
        if (ketikTimer) {
          clearInterval(ketikTimer);
          ketikTimer = null;
        }
        const bersih = teks.trim();
        if (!bersih) return;

        if (activeReply.content && !activeReply.content.endsWith(" ")) {
          activeReply.content += " ";
        }

        if (!durasi || durasi <= 0.3) {
          activeReply.content += bersih;
          return;
        }

        const durasiMs = Math.max(100, durasi * 900);
        const delay = Math.max(15, durasiMs / bersih.length);
        let idx = 0;
        ketikTimer = setInterval(() => {
          if (idx < bersih.length) {
            activeReply.content += bersih[idx++];
          } else {
            clearInterval(ketikTimer);
            ketikTimer = null;
          }
        }, delay);
      };

      const antrian = new AntreanSuara({
        playbackRate: 1.15,
        synthesize: (teks) => suara().synthesize(teks),
        onMulut: (level) => renderer?.setMouth(level),
        onKalimatPutar: (teks, durasi) => {
          audioStatus.value = "Transmisi suara aktif";
          ketikKalimat(teks, durasi);
        },
        onGalat: (error) => {
          console.warn("Kesalahan sintesis suara:", error);
          audioStatus.value = error instanceof Error ? error.message : String(error);
        },
      });
      antrean = antrian;

      let mentah = "";
      let tagSelesai = false;
      let diproses = 0;

      const proses = (selesai = false) => {
        let pos = lewatiTag(mentah);
        if (!tagSelesai) {
          if (pos < 0 && !selesai) return;
          tagSelesai = true;
          if (pos >= 0) {
            const ext = EmotionParser.extractEmotion(mentah);
            if (ext.emotion) store.expression = ext.emotion;
          } else {
            pos = 0;
          }
        }
        const bisaUcap = mentah.slice(Math.max(pos, 0));
        const baru = bisaUcap.slice(diproses);
        if (!baru) return;
        diproses = bisaUcap.length;
        antrian.tambah(baru);
      };

      audioBusy.value = true;
      audioStatus.value = "Menyiapkan transmisi suara...";

      try {
        await aksiFn({
          syncVoice: true,
          onDelta: (potongan, reply) => {
            activeReply = reply;
            mentah += potongan;
            proses(false);
          },
        });
        proses(true);
        antrian.tutup();
        await antrian.tungguSelesai();
      } finally {
        if (ketikTimer) {
          clearInterval(ketikTimer);
          ketikTimer = null;
        }
        if (activeReply) {
          const ext = EmotionParser.extractEmotion(mentah);
          const clean = ext.cleanText || EmotionParser.cleanEmotionTags(mentah);
          if (!activeReply.content || activeReply.content.length < clean.length) {
            activeReply.content = clean;
          }
          activeReply.pending = false;
        }
        store.sending = false;
        audioBusy.value = false;
        antrean = void 0;
        audioStatus.value = "";
      }
    }

    async function submit() {
      const value = input.value;
      input.value = "";
      if (!value.trim()) return;

      if (!voiceEnabled.value) {
        await store.send(value);
        return;
      }
      await jalankanAliranUcapan((opts) => store.send(value, opts));
    }

    async function handleTouch() {
      if (!voiceEnabled.value) {
        await store.touch();
        return;
      }
      await jalankanAliranUcapan((opts) => store.touch(opts));
    }

    async function handleProactive() {
      if (!voiceEnabled.value) {
        await store.triggerProactive();
        return;
      }
      await jalankanAliranUcapan((opts) => store.triggerProactive(opts));
    }

    return () => (
      <main class="shell">
        <Panggung
          kanvas={kanvas}
          modelHilang={modelState.value === "missing"}
          ekspresi={store.expression}
          kizuna={store.kizuna}
          mirrorTracking={mirrorTracking.value}
          onHeadpat={() => void handleTouch()}
          onToggleMirror={() => {
            mirrorTracking.value = !mirrorTracking.value;
          }}
        />
        <Konsol
          nilai={input.value}
          sibukAudio={audioBusy.value}
          status={audioStatus.value}
          suaraAktif={voiceEnabled.value}
          ubah={(nilai) => {
            input.value = nilai;
          }}
          kirim={() => void submit()}
          toggleSuara={() => {
            voiceEnabled.value = !voiceEnabled.value;
          }}
          onHeadpat={() => void handleTouch()}
          onProactive={() => void handleProactive()}
        />
      </main>
    );
  },
});

export { stdin_default as default };
