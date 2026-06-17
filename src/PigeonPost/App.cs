using System;
using System.Collections.Generic;
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
using PigeonPost.Tun;
using PigeonPost.Tun.Virtual;
using Scriba;

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

        using var tun = new TunDevice(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var serverHub = new ServerHub(_logger, tun);
        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeImpl(tun, buffer, _logger, _config.Verbose);
        bridge.SetPacketHandler(packet => serverHub.OnPacketFromTun(packet));

        var transport = CreateTransport(_config.PontifexUrl, isServer: true);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(serverHub));

        bridge.Start();

        var stopped = new TaskCompletionSource<StopReason>();
        bridge.OnStopped += reason => stopped.TrySetResult(reason);

        ackServer.Start(reason =>
        {
            _logger.i($"Server stopped: {reason.Type}");
            stopped.TrySetResult(reason);
        });

        _logger.i("Server running. Accepting clients...");

        var result = await Task.WhenAny(
            stopped.Task,
            WaitForShutdownAsync()
        );

        if (result == stopped.Task)
        {
            _logger.w("Transport stopped unexpectedly. Exiting.");
        }

        serverHub.StopAccepting();
        serverHub.StopAll(Pontifex.StopReason.UserIntention);
        bridge.Stop(Pontifex.StopReason.UserIntention);
        ackServer.Stop(Pontifex.StopReason.UserIntention);
        tun.Close();
        _logger.i("Server shut down.");
    }

    private async Task RunClientAsync()
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
        int clientCount = _config.DebugClientCount;
        string serverUrl = _config.DebugServerUrl;
        string clientUrl = _config.DebugClientUrl;

        var network = new VirtualNetwork();
        var debugTopology = new DebugNetworkTopology(
            network,
            clientCount,
            messagesPerClient: 100,
            periodBetweenMessages: TimeSpan.FromMilliseconds(10));

        var serverTun = debugTopology.ServerDevice;
        var clientTuns = debugTopology.ClientDevices;

        var serverHub = new ServerHub(_logger, serverTun);
        var serverBuffer = new PacketBuffer(_config.BufferSizeBytes);
        using var serverBridge = new BridgeImpl(serverTun, serverBuffer, _logger, _config.Verbose);
        serverBridge.SetPacketHandler(packet => serverHub.OnPacketFromTun(packet));

        var serverTransport = CreateTransport(serverUrl, isServer: true);
        if (serverTransport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Server transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(serverHub));

        var clientBridges = new List<BridgeImpl>();
        var clientTransports = new List<ITransport>();

        for (int i = 0; i < clientCount; i++)
        {
            string debugClientId = $"debug-client-{i + 1}";

            uint clientIpv4 = (uint)(new IPv4(192, 168, 0, 2).Value + (uint)i);

            _logger.i($"Debug client '{debugClientId}': virtual IP={FormatIp(clientIpv4)}");

            var clientBuffer = new PacketBuffer(_config.BufferSizeBytes);
            var clientBridge = new BridgeImpl(clientTuns[i], clientBuffer, _logger, _config.Verbose);
            clientBridges.Add(clientBridge);

            var handshake = new ClientHandshake(new ClientId(debugClientId), clientIpv4);
            var clientHandler = new BridgeClientHandler(clientBridge, handshake);

            var clientTransport = CreateTransport(clientUrl, isServer: false);
            if (clientTransport is not IAckRawClient ackClient)
                throw new InvalidOperationException("Client transport is not an IAckRawClient.");

            ackClient.Init(clientHandler);
            clientTransports.Add(clientTransport);
        }

        ackServer.Start(reason => _logger.i($"Debug server stopped: {reason.Type}"));
        serverBridge.Start();

        for (int i = 0; i < clientCount; i++)
        {
            if (clientTransports[i] is IAckRawClient ackClient)
            {
                int idx = i;
                ackClient.Start(reason => _logger.i($"Debug client {idx + 1} stopped: {reason.Type}"));
            }
            clientBridges[i].Start();
        }

        _logger.i($"Debug mode running: {clientCount} virtual client(s), server={serverUrl}");

        await Task.WhenAny(
            debugTopology.WaitForCompletionAsync(),
            WaitForShutdownAsync());

        _logger.i("Shutting down debug mode...");

        debugTopology.Stop();

        foreach (var t in clientTransports)
            if (t is IAckRawClient c) c.Stop(Pontifex.StopReason.UserIntention);

        foreach (var b in clientBridges)
            b.Stop(Pontifex.StopReason.UserIntention);

        serverHub.StopAccepting();
        serverHub.StopAll(Pontifex.StopReason.UserIntention);
        serverBridge.Stop(Pontifex.StopReason.UserIntention);
        ackServer.Stop(Pontifex.StopReason.UserIntention);

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

    private static string FormatIp(uint ip)
    {
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }
}
