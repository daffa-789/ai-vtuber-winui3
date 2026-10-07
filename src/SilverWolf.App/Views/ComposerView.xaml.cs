using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SilverWolf.App.Diagnostics;
using SilverWolf.App.ViewModels;
using Windows.System;

namespace SilverWolf.App.Views;

/// <summary>Kotak input — port komponen <c>Komposer</c>.</summary>
public sealed partial class ComposerView : UserControl
{
    public ComposerView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Enter mengirim pesan.
    ///
    /// Dipasang ke <c>PreviewKeyDown</c> DAN <c>KeyDown</c> di XAML, keduanya ke
    /// penangan ini. <c>PreviewKeyDown</c> berjalan lebih dulu; ia menandai
    /// kejadiannya sudah ditangani sehingga <c>KeyDown</c> tidak menyala lagi —
    /// tidak ada pengiriman ganda. Cadangannya ada karena perilaku Enter pada
    /// <c>TextBox</c> WinUI 3 bergantung versi, dan di lingkungan pengembangan
    /// ini tidak bisa diuji secara interaktif (input sintetis tidak sampai ke
    /// aplikasi), jadi lebih aman menangkap keduanya.
    ///
    /// <c>KeyboardAccelerator</c> sengaja tidak dipakai: kotak ini
    /// <c>AcceptsReturn="False"</c> sehingga <c>TextBox</c> menangani Enter
    /// lebih dulu dan accelerator tidak pernah kebagian.
    ///
    /// Modifier sengaja tidak diperiksa: kotaknya satu baris, jadi Shift+Enter
    /// tidak akan menghasilkan baris baru apa pun.
    /// </summary>
    private void OnKotakPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        // Selalu tandai ditangani — walau perintahnya tidak bisa dijalankan —
        // supaya Enter tidak memicu perilaku bawaan.
        e.Handled = true;

        if (DataContext is not CompanionViewModel vm)
        {
            return;
        }

        // Dicatat supaya "Enter saya tidak mengirim apa-apa" bisa dibedakan
        // antara tiga sebab yang tampak sama dari luar: handler tidak jalan,
        // draf kosong (fokus tidak di kotak), atau perintah sedang terkunci.
        // Isi draf sengaja TIDAK ditulis — hanya panjangnya.
        CrashLog.Tahap($"composer: Enter, panjang draf={vm.Draf.Length}, "
                       + $"bisaKirim={vm.KirimCommand.CanExecute(null)}");

        // CanExecute sudah menampung IsSending dan Draf kosong, jadi tidak perlu
        // memeriksa ulang di sini — dan pesan kosong memang tidak terkirim.
        if (vm.KirimCommand.CanExecute(null))
        {
            vm.KirimCommand.Execute(null);
        }
    }
}
