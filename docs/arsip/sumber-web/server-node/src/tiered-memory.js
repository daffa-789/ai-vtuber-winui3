/**
 * Tiered Memory Engine — Sistem Memori Tiga Tingkat (@aituber-onair/core)
 *
 * Menggabungkan:
 *  1. Short-Term Memory: Buffer putaran percakapan aktif terkini.
 *  2. Mid-Term Memory: MemoryManager dari @aituber-onair/core untuk sesi & topik.
 *  3. Long-Term Memory: CharacterVault (Fakta.md, Mood.md, Riwayat harian) di silver_wolf_memory/.
 */
import { MemoryManager } from '@aituber-onair/core'

export class TieredMemoryEngine {
  constructor(vault, options = {}) {
    this.vault = vault
    this.shortTermBuffer = []
    this.maxShortTerm = options.maxShortTerm ?? 12

    this.coreMemory = new MemoryManager({
      enableSummarization: false,
      shortTermDuration: 30 * 60 * 1000,
      midTermDuration: 24 * 60 * 60 * 1000,
      longTermDuration: 30 * 24 * 60 * 60 * 1000,
      maxMessagesBeforeSummarization: 10,
      maxSummaryLength: 300,
    })
  }

  async readAll() {
    let fakta = []
    let mood
    if (this.vault?.available()) {
      try {
        fakta = await this.vault.bacaFakta()
      } catch (err) {
        console.error('[Memory] Gagal membaca fakta:', err)
      }
      try {
        mood = await this.vault.bacaMood()
      } catch (err) {
        console.error('[Memory] Gagal membaca mood:', err)
      }
    }

    const midTermPrompt = this.coreMemory.getMemoryForPrompt()
    return {
      fakta,
      mood,
      midTermPrompt,
      shortTerm: [...this.shortTermBuffer],
    }
  }

  async recordTurn(userText, assistantText) {
    if (userText) {
      this.shortTermBuffer.push({ role: 'user', content: userText, timestamp: Date.now() })
    }
    if (assistantText) {
      this.shortTermBuffer.push({ role: 'assistant', content: assistantText, timestamp: Date.now() })
    }
    while (this.shortTermBuffer.length > this.maxShortTerm * 2) {
      this.shortTermBuffer.shift()
    }

    // Perbarui ingatan @aituber-onair/core
    try {
      await this.coreMemory.createMemoryIfNeeded(
        this.shortTermBuffer.map(m => ({ role: m.role, content: m.content })),
        Date.now() - 60000
      )
    } catch (err) {
      console.warn('[Memory] Mid-term memory create error:', err)
    }
  }
}
