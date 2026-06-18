using CommunityToolkit.Mvvm.ComponentModel;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public ConfigViewModel ConfigViewModel { get; }

    public MainViewModel(ConfigViewModel configViewModel)
    {
        ConfigViewModel = configViewModel;
        _selectedTabIndex = configViewModel.HasLoadedProfile ? 0 : 1;
    }
}
