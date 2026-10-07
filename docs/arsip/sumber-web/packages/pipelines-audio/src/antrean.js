import { potongKalimat } from "./kalimat.js";

let konteksGlobal = null;

export function dapatkanAudioContext() {
  if (typeof window === "undefined") return null;
  const AudioCtx = window.AudioContext || window.webkitAudioContext;
  if (!AudioCtx) return null;
  if (!konteksGlobal || konteksGlobal.state === "closed") {
    konteksGlobal = new AudioCtx();
  }
  return konteksGlobal;
}

class AntreanSuara {
  opsi;
  antrean = [];
  sisa = "";
  ditutup = false;
  berjalan = false;
  berhentiTotal = false;
  tunggu = [];
  audio;
  bingkai;
  sumberBuffer;
  gainNode;

  constructor(opsi) {
    this.opsi = opsi;
  }

  /**
   * Tambah potongan teks dari aliran streaming.
   * Setiap kalimat utuh langsung dipicu sintesisnya secara paralel (pipelined)
   * agar saat kalimat sebelumnya selesai bicara, audio berikutnya sudah siap tanpa jeda.
   */
  tambah(potongan) {
    if (this.berhentiTotal || !potongan) return;
    this.sisa += potongan;
    const { kalimat, sisa } = potongKalimat(this.sisa);
    this.sisa = sisa;
    for (const k of kalimat) {
      this.antrean.push({
        teks: k,
        promiseBlob: this.sintesisAman(k)
      });
    }
    void this.jalankan();
  }

  /** Aliran selesai: sisa tanpa tanda baca penutup tetap disintesis & diucapkan. */
  tutup() {
    if (this.berhentiTotal) return;
    const sisa = this.sisa.trim();
    if (sisa) {
      this.antrean.push({
        teks: sisa,
        promiseBlob: this.sintesisAman(sisa)
      });
      this.sisa = "";
    }
    this.ditutup = true;
    void this.jalankan();
  }

  sintesisAman(teks) {
    console.log("[antrean-suara] Meminta sintesis kalimat:", JSON.stringify(teks));
    return Promise.resolve(this.opsi.synthesize(teks)).then((blob) => {
      console.log("[antrean-suara] Sintesis BERHASIL untuk:", JSON.stringify(teks), "blob size:", blob?.size);
      return blob;
    }).catch((err) => {
      console.error("[antrean-suara] Galat sintesis:", JSON.stringify(teks), err);
      this.opsi.onGalat?.(err);
      return null;
    });
  }

  /** Tunggu sampai seluruh antrean audio selesai diputar. */
  async tungguSelesai() {
    if (this.berhentiTotal) return;
    if (this.ditutup && !this.antrean.length && !this.berjalan) return;
    await new Promise((resolve) => this.tunggu.push(resolve));
  }

  /** Hentikan semua pemutaran, antrean, dan animasi mulut secara instan. */
  berhenti() {
    this.berhentiTotal = true;
    this.antrean.length = 0;
    this.sisa = "";
    this.hentikanMulut();

    if (this.sumberBuffer) {
      try {
        this.sumberBuffer.stop();
        this.sumberBuffer.disconnect();
      } catch {}
      this.sumberBuffer = void 0;
    }

    if (this.audio) {
      try {
        this.audio.pause();
      } catch {}
      this.audio = void 0;
    }

    this.selesaikanSemua();
  }

  selesaikanSemua() {
    const daftar = this.tunggu;
    this.tunggu = [];
    for (const r of daftar) r();
  }

  async jalankan() {
    if (this.berjalan || this.berhentiTotal) return;
    this.berjalan = true;
    try {
      while (this.antrean.length && !this.berhentiTotal) {
        const item = this.antrean.shift();
        if (!item) continue;

        this.opsi.onKalimatMulai?.(item.teks);
        try {
          const blob = await item.promiseBlob;
          if (this.berhentiTotal) break;
          if (blob) {
            console.log("[antrean-suara] Memulai putar audio untuk:", JSON.stringify(item.teks));
            await this.putar(blob, item.teks);
            console.log("[antrean-suara] Selesai putar audio untuk:", JSON.stringify(item.teks));
          } else {
            console.warn("[antrean-suara] Blob kosong/gagal untuk:", JSON.stringify(item.teks));
            this.opsi.onKalimatPutar?.(item.teks, 0);
          }
        } catch (error) {
          console.error("[antrean-suara] Error saat memutar audio:", error);
          this.opsi.onKalimatPutar?.(item.teks, 0);
          this.opsi.onGalat?.(error);
        }
        this.opsi.onKalimatSelesai?.(item.teks);
      }
    } finally {
      this.berjalan = false;
      if (this.ditutup && !this.antrean.length) this.selesaikanSemua();
    }
  }

