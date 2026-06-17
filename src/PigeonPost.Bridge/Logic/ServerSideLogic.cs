using System;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Servers;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Bridge;

public sealed class ServerSideLogic
{
    private readonly ITunDevice _tun;
    private readonly string _serverUrl;
    private readonly ILogger _logger;
    private readonly ITransportFactory _transportFactory;
    private readonly int _bufferSizeBytes;
    private readonly bool _verbose;

    private ServerHub? _hub;
    private BridgeImpl? _bridge;
    private ITransport? _transport;
    private volatile bool _stopped;

    public event Action? Stopped;

    public ServerSideLogic(
        ITunDevice tun,
        string serverUrl,
        ILogger logger,
        ITransportFactory transportFactory,
        int bufferSizeBytes,
        bool verbose)
    {
        _tun = tun;
        _serverUrl = serverUrl;
        _logger = logger;
        _transportFactory = transportFactory;
        _bufferSizeBytes = bufferSizeBytes;
        _verbose = verbose;
    }

    public void Start()
    {
        _hub = new ServerHub(_logger, _tun);
        var buffer = new PacketBuffer(_bufferSizeBytes);
        _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);
        _bridge.SetPacketHandler(_hub.OnPacketFromTun);
        _bridge.OnStopped += OnBridgeStopped;

        var transport = _transportFactory.Construct(_serverUrl, _logger, MemoryRental.Shared);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Server transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(_hub));
        _transport = transport;

        _bridge.Start();
        ackServer.Start(reason =>
        {
            _logger.i($"Server transport stopped: {reason.Type}");
            OnStopped();
        });
    }

    private void OnBridgeStopped(StopReason reason)
    {
        if (_stopped)
            return;

        _logger.e($"Server bridge stopped unexpectedly ({reason.Type}). Fatal.");
        _stopped = true;
        Stopped?.Invoke();
    }

    private void OnStopped()
    {
        if (_stopped)
            return;

        _stopped = true;
        Stopped?.Invoke();
    }

    public void Stop()
    {
        if (_stopped)
            return;
        _stopped = true;

        _hub?.StopAccepting();
        _hub?.StopAll(StopReason.UserIntention);
        _bridge?.Stop(StopReason.UserIntention);
        _transport?.Stop(StopReason.UserIntention);

        Stopped?.Invoke();
    }
}
