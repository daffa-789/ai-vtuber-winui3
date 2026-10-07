import { computed, reactive, ref } from "vue";
import { defineStore } from "pinia";
import { EmotionParser } from "@aituber-onair/voice";

let nextId = 1;
const apiBase = typeof window === "undefined" ? "" : new URLSearchParams(window.location.search).get("api") ?? "";

const useCompanionStore = defineStore("companion", () => {
  const messages = ref([]);
  const health = ref();
  const healthError = ref("");
  const sending = ref(false);
  const expression = ref("netral");
  const autonomousMode = ref(true);
  const lastUserActivity = ref(Date.now());

  const kizuna = ref({
    userId: "master",
    stage: "stranger",
    stageLabel: "Hacker Waspada",
    stageName: "Stranger",
    level: 1,
    points: 0,
    nextPoints: 100,
    progress: 0,
    warmth: 1,
    atmosphere: "warm",
    trend: "rising",
    tone: "",
  });

  const ready = computed(() => Boolean(health.value?.ok));
  const loading = computed(() => Boolean(health.value?.loading));
  let timer = null;

  function updateKizunaFromHeader(response) {
    try {
      const header = response.headers.get("x-kizuna");
      if (header) {
        const decoded = JSON.parse(decodeURIComponent(header));
        if (decoded) kizuna.value = decoded;
      }
    } catch {}
  }

  async function checkHealth() {
    try {
      const response = await fetch(`${apiBase}/api/health`, { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      health.value = await response.json();
      healthError.value = "";
      if (health.value?.kizuna) {
        kizuna.value = health.value.kizuna;
      }
    } catch (error) {
      healthError.value = error instanceof Error ? error.message : String(error);
    }
  }

  async function fetchKizuna() {
    try {
      const response = await fetch(`${apiBase}/api/kizuna`, { cache: "no-store" });
      if (response.ok) {
        const data = await response.json();
        if (data?.kizuna) kizuna.value = data.kizuna;
      }
    } catch {}
  }

  function startPolling() {
    if (timer) return;
    const tick = async () => {
      await checkHealth();
      const jeda = health.value?.ok ? 8000 : 2000;
      timer = setTimeout(tick, jeda);
    };
    void tick();
  }

  function stopPolling() {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
  }

  async function send(text, options = {}) {
    const clean = text.trim();
    if (!clean || sending.value) return "";
    lastUserActivity.value = Date.now();
    messages.value.push(reactive({ id: nextId++, role: "user", content: clean }));
    const reply = reactive({ id: nextId++, role: "assistant", content: "", pending: true });
    messages.value.push(reply);
    sending.value = true;
    try {
      const history = messages.value.filter((m) => !m.pending && !m.error).map(({ role, content }) => ({ role, content }));
      const response = await fetch(`${apiBase}/api/chat`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ messages: history })
      });
      if (!response.ok) {
        const value = await response.json().catch(() => ({}));
        if (response.status === 503) {
          throw new Error(value.error || "Model AI sedang dimuat. Harap tunggu sebentar lalu coba lagi...");
        }
        throw new Error(value.error ?? `HTTP ${response.status}`);
      }
      updateKizunaFromHeader(response);
      if (!response.body) throw new Error("browser tidak menyediakan response stream");
      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (!options.syncVoice) {
          reply.content += value;
        }
        options.onDelta?.(value, reply);
      }
      if (!options.syncVoice) {
        const extraction = EmotionParser.extractEmotion(reply.content);
        reply.content = extraction.cleanText || EmotionParser.cleanEmotionTags(reply.content);
        if (extraction.emotion) expression.value = extraction.emotion;
      }
      void fetchKizuna();
    } catch (error) {
      reply.error = true;
      reply.content = error instanceof Error ? error.message : String(error);
      reply.pending = false;
      sending.value = false;
    } finally {
      if (!options.syncVoice) {
        reply.pending = false;
        sending.value = false;
      }
    }
    return reply.error ? "" : reply.content;
  }

  async function touch(options = {}) {
    if (sending.value) return "";
    lastUserActivity.value = Date.now();
    expression.value = "kaget";
    const reply = reactive({ id: nextId++, role: "assistant", content: "", pending: true });
    messages.value.push(reply);
    sending.value = true;
    try {
      const response = await fetch(`${apiBase}/api/kizuna/touch`, {
        method: "POST",
        headers: { "content-type": "application/json" },
      });
      if (!response.ok) {
        throw new Error(`Gagal interaksi sentuhan (HTTP ${response.status})`);
      }
      updateKizunaFromHeader(response);
      if (!response.body) throw new Error("browser tidak menyediakan response stream");
      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (!options.syncVoice) {
          reply.content += value;
        }
        options.onDelta?.(value, reply);
      }
      if (!options.syncVoice) {
        const extraction = EmotionParser.extractEmotion(reply.content);
        reply.content = extraction.cleanText || EmotionParser.cleanEmotionTags(reply.content);
        if (extraction.emotion) expression.value = extraction.emotion;
      }
      void fetchKizuna();
    } catch (error) {
      reply.error = true;
      reply.content = error instanceof Error ? error.message : String(error);
      reply.pending = false;
      sending.value = false;
    } finally {
      if (!options.syncVoice) {
        reply.pending = false;
        sending.value = false;
      }
    }
    return reply.error ? "" : reply.content;
  }

  async function triggerProactive(options = {}) {
    if (sending.value) return "";
    const idleSeconds = Math.max(10, Math.round((Date.now() - lastUserActivity.value) / 1000));
    const reply = reactive({ id: nextId++, role: "assistant", content: "", pending: true });
    messages.value.push(reply);
    sending.value = true;
    try {
      const response = await fetch(`${apiBase}/api/autonomous/proactive`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ idle: idleSeconds })
      });
      if (!response.ok) {
        throw new Error(`Gagal memicu ucapan proaktif (HTTP ${response.status})`);
      }
      updateKizunaFromHeader(response);
      if (!response.body) throw new Error("browser tidak menyediakan response stream");
      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (!options.syncVoice) {
          reply.content += value;
        }
        options.onDelta?.(value, reply);
      }
      if (!options.syncVoice) {
        const extraction = EmotionParser.extractEmotion(reply.content);
        reply.content = extraction.cleanText || EmotionParser.cleanEmotionTags(reply.content);
        if (extraction.emotion) expression.value = extraction.emotion;
      }
      lastUserActivity.value = Date.now();
      void fetchKizuna();
    } catch (error) {
      reply.error = true;
      reply.content = error instanceof Error ? error.message : String(error);
      reply.pending = false;
      sending.value = false;
    } finally {
      if (!options.syncVoice) {
        reply.pending = false;
        sending.value = false;
      }
    }
    return reply.error ? "" : reply.content;
  }

  return {
    messages,
    health,
    healthError,
    sending,
    expression,
    ready,
    loading,
    kizuna,
    autonomousMode,
    lastUserActivity,
    checkHealth,
    fetchKizuna,
    startPolling,
    stopPolling,
    send,
    touch,
    triggerProactive,
  };
});

export { useCompanionStore };
