using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IProfileStore, DesktopProfileStore>();
            services.AddSingleton<IVpnRuntime, FakeVpnRuntime>();
            services.AddSingleton<ConfigViewModel>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<LogsViewModel>();
            services.AddSingleton<MainViewModel>();
            var provider = services.BuildServiceProvider();

            var vm = provider.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
