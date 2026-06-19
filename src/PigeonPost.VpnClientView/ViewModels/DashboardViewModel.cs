using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PigeonPost.Bridge;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IVpnRuntime _runtime;
    private readonly IProfileStore? _store;
    private readonly VpnProfile? _profile;
    private readonly SpeedHistoryBuffer _speedHistory = new();

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

    [ObservableProperty]
    private IReadOnlyList<double> _sentHistory = Array.Empty<double>();

    [ObservableProperty]
    private IReadOnlyList<double> _receivedHistory = Array.Empty<double>();

    public string StatusBadgeColor => ConnectionState switch
    {
        ConnectionState.Connected => "#4CAF50",
        ConnectionState.Connecting => "#FF9800",
        _ => "#9E9E9E"
    };

    public DashboardViewModel(IVpnRuntime runtime, VpnProfile? profile = null, IProfileStore? store = null)
    {
        _runtime = runtime;
        _profile = profile;
        _store = store;

        _runtime.SessionUpdated += OnSessionUpdated;
        UpdateFromSnapshot(runtime.CurrentSession);
    }

    partial void OnConnectionStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(StatusBadgeColor));
    }

    private void OnSessionUpdated(VpnSessionSnapshot snapshot)
    {
        if (Application.Current?.ApplicationLifetime is not null)
            Dispatcher.UIThread.Post(() => UpdateFromSnapshot(snapshot));
        else
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

        _speedHistory.AddSample(snapshot.SpeedSentBps, snapshot.SpeedReceivedBps);
        SentHistory = _speedHistory.SentHistory;
        ReceivedHistory = _speedHistory.ReceivedHistory;

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
        catch (HandshakeRejectedException ex)
        {
            StatusText = ex.RejectCode switch
            {
                HandshakeRejectCode.DuplicateHostIp => "This client IP is already in use on the server",
                HandshakeRejectCode.InvalidHandshake => "Server rejected our handshake (invalid format)",
                HandshakeRejectCode.ServerShuttingDown => "Server is shutting down",
                HandshakeRejectCode.UnsupportedPacketFamily => "Server does not support our packet type",
                _ => $"Handshake rejected: {ex.RejectCode}"
            };
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
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
