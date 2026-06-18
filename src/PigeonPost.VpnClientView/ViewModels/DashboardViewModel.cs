using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IVpnRuntime _runtime;
    private readonly IProfileStore? _store;
    private readonly VpnProfile? _profile;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    private string _bytesSentText = "0 B";

    [ObservableProperty]
    private string _bytesReceivedText = "0 B";

    [ObservableProperty]
    private string _speedUpText = "0 bps";

    [ObservableProperty]
    private string _speedDownText = "0 bps";

    [ObservableProperty]
    private string _uptimeText = "--";

    [ObservableProperty]
    private int _reconnectCount;

    [ObservableProperty]
    private bool _canConnect = true;

    [ObservableProperty]
    private bool _canDisconnect;

    public DashboardViewModel(IVpnRuntime runtime, VpnProfile? profile = null, IProfileStore? store = null)
    {
        _runtime = runtime;
        _profile = profile;
        _store = store;

        _runtime.SessionUpdated += OnSessionUpdated;
        UpdateFromSnapshot(runtime.CurrentSession);
    }

    private void OnSessionUpdated(VpnSessionSnapshot snapshot)
    {
        UpdateFromSnapshot(snapshot);
    }

    private void UpdateFromSnapshot(VpnSessionSnapshot snapshot)
    {
        ConnectionState = snapshot.State;
        ReconnectCount = snapshot.ReconnectCount;
        CanConnect = snapshot.State is ConnectionState.Disconnected;
        CanDisconnect = snapshot.State is ConnectionState.Connected or ConnectionState.Connecting;

        switch (snapshot.State)
        {
            case ConnectionState.Disconnected:
                StatusText = _runtime.IsReconnecting ? "Reconnecting..." : "Disconnected";
                break;
            case ConnectionState.Connecting:
                StatusText = "Connecting...";
                break;
            case ConnectionState.Connected:
                StatusText = "Connected";
                break;
        }

        BytesSentText = FormatBytes(snapshot.BytesSent);
        BytesReceivedText = FormatBytes(snapshot.BytesReceived);
        SpeedUpText = FormatSpeed(snapshot.SpeedSentBps);
        SpeedDownText = FormatSpeed(snapshot.SpeedReceivedBps);

        if (snapshot.SessionStart is { } start)
        {
            var elapsed = DateTime.UtcNow - start;
            UptimeText = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s"
                : elapsed.TotalMinutes >= 1
                    ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
                    : $"{elapsed.Seconds}s";
        }
        else
        {
            UptimeText = "--";
        }
    }

    [RelayCommand]
    private async Task Connect()
    {
        var profile = _profile ?? _store?.Load();
        if (profile is null)
        {
            StatusText = "No profile configured";
            return;
        }

        try
        {
            await _runtime.ConnectAsync(profile, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await _runtime.DisconnectAsync();
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }

    private static string FormatSpeed(double bps)
    {
        return bps switch
        {
            < 1000 => $"{bps:F0} bps",
            < 1_000_000 => $"{bps / 1000.0:F1} Kbps",
            < 1_000_000_000 => $"{bps / 1_000_000.0:F1} Mbps",
            _ => $"{bps / 1_000_000_000.0:F2} Gbps"
        };
    }
}
