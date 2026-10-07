/**
 * Agen percakapan: menyusun prompt, mengalirkan balasan, menyimpan mood,
 * ikatan Kizuna (@aituber-onair/kizuna), dan memori bertingkat (@aituber-onair/core).
 */
import { bacaTagAwal, gabungSystem, perbaruiMood } from './character.js'
import { buatPromptProaktif, buatPromptSentuhan } from './proactive.js'

const spasiGanda = /\s+/g

export class Agent {
  constructor(o) {
    this.provider = o.provider
    this.persona = o.persona
    this.vault = o.vault
    this.kizuna = o.kizuna
    this.memoryEngine = o.memoryEngine
    this.lokal = o.provider.id === 'ollama' ? false : Boolean(o.localPrompt)
    this.onError = o.onError ?? (error => console.error('memori:', error))
  }

  get localPrompt() {
    return this.lokal
  }

  async memory() {
    let fakta = []
    let mood
    let midTermPrompt = ''
    if (this.memoryEngine) {
      try {
        const mem = await this.memoryEngine.readAll()
        fakta = mem.fakta ?? []
        mood = mem.mood
        midTermPrompt = mem.midTermPrompt ?? ''
      } catch (error) {
        this.onError(error)
      }
    } else if (this.vault?.available()) {
      try {
        fakta = await this.vault.bacaFakta()
      } catch (error) {
        this.onError(error)
      }
      try {
        mood = await this.vault.bacaMood()
      } catch (error) {
        this.onError(error)
      }
    }

    let kizunaContext = ''
    if (this.kizuna) {
      try {
        kizunaContext = this.kizuna.getBondContext()
      } catch (error) {
        this.onError(error)
      }
    }

    return { fakta, mood, midTermPrompt, kizunaContext }
  }

  async *chat(riwayat, opts = {}, signal) {
    const mem = await this.memory()
    const pesan = [
      {
        role: 'system',
        content: gabungSystem(
          this.persona,
          mem.fakta,
          mem.mood,
          this.lokal,
          mem.kizunaContext,
          mem.midTermPrompt
        ),
      },
      ...riwayat.filter(m => m.role !== 'system'),
    ]

    let jawaban = ''
    for await (const potongan of this.provider.stream(pesan, opts, signal)) {
      if (potongan.err) {
        yield potongan
        return
      }
      jawaban += potongan.text ?? ''
      yield potongan
    }

    if (jawaban.length > 0) {
      try {
        await this.persist(riwayat, jawaban, mem)
      } catch (error) {
        this.onError(error)
      }
    }
  }

  async *chatProactive(idleDetik = 60, opts = {}, signal) {
    const snapshot = this.kizuna?.getSnapshot()
    const promptProaktif = buatPromptProaktif(snapshot, idleDetik)
    const riwayat = [
      {
        role: 'user',
        content: `[Kondisi: kamu sedang menatap layar dan memperhatikan Master/pacarmu yang sedang hening]. ${promptProaktif}`,
      },
    ]

    for await (const chunk of this.chat(riwayat, { ...opts, maxTokens: 256, temperature: 0.8 }, signal)) {
      yield chunk
    }
  }

  async *chatTouch(opts = {}, signal) {
    let snapshot = this.kizuna?.getSnapshot()
    if (this.kizuna) {
      try {
        const touchRes = await this.kizuna.recordTouch()
        snapshot = touchRes.snapshot
      } catch (err) {
        this.onError(err)
      }
    }

    const promptSentuhan = buatPromptSentuhan(snapshot)
    const riwayat = [
      {
        role: 'user',
        content: `[Interaksi Fisik: Master baru saja mengelus kepalamu dengan lembut]. ${promptSentuhan}`,
      },
    ]

    for await (const chunk of this.chat(riwayat, { ...opts, maxTokens: 256, temperature: 0.85 }, signal)) {
      yield chunk
    }
  }

  async persist(riwayat, jawaban, mem) {
    let ucapan = ''
    for (let i = riwayat.length - 1; i >= 0; i--) {
      if (riwayat[i]?.role === 'user') {
        ucapan = riwayat[i].content
        break
      }
    }

    // Catat ke memori bertingkat
    if (this.memoryEngine) {
      try {
        await this.memoryEngine.recordTurn(ucapan, jawaban)
      } catch (err) {
        this.onError(err)
      }
    }

    // Catat ke sistem ikatan Kizuna
    if (this.kizuna) {
      try {
        await this.kizuna.recordMessage(jawaban)
      } catch (err) {
        this.onError(err)
      }
    }

    // Catat ke CharacterVault
    if (this.vault?.available()) {
      const mood = perbaruiMood(mem.mood, bacaTagAwal(jawaban))
      await this.vault.simpanMood(mood)

      const ringkas = s => {
        const padat = s.replace(spasiGanda, ' ').trim()
        return padat.length > 240 ? padat.slice(0, 240) : padat
      }
      await this.vault.catatHari(`Master: ${ringkas(ucapan)} | Silver Wolf: ${ringkas(jawaban)}`)
    }
  }
}
