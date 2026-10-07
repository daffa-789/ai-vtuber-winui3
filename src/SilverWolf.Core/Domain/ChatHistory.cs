namespace SilverWolf.Core.Domain;

/// <summary>Satu pesan dalam riwayat percakapan.</summary>
public sealed class ChatMessage
{
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    /// <summary>Bagian alternatif bergaya Gemini (<c>parts[].text</c>).</summary>
    public List<string>? Parts { get; set; }

    public ChatMessage()
    {
    }

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Pembersih riwayat — port <c>rapikanRiwayat</c> dari
/// <c>apps/server-node/src/server.js</c>.
///
/// Menentukan apa yang boleh dikirim ke LLM: peran dinormalisasi ke
/// user/assistant, pesan kosong dibuang, konten dipotong, dan hanya
/// <c>maksPesan</c> pesan terakhir yang dipakai.
/// </summary>
public static class ChatHistory
{
    public static List<ChatMessage> Rapikan(
        IEnumerable<ChatMessage>? mentah,
        int maksPesan = 64,
        int maksKarakter = 8192)
    {
        var hasil = new List<ChatMessage>();
        if (mentah is null)
        {
            return hasil;
        }

        foreach (var item in mentah)
        {
            if (item is null)
            {
                continue;
            }

            var isi = item.Content ?? string.Empty;
            if (isi.Length == 0 && item.Parts is { Count: > 0 })
            {
                isi = string.Join(' ', item.Parts.Where(p => p is not null));
            }

            if (isi.Trim().Length == 0)
            {
                continue;
            }

            var peran = item.Role is "assistant" or "model" ? "assistant" : "user";
            hasil.Add(new ChatMessage(peran, isi.Length > maksKarakter ? isi[..maksKarakter] : isi));
        }

        return hasil.Count > maksPesan ? [.. hasil.TakeLast(maksPesan)] : hasil;
    }
}
