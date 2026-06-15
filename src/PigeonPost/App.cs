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
using PigeonPost.Bridge.Handlers;
using PigeonPost.Bridge.Protocol;
using PigeonPost.Bridge.Server;
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

        var serverHub = new ServerHub(_logger, tun);
        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeClass(tun, buffer, _logger, _config.Verbose);
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

        using var tun = new TunDevice();
        tun.Open(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeClass(tun, buffer, _logger, _config.Verbose);
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
        var tunNames = _config.TunNames;

        string serverTunName = tunNames[0];
        var clientTunNames = new List<string>();
        for (int i = 1; i < tunNames.Count; i++)
            clientTunNames.Add(tunNames[i]);

        using var serverTun = new TunDevice();
        serverTun.Open(serverTunName);
        serverTun.SetSendBufferSize(1048576);
        _logger.i($"Server TUN '{serverTunName}' opened.");

        var serverHub = new ServerHub(_logger, serverTun);
        var serverBuffer = new PacketBuffer(_config.BufferSizeBytes);
        using var serverBridge = new BridgeClass(serverTun, serverBuffer, _logger, _config.Verbose);
        serverBridge.SetPacketHandler(packet => serverHub.OnPacketFromTun(packet));

        var clientBridges = new List<BridgeClass>();
        var clientTuns = new List<TunDevice>();
        var clientHandlers = new List<BridgeClientHandler>();
        var clientTransports = new List<AckRawDirectClient>();

        for (int i = 0; i < clientCount; i++)
        {
            string clientTunName = clientTunNames[i];
            string debugClientId = $"debug-client-{i + 1}";

            var clientTun = new TunDevice();
            clientTun.Open(clientTunName);
            clientTun.SetSendBufferSize(1048576);
            clientTuns.Add(clientTun);

            uint clientIpv4;
            try
            {
                clientIpv4 = TunIpv4AddressResolver.ResolveIpv4Address(clientTunName);
            }
            catch (Exception ex)
            {
                _logger.e($"Failed to resolve TUN IPv4 for '{clientTunName}': {ex.Message}");
                return;
            }

            _logger.i($"Debug client '{debugClientId}': TUN={clientTunName}, host={FormatIp(clientIpv4)}");

            var clientBuffer = new PacketBuffer(_config.BufferSizeBytes);
            var clientBridge = new BridgeClass(clientTun, clientBuffer, _logger, _config.Verbose);
            clientBridges.Add(clientBridge);

            var handshake = new ClientHandshake(new ClientId(debugClientId), clientIpv4);
            clientHandlers.Add(new BridgeClientHandler(clientBridge, handshake));

            var clientTransport = new AckRawDirectClient(ExtractDirectServerName(_config.PontifexUrl),
                _logger, MemoryRental.Shared);
            clientTransports.Add(clientTransport);
        }

        var serverNameActual = ExtractDirectServerName(_config.PontifexUrl);
        var server = new AckRawDirectServer(serverNameActual, _logger, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(serverHub));

        server.Start(reason => _logger.i($"Debug server stopped: {reason.Type}"));
        serverBridge.Start();

        for (int i = 0; i < clientCount; i++)
        {
            var clientTransport = clientTransports[i];
            clientTransport.Init(clientHandlers[i]);
            int idx = i;
            clientTransport.Start(reason => _logger.i($"Debug client {idx + 1} stopped: {reason.Type}"));
            clientBridges[i].Start();
        }

        _logger.i($"Debug mode running: {serverTunName} ←→ {clientCount} client(s)");

        await WaitForShutdownAsync();

        _logger.i("Shutting down debug mode...");

        foreach (var clientTransport in clientTransports)
            clientTransport.Stop(Pontifex.StopReason.UserIntention);

        foreach (var clientBridge in clientBridges)
            clientBridge.Stop(Pontifex.StopReason.UserIntention);

        foreach (var clientTun in clientTuns)
            clientTun.Close();

        serverHub.StopAccepting();
        serverHub.StopAll(Pontifex.StopReason.UserIntention);
        serverBridge.Stop(Pontifex.StopReason.UserIntention);
        serverTun.Close();

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
