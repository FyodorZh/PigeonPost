using System;
using System.Threading;
using System.Threading.Tasks;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Protocols.Monitoring.AckRaw;
using Pontifex.Transports.Direct;
using Pontifex.Transports.Tcp;
using Actuarius.Memory;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Vpn;

public sealed class VpnClientRuntime : IVpnRuntime, IDisposable
{
    private static readonly TransportFactory _transportFactory = CreateTransportFactory();

    private readonly Lock _lock = new();
    private VpnProfile _profile = null!;
    private readonly bool _verbose;

    private ConnectionState _state = ConnectionState.Disconnected;
    private DateTime? _sessionStart;
    private double _speedSentBps;
    private double _speedReceivedBps;
    private int _reconnectCount;
    private bool _isReconnecting;
    private bool _shutdownRequested;
    private bool _disposed;
    private bool _connecting;

    private ProbeTunDevice? _probeTun;
    private ProbeScheduler? _probeScheduler;
    private CountingTunDevice? _countingTun;
    private PacketBuffer? _buffer;
    private BridgeImpl? _bridge;
    private RuntimeLogger? _runtimeLogger;
    private ITransport? _transport;
    private TaskCompletionSource? _transportStoppedTcs;
    private CancellationTokenSource? _connectCts;
    private Timer? _statsTimer;
    private long _prevBytesSent;
    private long _prevBytesReceived;
    private DateTime _prevStatsTime;

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

    public VpnClientRuntime(bool verbose = false)
    {
        _verbose = verbose;
    }

    private static TransportFactory CreateTransportFactory()
    {
        var factory = new TransportFactory();
        factory.Register(new AckRawDirectClientProducer());
        factory.Register(new AckRawTcpClientProducer());
        factory.Register(new AckRawLoggerClientProducer());
        return factory;
    }

    public async Task ConnectAsync(VpnProfile profile, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(profile);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_state != ConnectionState.Disconnected || _connecting)
                return;

