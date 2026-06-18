using CommunityToolkit.Mvvm.ComponentModel;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public ConfigViewModel ConfigViewModel { get; }
    public DashboardViewModel DashboardViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public MainViewModel(
        ConfigViewModel configViewModel,
        DashboardViewModel dashboardViewModel,
        LogsViewModel logsViewModel)
    {
        ConfigViewModel = configViewModel;
        DashboardViewModel = dashboardViewModel;
        LogsViewModel = logsViewModel;
        _selectedTabIndex = configViewModel.HasLoadedProfile ? 0 : 1;
    }
}
