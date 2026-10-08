using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

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
    private bool _sedangMemuat;
    private string? _error;
    private string _teksMemuat = "sedang berpikir…";
    private string _catatanSuara = string.Empty;

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
        set
        {
            SetProperty(ref _pending, value);
            // Termasuk early-return HTTP, exception, dan pembatalan.
            if (!value) SedangMemuat = false;
        }
    }

    /// <summary>
    /// Gelembung sedang menunggu hasil — menampilkan animasi loading.
    ///
    /// <para>
    /// <b>Kenapa ada properti terpisah dari <see cref="Pending"/>:</b>
    /// <c>Pending</c> berarti "aliran balasan belum selesai" dan isinya sudah
    /// terlihat sebagian. <c>SedangMemuat</c> berarti "belum ada yang bisa
    /// ditampilkan sama sekali" — dipakai untuk dua jeda yang benar-benar
    /// kosong: (1) sebelum token pertama datang dari model, dan (2) setelah
    /// teks lengkap tetapi sebelum suara RVC siap diputar (lihat
    /// <c>AlirkanAsync</c>, alur "suara dulu baru chat"). Tanpa penanda ini,
    /// gelembung tampak kosong dan Master mengira aplikasi menggantung.
    /// </para>
    /// </summary>
    public bool SedangMemuat
    {
        get => _sedangMemuat;
        set
        {
            if (SetProperty(ref _sedangMemuat, value))
            {
                OnPropertyChanged(nameof(VisibilitasMemuat));
                OnPropertyChanged(nameof(VisibilitasKonten));
            }
        }
    }

    public string TeksMemuat
    {
        get => _teksMemuat;
        set => SetProperty(ref _teksMemuat, value);
    }

    public string CatatanSuara
    {
        get => _catatanSuara;
        set => SetProperty(ref _catatanSuara, value);
    }

    public string? Error
    {
        get => _error;
        set
        {
            if (!SetProperty(ref _error, value)) return;
            OnPropertyChanged(nameof(TeksGalat));
            OnPropertyChanged(nameof(PunyaGalat));
            if (value is not null) Pending = false;
        }
    }

    /// <summary>
    /// Visibilitas untuk elemen animasi loading. Dipakai langsung dari XAML
    /// (<c>Visibility="{Binding VisibilitasMemuat}"</c>) supaya tidak perlu
    /// konverter tambahan — pola yang sama dengan <see cref="TeksGalat"/>.
    /// </summary>
    public Visibility VisibilitasMemuat => _sedangMemuat
        ? Visibility.Visible
        : Visibility.Collapsed;

    /// <summary>
    /// Kebalikan sederhana: teks isi disembunyikan selama animasi loading
    /// tampil. Ini yang memberi efek "chat menunggu suara dulu baru muncul".
    /// </summary>
    public Visibility VisibilitasKonten => _sedangMemuat
        ? Visibility.Collapsed
        : Visibility.Visible;

    /// <summary>Teks yang ditampilkan bila gelembung ini gagal.</summary>
    public string TeksGalat => Error is null ? string.Empty : $"⚠ {Error}";

    public bool PunyaGalat => Error is not null;
}
