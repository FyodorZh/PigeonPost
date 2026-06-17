using System;
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

    public event Action<ClientId>? Stopped;

    private readonly ITunDevice _tun;
    private readonly string _clientUrl;
    private readonly ITransportFactory _transportFactory;
    private readonly int _bufferSizeBytes;
    private readonly bool _verbose;

    private BridgeImpl? _bridge;
    private ITransport? _transport;
    private volatile bool _stopped;

    public ClientSideLogic(
        ITunDevice tun,
        ClientId clientId,
        IPv4 clientIp,
        IPv4 serverIp,
        string clientUrl,
        ILogger logger,
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
        _transportFactory = transportFactory;
        _bufferSizeBytes = bufferSizeBytes;
        _verbose = verbose;
    }

    public virtual void RequestShutdown()
    {
        Stop();
    }

    public virtual Task Start()
    {
        var buffer = new PacketBuffer(_bufferSizeBytes);
        _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);
        _bridge.Start();

        _ = ReconnectLoopAsync();

        return Task.CompletedTask;
    }

    private async Task ReconnectLoopAsync()
    {
        while (!_stopped)
        {
            _logger.i($"Client {_clientId} connecting...");

            var handshake = new ClientHandshake(_clientId, _clientIp.Value);
            var handler = new BridgeClientHandler(_bridge!, handshake);

            try
            {
                var transport = _transportFactory.Construct(_clientUrl, _logger, MemoryRental.Shared);
                if (transport is not IAckRawClient ackClient)
                {
                    _logger.e($"Client {_clientId}: constructed transport is not IAckRawClient.");
                    break;
                }

                ackClient.Init(handler);
                _transport = transport;

                var stoppedTcs = new TaskCompletionSource();
                ackClient.Start(reason =>
                {
                    _logger.i($"Client {_clientId} transport stopped: {reason.Type}");
                    stoppedTcs.TrySetResult();
                });

                await stoppedTcs.Task;
            }
            catch (Exception ex)
            {
                _logger.e($"Client {_clientId}: transport error: {ex.Message}");
            }

            if (_stopped)
                break;

            _logger.i("Connection lost. Reconnecting in 1 second...");
            await Task.Delay(1000);
        }
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
