using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView;

public partial class App : Application
{
    private ServiceProvider? _services;
    private IVpnRuntime? _runtime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var services = new ServiceCollection();
        services.AddSingleton<IProfileStore, DesktopProfileStore>();
        services.AddSingleton<IVpnRuntime, VpnClientRuntime>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();
        _services = services.BuildServiceProvider();

        _runtime = _services.GetRequiredService<IVpnRuntime>();

        var vm = _services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = vm
            };

            desktop.MainWindow.Closing += OnMainWindowClosing;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new Views.MainView
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var rt = _runtime;
        if (rt == null)
            return;

        _runtime = null;

        if (rt.State == ConnectionState.Disconnected)
        {
            if (rt is IDisposable d)
                d.Dispose();
            return;
        }

        e.Cancel = true;

        rt.DisconnectAsync().ContinueWith(_ =>
        {
            if (rt is IDisposable d)
                d.Dispose();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.MainWindow?.Close();
            });
        });
    }
}
