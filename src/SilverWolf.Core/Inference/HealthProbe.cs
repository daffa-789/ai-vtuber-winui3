using System.Text.Json;

namespace SilverWolf.Core.Inference;

/// <summary>
/// Membaca badan respons <c>/health</c> milik llama-server untuk membedakan
/// "sedang memuat model" dari "tidak jalan".
///
/// Kenapa diletakkan di Core: keputusannya murni pemeriksaan JSON, sehingga
/// bisa diuji dari mesin mana pun. Pemanggilnya
/// (<c>OpenAiCompatibleProvider</c>) ada di SilverWolf.Services yang ber-TFM
/// <c>windows</c>, jadi menaruh logika ini di sana membuatnya tidak terjangkau
/// unit test.
///
/// Bentuk badan yang HARUS dikenali — diambil apa adanya dari llama-server saat
/// memuat GGUF ke VRAM:
/// <code>{"error":{"message":"Loading model","type":"unavailable_error","code":503}}</code>
///
/// Bentuk lama yang dicari kode sebelumnya dan <b>tidak pernah muncul</b>:
/// <code>{"status":"loading model"}</code>
///
/// Lihat docs/PROYEK.md §8.6.
/// </summary>
public static class HealthProbe
{
    /// <summary>
    /// Penanda pada badan 503 llama-server. Dicocokkan tanpa peduli besar-kecil
    /// huruf: "Loading model" hanyalah teks manusia, bukan bagian kontrak API.
    /// </summary>
    private const string PenandaMemuat = "loading model";

    /// <summary>
    /// True bila server menjawab <b>503</b> karena sedang memuat model.
    ///
    /// Sengaja mensyaratkan 503: 200 berarti siap, dan kode lain (404/500)
    /// memang "tidak jalan" — bukan "tunggu sebentar". Badan yang bukan JSON,
    /// kosong, atau tanpa penanda apa pun dikembalikan false supaya kegagalan
    /// nyata tidak tersamar menjadi "memuat".
    /// </summary>
    public static bool MenandakanSedangMemuat(int statusCode, string? body)
    {
        if (statusCode != 503 || string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var dokumen = JsonDocument.Parse(body);
            var akar = dokumen.RootElement;

            if (akar.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Bentuk llama-server: {"error":{"message":"Loading model",...}}
            if (akar.TryGetProperty("error", out var galat)
                && galat.ValueKind == JsonValueKind.Object
                && TeksMemuat(galat, "message"))
            {
                return true;
            }

            // Bentuk lama: {"status":"loading model"}
            return TeksMemuat(akar, "status");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TeksMemuat(JsonElement induk, string nama)
    {
        return induk.TryGetProperty(nama, out var nilai)
            && nilai.ValueKind == JsonValueKind.String
            && nilai.GetString() is { } teks
            && teks.Contains(PenandaMemuat, StringComparison.OrdinalIgnoreCase);
    }
}
