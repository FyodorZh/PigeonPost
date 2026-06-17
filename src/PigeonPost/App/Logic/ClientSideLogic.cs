using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

public class ClientSideLogic
{
    protected readonly ClientId _clientId;
    protected readonly IPv4 _clientIp;
    protected readonly IPv4 _serverIp;
    protected readonly ILogger _logger;
    protected readonly CancellationToken _externalCt;

    public event Action<ClientId>? Stopped;

    private readonly ITunDevice _tun;
    private readonly string _clientUrl;
    private readonly ITransportFactory _transportFactory;
    private readonly int _bufferSizeBytes;
    private readonly bool _verbose;

    private BridgeImpl? _bridge;
    private ITransport? _transport;
    private bool _stopped;

    public ClientSideLogic(
        ITunDevice tun,
        ClientId clientId,
        IPv4 clientIp,
        IPv4 serverIp,
        string clientUrl,
        ILogger logger,
        CancellationToken externalCt,
        ITransportFactory transportFactory,
        int bufferSizeBytes,
        bool verbose)
    {
        _tun = tun;
        _clientId = clientId;
        _clientIp = clientIp;
        _serverIp = serverIp;
        _clientUrl = clientUrl;
        _logger = logger;
        _externalCt = externalCt;
        _transportFactory = transportFactory;
        _bufferSizeBytes = bufferSizeBytes;
        _verbose = verbose;
    }

    public virtual Task Start()
    {
        var buffer = new PacketBuffer(_bufferSizeBytes);
        _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);

        var handshake = new ClientHandshake(_clientId, _clientIp.Value);
        var handler = new BridgeClientHandler(_bridge, handshake);

        var transport = _transportFactory.Construct(_clientUrl, _logger, MemoryRental.Shared);
        if (transport is not IAckRawClient ackClient)
            throw new InvalidOperationException("Client transport is not an IAckRawClient.");

        ackClient.Init(handler);
        _transport = transport;

        _bridge.Start();
        ackClient.Start(reason => _logger.i($"Client {_clientId} transport stopped: {reason.Type}"));
        
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (_stopped)
            return;
        _stopped = true;

        _bridge?.Stop(StopReason.UserIntention);
        _transport?.Stop(StopReason.UserIntention);

        Stopped?.Invoke(_clientId);
    }
}
