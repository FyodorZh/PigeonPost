using System;
using System.Threading;
using System.Threading.Tasks;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.StopReasons;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

internal sealed class ClientApp : BaseApp
{
    private readonly CancellationTokenSource _reconnectCts = new();

    public ClientApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Client)
            throw new ArgumentException("Role must be Client.", nameof(config));
    }

    public override void RequestShutdown()
    {
        _reconnectCts.Cancel();
        base.RequestShutdown();
    }

    public override async Task RunAsync()
    {
        var tunName = _config.TunNames[0];
        var clientId = _config.ClientId!;

        uint hostIpv4;
        try
        {
            hostIpv4 = TunIpv4AddressResolver.ResolveIpv4Address(tunName);
        }
        catch (Exception ex)
        {
            _logger.e($"Failed to resolve TUN IPv4 address: {ex.Message}");
            return;
        }

        _logger.i($"Client ID: {clientId}, host IPv4: {FormatIp(hostIpv4)}");

        using var tun = new TunDevice(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeImpl(tun, buffer, _logger, _config.Verbose);
        bridge.Start();

        var handshake = new ClientHandshake(new ClientId(clientId), hostIpv4);

        while (!_shutdownRequested)
        {
            _logger.i("Connecting to server...");

            var transport = CreateTransport(_config.PontifexUrl, isServer: false);
            if (transport is not IAckRawClient ackClient)
                throw new InvalidOperationException("Transport is not an IAckRawClient.");

            var handler = new BridgeClientHandler(bridge, handshake);
            ackClient.Init(handler);

            var stopped = new TaskCompletionSource<StopReason>();
            Action<StopReason> onBridgeStopped = reason => stopped.TrySetResult(reason);
            bridge.OnStopped += onBridgeStopped;

            ackClient.Start(reason =>
            {
                _logger.i($"Transport stopped: {reason.Type}");
                stopped.TrySetResult(reason);
            });

            var result = await Task.WhenAny(
                stopped.Task,
                WaitForShutdownAsync()
            );

            bridge.OnStopped -= onBridgeStopped;

            if (_shutdownRequested)
            {
                ackClient.Stop(Pontifex.StopReason.UserIntention);
                break;
            }

            _logger.i("Connection lost. Reconnecting in 1 second...");
            try
            {
                await Task.Delay(1000, _reconnectCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        bridge.Stop(Pontifex.StopReason.UserIntention);
        tun.Close();
        _logger.i("Client shut down.");
    }
}
