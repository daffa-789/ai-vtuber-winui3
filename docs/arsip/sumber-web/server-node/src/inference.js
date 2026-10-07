/**
 * Provider inferensi LLM untuk server Node (OpenAI-compatible SSE & Stub).
 */
const decoder = new TextDecoder('utf-8')

function pesanError(teks) {
  try {
    const parsed = JSON.parse(teks)
    const e = parsed?.error
    if (typeof e === 'string') return e
    if (e && typeof e === 'object' && 'message' in e) return String(e.message)
  } catch {
    /* bukan JSON */
  }
  return 'respons inferensi tidak valid'
}

export class OpenAiCompatibleProvider {
  constructor(id, baseUrl, model) {
    this.id = id
    this.baseUrl = baseUrl.replace(/\/+$/, '')
    this.model = model
  }

  async available() {
    const path = this.id === 'ollama' ? '/api/tags' : '/health'
    try {
      const res = await fetch(this.baseUrl + path, { signal: AbortSignal.timeout(2000) })
      if (res.ok) return { ok: true, reason: 'siap' }
      if (res.status === 503) {
        const body = await res.json().catch(() => null)
        if (body?.status === 'loading model') {
          return { ok: false, loading: true, reason: 'memuat model ke VRAM...' }
        }
      }
      return { ok: false, reason: `HTTP ${res.status}` }
    } catch (error) {
      return { ok: false, reason: error instanceof Error ? error.message : String(error) }
    }
  }

  async dispose() {
    /* provider jaringan tidak menyimpan sumber daya */
  }

  async *stream(messages, opts = {}, signal) {
    const ollama = this.id === 'ollama'
    const path = ollama ? '/api/chat' : '/v1/chat/completions'
    const body = ollama
      ? { model: this.model, messages, stream: true, options: { temperature: opts.temperature } }
      : { model: this.model, messages, stream: true, max_tokens: opts.maxTokens, temperature: opts.temperature }

    let res
    const batasWaktu = Date.now() + 45000
    while (true) {
      try {
        res = await fetch(this.baseUrl + path, {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify(body),
          signal,
        })
      } catch (error) {
        if (!ollama && Date.now() < batasWaktu && !signal?.aborted) {
          await new Promise(r => setTimeout(r, 1500))
          continue
        }
        yield { err: error instanceof Error ? error : new Error(String(error)) }
        return
      }

      if (res.status === 503 && !ollama && Date.now() < batasWaktu) {
        // Model GGUF sedang dimuat ke Vulkan VRAM oleh llama-server, tunggu dan coba lagi
        await new Promise(r => setTimeout(r, 1200))
        if (signal?.aborted) return
        continue
      }
      break
    }

    if (!res.ok) {
      const teks = await res.text().catch(() => '')
      yield { err: new Error(`${this.id} HTTP ${res.status}: ${pesanError(teks)}`) }
      return
    }
    if (!res.body) {
      yield { err: new Error('respons inferensi tanpa body') }
      return
    }

    const reader = res.body.getReader()
    let sisa = ''
    try {
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        sisa += decoder.decode(value, { stream: true })
        const baris = sisa.split('\n')
        sisa = baris.pop() ?? ''
        for (const mentah of baris) {
          const bersih = mentah.trim()
          if (!bersih) continue
          const payload = ollama ? bersih : bersih.replace(/^data:\s*/, '')
          if (payload === '[DONE]') return
          let data
          try {
            data = JSON.parse(payload)
          } catch {
            continue
          }
          const isi = ollama
            ? (data.message?.content ?? '')
            : (data.choices?.[0]?.delta?.content ?? '')
          if (!isi) continue
          yield { text: isi }
        }
      }
    } finally {
      try {
        reader.releaseLock()
      } catch {
        /* sudah dilepas */
      }
    }
  }
}

export class StubProvider {
  constructor() {
    this.id = 'llama-server'
  }

  async available() {
    return { ok: true, reason: 'stub' }
  }

  async dispose() {
    /* tidak ada sumber daya */
  }

  async *stream(_messages, _opts, signal) {
    for (const potongan of ['[senyum] ', 'Sistem inti sudah hidup, ', 'Master.']) {
      if (signal?.aborted) return
      yield { text: potongan }
    }
  }
}
