using CommunityToolkit.Mvvm.ComponentModel;

namespace SilverWolf.App.Models;

/// <summary>
/// Satu gelembung dalam <c>DaftarPesan</c> — port bentuk
/// <c>{ id, role, content, pending, error }</c> milik <c>store.js</c>.
///
/// Peran diberikan sekali (<c>init</c>), tetapi <c>Content</c> dan
/// <c>Pending</c> berubah terus selama balasan dialirkan. Karena itu kelas ini
/// memberi tahu perubahan — tanpa itu teks yang mengalir tidak akan pernah
/// muncul di layar.
/// </summary>
public sealed class ChatBubble : ObservableObject
{
    private string _content = string.Empty;
    private bool _pending;
    private string? _error;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>"user" | "assistant" — nilai yang dikenal <c>rapikanRiwayat</c>.</summary>
    public string Role { get; init; } = "user";

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public bool Pending
    {
        get => _pending;
        set => SetProperty(ref _pending, value);
    }

    public string? Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    /// <summary>Teks yang ditampilkan bila gelembung ini gagal.</summary>
    public string TeksGalat => Error is null ? string.Empty : $"⚠ {Error}";

    public bool PunyaGalat => Error is not null;
}
