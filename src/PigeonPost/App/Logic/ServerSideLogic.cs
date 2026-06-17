using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Servers;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

public sealed class ServerSideLogic
{
    private readonly ITunDevice _tun;
    private readonly string _serverUrl;
    private readonly ILogger _logger;
    private readonly Pontifex.ITransportFactory _transportFactory;
    private readonly int _bufferSizeBytes;
    private readonly bool _verbose;

    private ServerHub? _hub;
    private BridgeImpl? _bridge;
    private ITransport? _transport;
    private int _activeClients;
    private readonly TaskCompletionSource _completionTcs = new();

    public Task Completion => _completionTcs.Task;

    public ServerSideLogic(
        ITunDevice tun,
        string serverUrl,
        ILogger logger,
        Pontifex.ITransportFactory transportFactory,
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

    public void AddClient(string clientId)
    {
        Interlocked.Increment(ref _activeClients);
    }

    public void RemoveClient(string clientId)
    {
        if (Interlocked.Decrement(ref _activeClients) == 0)
        {
            Stop();
            _completionTcs.TrySetResult();
        }
    }

    public void Start()
    {
        _hub = new ServerHub(_logger, _tun);
        var buffer = new PacketBuffer(_bufferSizeBytes);
        _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);
        _bridge.SetPacketHandler(_hub.OnPacketFromTun);

        var transport = _transportFactory.Construct(_serverUrl, _logger, MemoryRental.Shared);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Server transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(_hub));
        _transport = transport;

        _bridge.Start();
        ackServer.Start(reason => _logger.i($"Server transport stopped: {reason.Type}"));
    }

    public void Stop()
    {
        _hub?.StopAccepting();
        _hub?.StopAll(Pontifex.StopReason.UserIntention);
        _bridge?.Stop(Pontifex.StopReason.UserIntention);
        _transport?.Stop(Pontifex.StopReason.UserIntention);
    }
}
