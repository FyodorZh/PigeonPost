using System;
using System.Threading;
using System.Threading.Tasks;

namespace PigeonPost.Vpn;

public sealed class FakeVpnRuntime : IVpnRuntime, IDisposable
{
    private readonly Lock _lock = new();
    private readonly Random _random = new();
    private Timer? _timer;
    private CancellationTokenSource? _shutdownCts;
    private VpnProfile? _currentProfile;
    private int _ticksSinceConnected;
    private bool _disposed;
    private bool _shutdownRequested;
    private bool _isReconnecting;
    private bool _connecting;

    private ConnectionState _state = ConnectionState.Disconnected;
    private DateTime? _sessionStart;
    private long _bytesSent;
    private long _bytesReceived;
    private double _speedSentBps;
    private double _speedReceivedBps;
    private int _reconnectCount;

    public ConnectionState State
    {
        get { lock (_lock) return _state; }
    }

    public bool IsReconnecting
    {
        get { lock (_lock) return _isReconnecting; }
    }

    public VpnSessionSnapshot CurrentSession
    {
        get { lock (_lock) return CreateSnapshot(); }
    }

    public event Action<VpnSessionSnapshot>? SessionUpdated;
    public event Action<VpnLogEntry>? LogEmitted;

    public async Task ConnectAsync(VpnProfile profile, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_state != ConnectionState.Disconnected || _connecting)
                return;

            _currentProfile = profile;
            _state = ConnectionState.Connecting;
            _connecting = true;
            _bytesSent = 0;
            _bytesReceived = 0;
            _speedSentBps = 0;
            _speedReceivedBps = 0;
            _sessionStart = null;
            _reconnectCount = 0;
            _ticksSinceConnected = 0;
            _isReconnecting = false;
            _shutdownRequested = false;
        }

        _shutdownCts?.Cancel();
        _shutdownCts = new CancellationTokenSource();

        EmitLog("Connecting...", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        try
        {
            await Task.Delay(_random.Next(1000, 2001), ct);
            ct.ThrowIfCancellationRequested();

            lock (_lock)
            {
                _state = ConnectionState.Connected;
                _sessionStart = DateTime.UtcNow;
                _ticksSinceConnected = 0;
                _connecting = false;
            }

            EmitLog("Connected", VpnLogLevel.Info);
            SessionUpdated?.Invoke(CreateSnapshot());

            StartTrafficTimer();
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
            }
            EmitLog("Connection cancelled", VpnLogLevel.Warning);
            SessionUpdated?.Invoke(CreateSnapshot());
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            _shutdownRequested = true;
            _connecting = false;
            _state = ConnectionState.Disconnected;
            _sessionStart = null;
        }

        _shutdownCts?.Cancel();
        StopTimer();

        EmitLog("Disconnected", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopTimer();
        _shutdownCts?.Cancel();
        _shutdownCts?.Dispose();
    }

    private void StartTrafficTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    private void StopTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnTimerTick(object? state)
    {
        bool disconnected = false;

        lock (_lock)
        {
            if (_state != ConnectionState.Connected)
                return;

            var sentDelta = _random.Next(10000, 50001);
            var recvDelta = _random.Next(20000, 200001);

            _bytesSent += sentDelta;
            _bytesReceived += recvDelta;
            _speedSentBps = sentDelta;
            _speedReceivedBps = recvDelta;
            _ticksSinceConnected++;

            if (_ticksSinceConnected >= 12 && _random.Next(5) == 0)
            {
                _state = ConnectionState.Disconnected;
                disconnected = true;
            }
        }

        if (disconnected)
        {
            StopTimer();
            EmitLog("Connection lost - unexpected disconnect", VpnLogLevel.Warning);
            SessionUpdated?.Invoke(CreateSnapshot());
            _ = StartReconnectAsync();
        }
        else
        {
            SessionUpdated?.Invoke(CreateSnapshot());
        }
    }

    private async Task StartReconnectAsync()
    {
        lock (_lock)
        {
            if (_shutdownRequested)
                return;
            _isReconnecting = true;
        }

        EmitLog("Auto-reconnecting in 1 second...", VpnLogLevel.Info);

        try
        {
            await Task.Delay(1000, _shutdownCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            lock (_lock) _isReconnecting = false;
            return;
        }

        lock (_lock)
        {
            if (_shutdownRequested)
            {
                _isReconnecting = false;
                return;
            }
            _reconnectCount++;
            _isReconnecting = false;
            _state = ConnectionState.Connecting;
            _sessionStart = null;
            _bytesSent = 0;
            _bytesReceived = 0;
            _speedSentBps = 0;
            _speedReceivedBps = 0;
            _ticksSinceConnected = 0;
            _connecting = true;
        }

        EmitLog("Reconnecting...", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        try
        {
            await Task.Delay(_random.Next(500, 1501), _shutdownCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
            }
            return;
        }

        lock (_lock)
        {
            if (_shutdownRequested)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
                return;
            }
            _state = ConnectionState.Connected;
            _sessionStart = DateTime.UtcNow;
            _ticksSinceConnected = 0;
            _connecting = false;
        }

        EmitLog("Reconnected", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        StartTrafficTimer();
    }

    internal void TestForceUnexpectedDisconnect()
    {
        bool wasConnected;
        lock (_lock)
        {
            wasConnected = _state == ConnectionState.Connected;
            if (wasConnected)
            {
                _state = ConnectionState.Disconnected;
            }
        }

        if (wasConnected)
        {
            StopTimer();
            EmitLog("Connection lost - unexpected disconnect", VpnLogLevel.Warning);
            SessionUpdated?.Invoke(CreateSnapshot());
            _ = StartReconnectAsync();
        }
    }

    private void EmitLog(string message, VpnLogLevel level)
    {
        var entry = new VpnLogEntry(DateTime.UtcNow, message, level);
        LogEmitted?.Invoke(entry);
    }

    private VpnSessionSnapshot CreateSnapshot()
    {
        return new VpnSessionSnapshot(
            _state,
            _sessionStart,
            _bytesSent,
            _bytesReceived,
            _speedSentBps,
            _speedReceivedBps,
            _reconnectCount);
    }
}
