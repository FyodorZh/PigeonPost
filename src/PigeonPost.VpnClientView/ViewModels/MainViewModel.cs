using CommunityToolkit.Mvvm.ComponentModel;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public int DefaultTabIndex => 0;

    public MainViewModel()
    {
        _selectedTabIndex = DefaultTabIndex;
    }
}