  async putar(blob, teks = "") {
    if (!blob || this.berhentiTotal) return;

    const konteks = dapatkanAudioContext();
    if (konteks) {
      try {
        console.log("[antrean-suara] AudioContext state saat ini:", konteks.state);
        if (konteks.state === "suspended") {
          await konteks.resume().catch((e) => console.warn("[antrean-suara] Gagal resume AudioContext:", e));
          console.log("[antrean-suara] AudioContext state setelah resume:", konteks.state);
        }

        const arrayBuffer = await blob.arrayBuffer();
        console.log("[antrean-suara] Men-decode audio data (byteLength:", arrayBuffer.byteLength, ")...");
        const audioBuffer = await konteks.decodeAudioData(arrayBuffer);
        console.log("[antrean-suara] Audio decoded: durasi =", audioBuffer.duration, "s, sampleRate =", audioBuffer.sampleRate);

        if (this.berhentiTotal) return;

        const sumber = konteks.createBufferSource();
        sumber.buffer = audioBuffer;
        this.sumberBuffer = sumber;

        // Karakter vokal Silver Wolf:
        // 1. Playback rate 1.15x mentransposisi nada vokal naik ~2.3 semitone,
        // mengubah vokal dewasa berita menjadi vokal anime girl / gamer yang ceria, lincah, dan imut.
        const rate = this.opsi.playbackRate ?? 1.15;
        sumber.playbackRate.value = rate;

        // 2. Filter EQ: potong resonansi dada berat pembaca berita (-3.5dB pada low)
        // dan angkat kejelasan treble (+3.5dB pada 3.2kHz) agar vokal jernih ala studio anime.
        const filterLow = konteks.createBiquadFilter();
        filterLow.type = "lowshelf";
        filterLow.frequency.value = 260;
        filterLow.gain.value = -3.5;

        const filterHigh = konteks.createBiquadFilter();
        filterHigh.type = "highshelf";
        filterHigh.frequency.value = 3200;
        filterHigh.gain.value = 3.5;

        const analyser = konteks.createAnalyser();
        analyser.fftSize = 1024;
        analyser.smoothingTimeConstant = 0.2;

        const gainNode = konteks.createGain();
        gainNode.gain.value = 1.0;
        this.gainNode = gainNode;

        sumber.connect(filterLow);
        filterLow.connect(filterHigh);
        filterHigh.connect(analyser);
        analyser.connect(gainNode);
        gainNode.connect(konteks.destination);

        this.gerakkanMulut(analyser);

        const durasiEfektif = audioBuffer.duration / rate;
        await new Promise((resolve) => {
          sumber.onended = () => {
            console.log("[antrean-suara] AudioBufferSource selesai berbunyi.");
            resolve();
          };
          this.opsi.onKalimatPutar?.(teks, durasiEfektif);
          sumber.start(0);
          console.log("[antrean-suara] AudioBufferSource.start(0) dipanggil!");
        });
        return;
      } catch (err) {
        console.warn("[antrean-suara] Web Audio decode gagal, mencoba fallback HTMLMediaElement:", err);
      } finally {
        this.hentikanMulut();
        this.sumberBuffer = void 0;
      }
    }

    // Fallback darurat jika AudioContext gagal
    console.log("[antrean-suara] Menggunakan fallback HTMLAudioElement...");
    const url = URL.createObjectURL(blob);
    const audio = new Audio(url);
    audio.volume = 1.0;
    this.audio = audio;
    try {
      this.opsi.onKalimatPutar?.(teks, 2.0);
      await audio.play();
      console.log("[antrean-suara] Fallback audio.play() berhasil dipanggil.");
      await new Promise((resolve) => {
        audio.onended = () => resolve();
        audio.onerror = (e) => {
          console.warn("[antrean-suara] Fallback audio error:", e);
          resolve();
        };
      });
    } catch (err) {
      console.warn("[antrean-suara] Gagal memutar fallback audio:", err);
    } finally {
      URL.revokeObjectURL(url);
      if (this.audio === audio) this.audio = void 0;
    }
  }

  hentikanMulut() {
    if (this.bingkai !== void 0) {
      cancelAnimationFrame(this.bingkai);
      this.bingkai = void 0;
    }
    this.opsi.onMulut?.(0);
  }

  /**
   * Ukur RMS dan kirim ke `onMulut`. Serangan cepat, pelepasan halus —
   * agar sinkronisasi bibir Live2D natural mengikuti gelombang suara.
   */
  gerakkanMulut(analyser) {
    const data = new Uint8Array(analyser.fftSize);
    let halus = 0;
    const langkah = () => {
      if (this.berhentiTotal) return;
      analyser.getByteTimeDomainData(data);
      let jumlah = 0;
      for (let i = 0; i < data.length; i++) {
        const s = (data[i] - 128) / 128;
        jumlah += s * s;
      }
      const rms = Math.sqrt(jumlah / data.length);
      const target = Math.min(1, rms * 5.2);
      halus = target > halus ? target : halus * 0.82 + target * 0.18;
      this.opsi.onMulut?.(halus);
      this.bingkai = requestAnimationFrame(langkah);
    };
    this.bingkai = requestAnimationFrame(langkah);
  }
}

export {
  AntreanSuara
};
