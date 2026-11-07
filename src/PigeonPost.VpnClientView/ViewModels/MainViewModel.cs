using CommunityToolkit.Mvvm.ComponentModel;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isWideLayout;

    [ObservableProperty]
    private bool _isNarrowLayout = true;

    [ObservableProperty]
    private bool _isDashboardTab = true;

    [ObservableProperty]
    private bool _isConfigTab;

    [ObservableProperty]
    private bool _isLogsTab;

    [ObservableProperty]
    private bool _isAboutTab;

    public ConfigViewModel ConfigViewModel { get; }
    public DashboardViewModel DashboardViewModel { get; }
    public LogsViewModel LogsViewModel { get; }
    public AboutViewModel AboutViewModel { get; }

    public MainViewModel(
        ConfigViewModel configViewModel,
        DashboardViewModel dashboardViewModel,
        LogsViewModel logsViewModel,
        AboutViewModel aboutViewModel)
    {
        ConfigViewModel = configViewModel;
        DashboardViewModel = dashboardViewModel;
        LogsViewModel = logsViewModel;
        AboutViewModel = aboutViewModel;
        SelectedTabIndex = configViewModel.HasLoadedProfile ? 0 : 1;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        IsDashboardTab = value == 0;
        IsConfigTab = value == 1;
        IsLogsTab = value == 2;
        IsAboutTab = value == 3;
    }

    public void UpdateLayout(double width)
    {
        IsWideLayout = width >= 700;
        IsNarrowLayout = !IsWideLayout;
    }
}
