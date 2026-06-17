using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Pontifex.StopReasons;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using PigeonPost.Tun.Virtual;
using Scriba;

namespace PigeonPost;

internal sealed class DebugApp : BaseApp
{
    public DebugApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Debug)
            throw new ArgumentException("DebugApp can only be used in debug mode.", nameof(config));
    }

    public override async Task RunAsync()
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
}
