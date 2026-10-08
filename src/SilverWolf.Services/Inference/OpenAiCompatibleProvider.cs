using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SilverWolf.Core.Domain;
using SilverWolf.Core.Inference;

namespace SilverWolf.Services.Inference;

/// <summary>
/// Provider OpenAI-compatible (llama-server /v1, atau Ollama /api) — port
/// <c>OpenAiCompatibleProvider</c> dari <c>apps/server-node/src/inference.js</c>.
///
/// Detail yang sengaja dipertahankan karena menentukan perilaku yang dirasakan
/// pengguna saat model GGUF sedang dimuat ke VRAM:
/// - percobaan ulang hanya untuk llama-server, bukan Ollama;
/// - jendela percobaan ulang 45 detik sejak panggilan pertama;
/// - jeda 1500 ms bila koneksi gagal, 1200 ms bila balasannya HTTP 503.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private const int BatasWaktuMs = 45000;
    private const int JedaGalatMs = 1500;
    private const int JedaMuatMs = 1200;
    private const int BatasSehatMs = 2000;

    private static readonly JsonSerializerOptions OpsiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    public string Id { get; }

    public string Model { get; }

    /// <summary>URL dasar tanpa garis miring di ekor — sama seperti konstruktor asli.</summary>
    public string BaseUrl { get; }

    public bool Ollama => Id == "ollama";

    public OpenAiCompatibleProvider(string id, string baseUrl, string model, HttpClient? http = null)
    {
        Id = id;
        BaseUrl = baseUrl.TrimEnd('/');
        Model = model;
        _http = http ?? new HttpClient();
    }

    public async Task<ProviderAvailability> AvailableAsync(CancellationToken ct = default)
    {
        var path = Ollama ? "/api/tags" : "/health";
        using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
        batas.CancelAfter(BatasSehatMs);

        try
        {
            using var res = await _http.GetAsync(BaseUrl + path, batas.Token).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
            {
                return new ProviderAvailability { Ok = true, Reason = "siap" };
            }

            if ((int)res.StatusCode == 503)
            {
                // Bentuk badan 503 llama-server yang sebenarnya adalah
                //   {"error":{"message":"Loading model","type":"unavailable_error","code":503}}
                // bukan {"status":"loading model"} seperti dugaan semula. Kode
                // sebelumnya mencari properti "status" yang tidak pernah ada,
                // sehingga model yang sedang dimuat dilaporkan "tidak-jalan"
                // dan tombolnya menampilkan OFFLINE (docs/PROYEK.md §8.6).
                // Pengenalan bentuknya dipindah ke HealthProbe di Core supaya
                // bisa dikunci unit test.
                var badan = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (HealthProbe.MenandakanSedangMemuat((int)res.StatusCode, badan))
                {
                    return new ProviderAvailability { Ok = false, Loading = true, Reason = "memuat model ke VRAM..." };
                }
            }

            return new ProviderAvailability { Ok = false, Reason = $"HTTP {(int)res.StatusCode}" };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ProviderAvailability { Ok = false, Reason = "waktu tunggu health habis" };
        }
        catch (Exception error)
        {
            return new ProviderAvailability { Ok = false, Reason = error.Message };
        }
    }

    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? opts = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        opts ??= new LlmOptions();
        var path = Ollama ? "/api/chat" : "/v1/chat/completions";

        object body = Ollama
            ? new
            {
                model = Model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                options = new { temperature = opts.Temperature },
            }
            : new
            {
                model = Model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                max_tokens = opts.MaxTokens,
                temperature = opts.Temperature,
            };

        var batasWaktu = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + BatasWaktuMs;

        HttpResponseMessage? res = null;
        Exception? galat = null;
        var berhenti = false;

        while (true)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
                req.Content = JsonContent.Create(body, options: OpsiJson);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                berhenti = true;
                break;
            }
            catch (Exception error)
            {
                if (!Ollama && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < batasWaktu)
                {
                    if (!await TungguAsync(JedaGalatMs, ct).ConfigureAwait(false))
                    {
                        berhenti = true;
                    }

                    continue;
                }

                galat = error;
                berhenti = true;
                break;
            }

            if ((int)res.StatusCode == 503
                && !Ollama
                && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < batasWaktu)
            {
                res.Dispose();
                res = null;
                if (!await TungguAsync(JedaMuatMs, ct).ConfigureAwait(false))
                {
                    berhenti = true;
                }

                continue;
            }

            break;
        }

        if (berhenti)
        {
            if (galat is not null)
            {
                yield return StreamChunk.Galat(galat);
            }

            yield break;
        }

        if (res is null)
        {
            yield break;
        }

        using (res)
        {
            if (!res.IsSuccessStatusCode)
            {
                var teks = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                yield return StreamChunk.Galat($"{Id} HTTP {(int)res.StatusCode}: {PesanError(teks)}");
                yield break;
            }

            var aliran = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var baca = new StreamReader(aliran);

            while (!ct.IsCancellationRequested)
            {
                var mentah = await baca.ReadLineAsync().ConfigureAwait(false);
                if (mentah is null)
                {
                    break;
                }

                var bersih = mentah.Trim();
                if (bersih.Length == 0)
                {
                    continue;
                }

                var payload = Ollama ? bersih : System.Text.RegularExpressions.Regex.Replace(bersih, @"^data:\s*", string.Empty);
                if (payload == "[DONE]")
                {
                    break;
                }

                JsonDocument data;
                try
                {
                    data = JsonDocument.Parse(payload);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (data)
                {
                    var isi = Ollama ? AmbilOllama(data.RootElement) : AmbilOpenAi(data.RootElement);
                    if (string.IsNullOrEmpty(isi))
                    {
                        continue;
                    }

                    yield return StreamChunk.Dari(isi!);
                }
            }
        }
    }

    /// <summary>Jeda percobaan ulang; mengembalikan false bila dibatalkan.</summary>
    private static async Task<bool> TungguAsync(int ms, CancellationToken ct)
    {
        try
        {
            await Task.Delay(ms, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static string? AmbilOllama(JsonElement root) =>
        root.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c)
            ? c.GetString()
            : null;

    private static string? AmbilOpenAi(JsonElement root) =>
        root.TryGetProperty("choices", out var choices)
        && choices.ValueKind == JsonValueKind.Array
        && choices.GetArrayLength() > 0
        && choices[0].TryGetProperty("delta", out var delta)
        && delta.TryGetProperty("content", out var c)
            ? c.GetString()
            : null;

    /// <summary>Port <c>pesanError(teks)</c>.</summary>
    private static string PesanError(string teks)
    {
        try
        {
            using var parsed = JsonDocument.Parse(teks);
            if (parsed.RootElement.TryGetProperty("error", out var e))
            {
                if (e.ValueKind == JsonValueKind.String)
                {
                    return e.GetString() ?? string.Empty;
                }

                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m))
                {
                    return m.ToString();
                }
            }
        }
        catch (JsonException)
        {
            // bukan JSON
        }

        return "respons inferensi tidak valid";
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
