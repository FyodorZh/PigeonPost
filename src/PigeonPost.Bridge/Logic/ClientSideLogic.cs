using System;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Bridge;

public class ClientSideLogic
{
    protected readonly IPv4 _clientIp;
    protected readonly IPv4 _serverIp;
    protected readonly ILogger _logger;

    public event Action? Stopped;

    private readonly ITunDevice _tun;
    private readonly string _clientUrl;
    private readonly ITransportFactory _transportFactory;
    private readonly int _bufferSizeBytes;
    private readonly bool _verbose;

    private readonly ClientHandshake _handshake;
    private readonly BridgeImpl _bridge;

    private ITransport? _transport;
    private TaskCompletionSource? _transportStoppedTcs;
    private volatile bool _stopped;

    public ClientSideLogic(
        ITunDevice tun,
        IPv4 clientIp,
        IPv4 serverIp,
        string clientUrl,
        ILogger logger,
        ITransportFactory transportFactory,
        int bufferSizeBytes,
        bool verbose)
    {
        _tun = tun;
        _clientIp = clientIp;
        _serverIp = serverIp;
        _clientUrl = clientUrl;
        _logger = logger;
        _transportFactory = transportFactory;
        _bufferSizeBytes = bufferSizeBytes;
        _verbose = verbose;

        _handshake = new ClientHandshake(_clientIp);

        var buffer = new PacketBuffer(_bufferSizeBytes);
        _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);
        _bridge.OnStopped += OnBridgeStopped;
    }

    public virtual void RequestShutdown()
    {
        Stop();
    }

    public virtual Task Start()
    {
        _bridge.Start();
        _ = ReconnectLoopAsync();
        return Task.CompletedTask;
    }

    private void OnBridgeStopped(StopReason reason)
    {
        if (_stopped)
            return;

        _logger.e($"Client {_clientIp}: bridge stopped unexpectedly ({reason.Type}). Fatal.");
        _stopped = true;
        _transportStoppedTcs?.TrySetResult();
        Stopped?.Invoke();
    }

    private async Task ReconnectLoopAsync()
    {
        while (!_stopped)
        {
            _logger.i($"Client {_clientIp} connecting...");

            var handler = new BridgeClientHandler(_bridge!, _handshake!);

            try
            {
                var transport = _transportFactory.Construct(_clientUrl, _logger, MemoryRental.Shared);
                if (transport is not IAckRawClient ackClient)
                {
                    _logger.e($"Client {_clientIp}: constructed transport is not IAckRawClient.");
                    break;
                }

                ackClient.Init(handler);
                _transport = transport;

                _transportStoppedTcs = new TaskCompletionSource();
                ackClient.Start(reason =>
                {
                    _logger.i($"Client {_clientIp} transport stopped: {reason.Type}");
                    _transportStoppedTcs?.TrySetResult();
                });

                await _transportStoppedTcs.Task;
            }
            catch (Exception ex)
            {
                _logger.e($"Client {_clientIp}: transport error: {ex.Message}");
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

        Stopped?.Invoke();
    }
}