            _profile = profile;
            _state = ConnectionState.Connecting;
            _connecting = true;
            _sessionStart = null;
            _speedSentBps = 0;
            _speedReceivedBps = 0;
            _reconnectCount = 0;
            _isReconnecting = false;
            _shutdownRequested = false;
        }

        _connectCts?.Cancel();
        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        EmitLog("Connecting...", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        try
        {
            _runtimeLogger = new RuntimeLogger(entry =>
            {
                LogEmitted?.Invoke(entry);
            });

            _probeTun = new ProbeTunDevice();
            _countingTun = new CountingTunDevice(_probeTun);
            _buffer = new PacketBuffer(10 * 1024 * 1024);
            _bridge = new BridgeImpl(_countingTun, _buffer, _runtimeLogger, _verbose);

            var connectTcs = new TaskCompletionSource();
            SubscribeConnectHandlers(connectTcs);

            _bridge.Start();

            await StartTransportAsync(profile.ServerUrl, connectTcs);

            using var reg = _connectCts.Token.Register(() => connectTcs.TrySetCanceled());

            await connectTcs.Task;

            var clientIp = IPv4.Parse(_profile.FullClientIp);
            _probeScheduler = new ProbeScheduler(_probeTun!, _runtimeLogger!, clientIp.Value);
            _probeScheduler.Start();
            EmitLog("Probe scheduler started", VpnLogLevel.Info);

            lock (_lock)
            {
                if (_state == ConnectionState.Connected && !_shutdownRequested)
                {
                    _ = ReconnectLoopAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            CleanupAll();
            lock (_lock)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
            }
            EmitLog("Connection cancelled", VpnLogLevel.Warning);
            SessionUpdated?.Invoke(CreateSnapshot());
            throw;
        }
        catch (HandshakeRejectedException)
        {
            CleanupAll();
            lock (_lock)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
            }
            SessionUpdated?.Invoke(CreateSnapshot());
            throw;
        }
        catch (Exception ex)
        {
            CleanupAll();
            lock (_lock)
            {
                _state = ConnectionState.Disconnected;
                _connecting = false;
            }
            EmitLog($"Connection failed: {ex.Message}", VpnLogLevel.Error);
            SessionUpdated?.Invoke(CreateSnapshot());
            throw;
        }
    }

    private void SubscribeConnectHandlers(TaskCompletionSource connectTcs)
    {
        Action<Pontifex.Abstractions.Endpoints.IAckRawBaseEndpoint>? onConnected = null;
        Action<StopReason>? onStopped = null;

        onConnected = ep =>
        {
            _bridge!.EndpointConnected -= onConnected;
            _bridge!.OnStopped -= onStopped;

            lock (_lock)
            {
                _state = ConnectionState.Connected;
                _sessionStart = DateTime.UtcNow;
                _connecting = false;
            }
            EmitLog("Connected", VpnLogLevel.Info);
            SessionUpdated?.Invoke(CreateSnapshot());
            StartStatsTimer();
            connectTcs.TrySetResult();
        };

        onStopped = reason =>
        {
            _bridge!.EndpointConnected -= onConnected;
            _bridge!.OnStopped -= onStopped;

            if (reason is Pontifex.StopReasons.ExceptionFail exFail &&
                exFail.Text.Contains("Handshake rejected"))
            {
                var code = exFail.Exception is HandshakeRejectedException hre
                    ? hre.RejectCode
                    : HandshakeRejectCode.DuplicateHostIp;
                connectTcs.TrySetException(new HandshakeRejectedException(code, exFail.Text));
            }
            else if (!connectTcs.Task.IsCompleted)
            {
                connectTcs.TrySetException(new InvalidOperationException(
                    $"Transport stopped before handshake: {reason.Type}"));
            }
        };

        _bridge!.EndpointConnected += onConnected;
        _bridge!.OnStopped += onStopped;
    }

    private async Task StartTransportAsync(string url, TaskCompletionSource connectTcs)
    {
        var clientIp = IPv4.Parse(_profile.FullClientIp);
        var handshake = new ClientHandshake(clientIp);
        var handler = new BridgeClientHandler(_bridge!, handshake);

        var transport = _transportFactory.Construct(url, _runtimeLogger!, MemoryRental.Shared);
        if (transport is not IAckRawClient ackClient)
        {
            connectTcs.TrySetException(new InvalidOperationException("Transport is not IAckRawClient"));
            return;
        }

        ackClient.Init(handler);
        _transport = transport;

        _transportStoppedTcs = new TaskCompletionSource();
        ackClient.Start(reason =>
        {
            _runtimeLogger?.i($"Transport stopped: {reason.Type}");
            _transportStoppedTcs?.TrySetResult();
        });
    }

    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            _shutdownRequested = true;
        }

        _connectCts?.Cancel();
        _probeScheduler?.Dispose();
        _probeScheduler = null;
        StopStatsTimer();
        CleanupAll();

        lock (_lock)
        {
            _state = ConnectionState.Disconnected;
            _connecting = false;
            _isReconnecting = false;
            _sessionStart = null;
            _speedSentBps = 0;
            _speedReceivedBps = 0;
        }

        EmitLog("Disconnected", VpnLogLevel.Info);
        SessionUpdated?.Invoke(CreateSnapshot());

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdownRequested = true;
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _probeScheduler?.Dispose();
        _probeScheduler = null;
        StopStatsTimer();
        CleanupAll();
        _bridge?.Dispose();
        _runtimeLogger?.Dispose();
        _probeTun?.Dispose();
        _probeTun = null;
    }

    private async Task ReconnectLoopAsync()
    {
        while (!_shutdownRequested)
        {
            await _transportStoppedTcs!.Task;

            if (_shutdownRequested)
                break;

            StopStatsTimer();

            lock (_lock)
            {
                _isReconnecting = true;
                _state = ConnectionState.Disconnected;
                _sessionStart = null;
                _speedSentBps = 0;
                _speedReceivedBps = 0;
            }

            EmitLog("Connection lost. Reconnecting in 1 second...", VpnLogLevel.Warning);
            SessionUpdated?.Invoke(CreateSnapshot());

            try
            {
                await Task.Delay(1000, _connectCts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_shutdownRequested)
                break;

            lock (_lock)
            {
                _reconnectCount++;
                _state = ConnectionState.Connecting;
                _isReconnecting = false;
            }

            EmitLog("Reconnecting...", VpnLogLevel.Info);
            SessionUpdated?.Invoke(CreateSnapshot());

            var connectTcs = new TaskCompletionSource();

            Action<Pontifex.Abstractions.Endpoints.IAckRawBaseEndpoint>? onReconnected = null;
            Action<StopReason>? onReconnectStopped = null;

            onReconnected = ep =>
            {
                _bridge!.EndpointConnected -= onReconnected;
                _bridge!.OnStopped -= onReconnectStopped;

                lock (_lock)
                {
                    _state = ConnectionState.Connected;
                    _sessionStart = DateTime.UtcNow;
                }
                EmitLog("Reconnected", VpnLogLevel.Info);
                SessionUpdated?.Invoke(CreateSnapshot());
                StartStatsTimer();
                connectTcs.TrySetResult();
            };

            onReconnectStopped = reason =>
            {
                _bridge!.EndpointConnected -= onReconnected;
                _bridge!.OnStopped -= onReconnectStopped;

                if (!connectTcs.Task.IsCompleted)
                {
                    connectTcs.TrySetResult();
                }
            };

            _bridge!.EndpointConnected += onReconnected;
            _bridge!.OnStopped += onReconnectStopped;

            await StartTransportAsync(_profile.ServerUrl, connectTcs);

            await connectTcs.Task;
        }
    }

    private void CleanupAll()
    {
        _bridge?.Stop(StopReason.UserIntention);
        _transport?.Stop(StopReason.UserIntention);
        _transport = null;
        _transportStoppedTcs = null;
    }

    private void EmitLog(string message, VpnLogLevel level)
    {
        var entry = new VpnLogEntry(DateTime.UtcNow, message, level);
        LogEmitted?.Invoke(entry);
    }

    private VpnSessionSnapshot CreateSnapshot()
    {
        long sent = 0, received = 0;
        if (_countingTun != null)
        {
            sent = _countingTun.BytesSent;
            received = _countingTun.BytesReceived;
        }

        return new VpnSessionSnapshot(
            _state,
            _sessionStart,
            sent,
            received,
            _speedSentBps,
            _speedReceivedBps,
            _reconnectCount);
    }

    internal void TestConnectTcsForReconnect()
    {
        _transportStoppedTcs?.TrySetResult();
    }

    private void StartStatsTimer()
    {
        StopStatsTimer();
        _prevBytesSent = _countingTun?.BytesSent ?? 0;
        _prevBytesReceived = _countingTun?.BytesReceived ?? 0;
        _prevStatsTime = DateTime.UtcNow;
        _speedSentBps = 0;
        _speedReceivedBps = 0;
        _statsTimer = new Timer(StatsTimerCallback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void StopStatsTimer()
    {
        _statsTimer?.Dispose();
        _statsTimer = null;
    }

    private void StatsTimerCallback(object? state)
    {
        VpnSessionSnapshot snapshot;

        lock (_lock)
        {
            if (_state != ConnectionState.Connected || _countingTun == null)
                return;

            var now = DateTime.UtcNow;
            var currentSent = _countingTun.BytesSent;
            var currentReceived = _countingTun.BytesReceived;

            var elapsed = (now - _prevStatsTime).TotalSeconds;
            if (elapsed >= 0.5)
            {
                _speedSentBps = (currentSent - _prevBytesSent) * 8.0 / elapsed;
                _speedReceivedBps = (currentReceived - _prevBytesReceived) * 8.0 / elapsed;

                _prevBytesSent = currentSent;
                _prevBytesReceived = currentReceived;
                _prevStatsTime = now;
            }

            snapshot = CreateSnapshot();
        }

        SessionUpdated?.Invoke(snapshot);
    }
}
