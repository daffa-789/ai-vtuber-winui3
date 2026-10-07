using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverWolf.App.ViewModels;

namespace SilverWolf.App.Views;

/// <summary>Panel kanan — port komponen <c>Konsol.jsx</c>.</summary>
public sealed partial class ConsoleView : UserControl
{
    public ConsoleView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Mode otonom gaya Neuro-sama. Nilainya hidup di
    /// <c>CompanionViewModel.AutonomousMode</c>; tombol ini hanya membaliknya,
    /// sedangkan teksnya mengikuti <c>TeksNeuro</c>.
    /// </summary>
    private void OnNeuroDiklik(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompanionViewModel vm)
        {
            vm.AutonomousMode = !vm.AutonomousMode;
        }
    }
}
