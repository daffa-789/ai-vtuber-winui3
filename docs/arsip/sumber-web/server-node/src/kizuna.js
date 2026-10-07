/**
 * Kizuna Engine — Sistem Ikatan Hubungan (@aituber-onair/kizuna)
 *
 * Mengelola level kedekatan (bond), afeksi, warmth, dan interaksi sentuhan (headpat)
 * antara Silver Wolf dan Master/Pacar.
 */
import { existsSync } from 'node:fs'
import fs from 'node:fs/promises'
import path from 'node:path'
import { createDefaultKizunaConfig, ExternalStorageProvider, KizunaManager } from '@aituber-onair/kizuna'

export const BOND_STAGES_INFO = {
  stranger: {
    level: 1,
    name: 'Stranger',
    label: 'Hacker Waspada',
    minPoints: 0,
    nextPoints: 100,
    tone: 'Skeptis, dingin, menganggap kamu pengguna biasa atau rival hacker.',
  },
  acquaintance: {
    level: 2,
    name: 'Acquaintance',
    label: 'Teman Mabar',
    minPoints: 100,
    nextPoints: 400,
    tone: 'Santai, suka mengajak mabar game, mulai memanggil Master dengan nada ramah.',
  },
  regular: {
    level: 3,
    name: 'Regular',
    label: 'Partner Hacking',
    minPoints: 400,
    nextPoints: 1000,
    tone: 'Akrab dan percaya, berbagi strategi game, cerita tentang misi Stellaron Hunters.',
  },
  companion: {
    level: 4,
    name: 'Companion',
    label: 'Gamer Girlfriend',
    minPoints: 1000,
    nextPoints: 2000,
    tone: 'Pacar gamer yang manis dan perhatian, manja saat lelah, suka digombalin dan cemburu tipis.',
  },
  lover: {
    level: 5,
    name: 'Lover',
    label: 'Pacar Tercinta (Bucin Tsundere)',
    minPoints: 2000,
    nextPoints: 5000,
    tone: 'Sangat sayang dan setia, posesif menggemaskan, menganggapmu orang paling berharga di seluruh galaksi.',
  },
}

function buatAdapterPenyimpanan() {
  return {
    async readFile(p) {
      try {
        return await fs.readFile(p, 'utf8')
      } catch {
        return ''
      }
    },
    async writeFile(p, data) {
      await fs.mkdir(path.dirname(p), { recursive: true })
      await fs.writeFile(p, data, 'utf8')
    },
    async exists(p) {
      return existsSync(p)
    },
    async deleteFile(p) {
      try {
        await fs.unlink(p)
      } catch {}
    },
    async listFiles(dir) {
      try {
        return await fs.readdir(dir)
      } catch {
        return []
      }
    },
    async ensureDir(dir) {
      await fs.mkdir(dir, { recursive: true })
    },
    joinPath(...args) {
      return path.join(...args)
    },
  }
}

export class SilverWolfKizuna {
  constructor(dataDir, userId = 'master') {
    this.dataDir = path.resolve(dataDir)
    this.userId = userId
    this.initialized = false
    this.manager = null
  }

  async initialize() {
    if (this.initialized) return
    const adapter = buatAdapterPenyimpanan()
    const storage = new ExternalStorageProvider(adapter, {
      dataDir: this.dataDir,
    })

    const config = createDefaultKizunaConfig()
    config.stages = [
      { id: 'stranger', minPoints: 0 },
      { id: 'acquaintance', minPoints: 100 },
      { id: 'regular', minPoints: 400 },
      { id: 'companion', minPoints: 1000 },
      { id: 'lover', minPoints: 2000 },
    ]
    config.basePoints = {
      message: 4,
      reaction: 3,
      gift: 25,
      presence: 1,
      touch: 8,
    }
    config.warmth = {
      halfLifeMs: 14 * 24 * 60 * 60 * 1000, // 14 hari
      floor: 0.35,
    }

    this.manager = new KizunaManager(config, storage, 'silverwolf_kizuna_v1')
    await this.manager.initialize()
    this.initialized = true
  }

  async recordMessage(text) {
    if (!this.initialized) await this.initialize()
    return await this.manager.processInteraction({
      userId: this.userId,
      kind: 'message',
      text: typeof text === 'string' ? text.slice(0, 300) : '',
      timestamp: Date.now(),
    })
  }

  async recordTouch() {
    if (!this.initialized) await this.initialize()
    const result = await this.manager.processInteraction({
      userId: this.userId,
      kind: 'touch',
      text: 'elusan kepala / headpat mesra di rambut',
      timestamp: Date.now(),
    })
    return {
      ...result,
      snapshot: this.getSnapshot(),
    }
  }

  getSnapshot() {
    if (!this.initialized || !this.manager) {
      return {
        userId: this.userId,
        stage: 'stranger',
        stageLabel: BOND_STAGES_INFO.stranger.label,
        stageName: BOND_STAGES_INFO.stranger.name,
        level: 1,
        points: 0,
        nextPoints: 100,
        progress: 0,
        warmth: 1,
        atmosphere: 'warm',
        trend: 'rising',
        tone: BOND_STAGES_INFO.stranger.tone,
      }
    }

    const raw = this.manager.getBondSnapshot(this.userId)
    const points = raw?.points ?? 0
    let stageId = raw?.stage ?? 'stranger'

    // Tentukan stage berdasarkan point jika belum tersinkron
    if (points >= 2000) stageId = 'lover'
    else if (points >= 1000) stageId = 'companion'
    else if (points >= 400) stageId = 'regular'
    else if (points >= 100) stageId = 'acquaintance'
    else stageId = 'stranger'

    const info = BOND_STAGES_INFO[stageId] ?? BOND_STAGES_INFO.stranger
    const minP = info.minPoints
    const nextP = info.nextPoints
    const range = Math.max(1, nextP - minP)
    const progress = Math.min(100, Math.max(0, Math.round(((points - minP) / range) * 100)))

    return {
      userId: this.userId,
      stage: stageId,
      stageLabel: info.label,
      stageName: info.name,
      level: info.level,
      points,
      nextPoints: nextP,
      progress,
      warmth: Math.min(1, Math.max(0, raw?.warmth ?? 1)),
      atmosphere: raw?.atmosphere ?? 'warm',
      trend: raw?.trend ?? 'rising',
      tone: info.tone,
      lastContact: raw?.continuity?.lastContactAt ?? new Date().toISOString(),
      streak: raw?.continuity?.streak ?? 1,
    }
  }

  getBondContext() {
    if (!this.initialized || !this.manager) return ''
    const snap = this.getSnapshot()
    const ctx = this.manager.getBondContext(this.userId)

    return [
      `## Hubungan & Ikatan (Kizuna Level ${snap.level} - ${snap.stageName}: "${snap.stageLabel}")`,
      `- Total Poin Ikatan: ${snap.points} XP (Kemajuan ke level berikutnya: ${snap.progress}%)`,
      `- Kehangatan Hubungan: ${(snap.warmth * 100).toFixed(0)}% (Suasana: ${snap.atmosphere}, Tren: ${snap.trend})`,
      `- Panduan Sikap: ${snap.tone}`,
      `- Raw Context: ${ctx.trim()}`,
    ].join('\n')
  }

  destroy() {
    this.manager?.destroy()
  }
}
