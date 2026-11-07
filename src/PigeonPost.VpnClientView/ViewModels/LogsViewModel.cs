using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly IVpnRuntime _runtime;

    public ObservableCollection<VpnLogEntry> Logs { get; } = new();

    public LogsViewModel(IVpnRuntime runtime)
    {
        _runtime = runtime;
        _runtime.LogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(VpnLogEntry entry)
    {
        if (Application.Current?.ApplicationLifetime is not null)
            Dispatcher.UIThread.Post(() => Logs.Add(entry));
        else
            Logs.Add(entry);
    }
}
