using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Pontifex.StopReasons;
using Pontifex.Transports.Direct;
using Pontifex.Transports.Tcp;
using PigeonPost.Bridge;
using PigeonPost.Bridge.Handlers;
using PigeonPost.Tun;
using Scriba;
using BridgeClass = PigeonPost.Bridge.Bridge;

namespace PigeonPost;

internal sealed class App
{
    private readonly BridgeConfiguration _config;
    private readonly ILogger _logger;

    private volatile bool _shutdownRequested;
    private readonly CancellationTokenSource _cts = new();

    public App(BridgeConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        switch (_config.Role)
        {
            case Role.Server:
                await RunServerAsync();
                break;
            case Role.Client:
                await RunClientAsync();
                break;
            case Role.Debug:
                await RunDebugAsync();
                break;
        }
    }

    public void RequestShutdown()
    {
        _shutdownRequested = true;
        _cts.Cancel();
    }

    private async Task RunServerAsync()
    {
        var tunName = _config.TunNames[0];

        using var tun = new TunDevice();
        tun.Open(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeClass(tun, buffer, _logger, _config.Verbose);

        var transport = CreateTransport(_config.PontifexUrl, isServer: true);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(bridge));

        bridge.Start();

        var stopped = new TaskCompletionSource<StopReason>();
        bridge.OnStopped += reason => stopped.TrySetResult(reason);

        ackServer.Start(reason =>
        {
            _logger.i($"Server stopped: {reason.Type}");
            stopped.TrySetResult(reason);
        });

        _logger.i("Server running. Waiting for client...");

        var result = await Task.WhenAny(
            stopped.Task,
            WaitForShutdownAsync()
        );

        if (result == stopped.Task)
        {
            _logger.w("Transport stopped unexpectedly. Exiting.");
        }

        bridge.Stop(Pontifex.StopReason.UserIntention);
        ackServer.Stop(Pontifex.StopReason.UserIntention);
        tun.Close();
        _logger.i("Server shut down.");
    }

    private async Task RunClientAsync()
    {
        var tunName = _config.TunNames[0];

        using var tun = new TunDevice();
        tun.Open(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeClass(tun, buffer, _logger, _config.Verbose);
        bridge.Start();

        while (!_shutdownRequested)
        {
            _logger.i("Connecting to server...");

            var transport = CreateTransport(_config.PontifexUrl, isServer: false);
            if (transport is not IAckRawClient ackClient)
                throw new InvalidOperationException("Transport is not an IAckRawClient.");

            var handler = new BridgeClientHandler(bridge);
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
                await Task.Delay(1000, _cts.Token);
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

    private async Task RunDebugAsync()
    {
        var tunName1 = _config.TunNames[0];
        var tunName2 = _config.TunNames[1];

        using var tun1 = new TunDevice();
        using var tun2 = new TunDevice();
        tun1.Open(tunName1);
        tun1.SetSendBufferSize(1048576);
        tun2.Open(tunName2);
        tun2.SetSendBufferSize(1048576);
        _logger.i($"TUN devices '{tunName1}' and '{tunName2}' opened.");

        var buffer1 = new PacketBuffer(_config.BufferSizeBytes);
        var buffer2 = new PacketBuffer(_config.BufferSizeBytes);
        using var bridge1 = new BridgeClass(tun1, buffer1, _logger, _config.Verbose);
        using var bridge2 = new BridgeClass(tun2, buffer2, _logger, _config.Verbose);

        var serverNameActual = ExtractDirectServerName(_config.PontifexUrl);

        var server = new AckRawDirectServer(serverNameActual, _logger, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(bridge1));

        var client = new AckRawDirectClient(serverNameActual, _logger, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(bridge2));

        server.Start(reason => _logger.i($"Debug server stopped: {reason.Type}"));
        client.Start(reason => _logger.i($"Debug client stopped: {reason.Type}"));

        bridge1.Start();
        bridge2.Start();

        _logger.i($"Debug mode running: {tunName1} ←→ {tunName2}");

        await WaitForShutdownAsync();

        client.Stop(Pontifex.StopReason.UserIntention);
        bridge2.Stop(Pontifex.StopReason.UserIntention);
        bridge1.Stop(Pontifex.StopReason.UserIntention);
        tun2.Close();
        tun1.Close();
        _logger.i("Debug instance shut down.");
    }

    private ITransport CreateTransport(string url, bool isServer)
    {
        if (url.StartsWith("direct|"))
        {
            string name = url.Substring("direct|".Length);
            if (isServer)
                return new AckRawDirectServer(name, _logger, MemoryRental.Shared);
            else
                return new AckRawDirectClient(name, _logger, MemoryRental.Shared);
        }

        var factory = new TransportFactory();
        var reg = new TransportFactoryRegistrator(factory);

        if (isServer)
            reg.Register<AckRawTcpServerProducer>();
        else
            reg.Register<AckRawTcpClientProducer>();

        var transport = factory.Construct(url, _logger, MemoryRental.Shared);
        if (transport == null)
            throw new InvalidOperationException($"Failed to construct transport from URL: '{url}'");
        return transport;
    }

    private static string ExtractDirectServerName(string url)
    {
        const string prefix = "direct|";
        if (url.StartsWith(prefix))
            return url.Substring(prefix.Length);
        throw new ArgumentException($"Expected direct transport URL, got: '{url}'");
    }

    private async Task WaitForShutdownAsync()
    {
        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
