using Avalonia.Controls;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.UpdateLayout(Bounds.Width);
    }
}
