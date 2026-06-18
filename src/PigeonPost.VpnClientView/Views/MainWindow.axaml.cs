using System;
using Avalonia.Controls;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Resized += OnResized;
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.UpdateLayout(e.ClientSize.Width);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel vm)
            vm.UpdateLayout(Bounds.Width);
    }
}
