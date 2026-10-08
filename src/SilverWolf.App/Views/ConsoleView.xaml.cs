using Microsoft.UI.Xaml.Controls;

namespace SilverWolf.App.Views;

/// <summary>
/// Panel kanan — HANYA percakapan.
///
/// Sejak 2026-10-08 panel ini tidak lagi punya header, kartu HUD, atau
/// telemetri: tiga blok itu dibuang dari XAML atas permintaan Master ("saya
/// butuh chatnya aja"). Karena itu handler <c>OnNeuroDiklik</c> ikut dihapus —
/// tombolnya sudah tidak ada, dan membiarkan handler tanpa pemakai membuat
/// pemeriksaan "tidak ada kode mati" berbohong.
///
/// Mode otonom sendiri TIDAK hilang: nilainya tetap hidup di
/// <c>CompanionViewModel.AutonomousMode</c> dan tetap dipakai pekerja proaktif.
/// Kalau tombolnya diperlukan lagi, cukup pasang kembali Button dengan
/// <c>Command="{Binding PancingObrolanCommand}"</c> — tanpa perlu handler.
/// </summary>
public sealed partial class ConsoleView : UserControl
{
    public ConsoleView()
    {
        InitializeComponent();
    }
}
