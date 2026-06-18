using CommunityToolkit.Mvvm.ComponentModel;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public int DefaultTabIndex => 0;

    public ConfigViewModel ConfigViewModel { get; }

    public MainViewModel(ConfigViewModel configViewModel)
    {
        _selectedTabIndex = DefaultTabIndex;
        ConfigViewModel = configViewModel;
    }
}
