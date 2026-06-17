using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using PigeonPost.Tun.Virtual;
using Scriba;

namespace PigeonPost;

internal sealed class DebugApp : BaseApp
{
    private static readonly IPv4 ServerIp = new(192, 168, 0, 1);
    private static readonly IPv4 ClientBaseIp = new(192, 168, 0, 2);

    public DebugApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Debug)
            throw new ArgumentException("Role must be Debug.", nameof(config));
    }

    public override async Task RunAsync()
    {
        int clientCount = _config.DebugClientCount;
        string serverUrl = _config.DebugServerUrl;
        string clientUrl = _config.DebugClientUrl;

        using var harness = new VirtualTrafficHarness();

        var serverSide = new ServerSideLogic(
            harness.ServerDevice, serverUrl, _logger, _serverTransportFactory,
            _config.BufferSizeBytes, _config.Verbose);

        var clientSides = new List<ClientSideLogic>();

        for (int i = 0; i < clientCount; i++)
        {
            var clientId = new ClientId($"debug-client-{i + 1}");
            uint ipVal = (uint)(ClientBaseIp.Value + (uint)i);
            var clientIp = new IPv4(ipVal);
            string name = $"client{i}";

            var pending = new Queue<byte[]>();

            var clientTun = harness.Network.CreateNode(name, clientIp, (fromIp, toIp, packet) =>
            {
                byte[] expected;
                lock (pending)
                {
                    expected = pending.Dequeue();
                }

                for (int j = 0; j < expected.Length; j++)
                {
                    if (packet[j] != expected[expected.Length - 1 - j])
                        throw new Exception($"Client {clientId}: response mismatch at byte {j}");
                }
            });

            _logger.i($"Debug client '{clientId}': virtual IP={FormatIp(ipVal)}");

            var clientSide = new ClientSideDebugLogic(
                clientTun, clientId, clientIp, ServerIp,
                clientUrl, _logger, _cts.Token, _clientTransportFactory,
                _config.BufferSizeBytes, _config.Verbose,
                pending, harness.Network);

            clientSide.Stopped += id => serverSide.RemoveClient(id.Value);
            clientSides.Add(clientSide);
            serverSide.AddClient(clientId.Value);
        }

        serverSide.Start();

        foreach (var c in clientSides)
            _ = c.Start().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.e($"Client error: {t.Exception?.GetBaseException().Message}");
            });

        _logger.i($"Debug mode running: {clientCount} virtual client(s), server={serverUrl}");

        await Task.WhenAny(
            serverSide.Completion,
            WaitForShutdownAsync());

        if (_shutdownRequested)
        {
            foreach (var c in clientSides)
                c.Stop();
            await serverSide.Completion;
        }

        _logger.i("Debug instance shut down.");
    }

    private sealed class VirtualTrafficHarness : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public VirtualNetwork Network { get; }
        public ITunDevice ServerDevice { get; }

        public VirtualTrafficHarness()
        {
            Network = new VirtualNetwork();
            ServerDevice = Network.CreateNode("server", ServerIp, (fromIp, toIp, packet) =>
            {
                byte[] response = new byte[packet.Length];
                for (int i = 0; i < packet.Length; i++)
                    response[i] = packet[packet.Length - 1 - i];
                Network.SendFromTo(ServerIp, fromIp, response);
            });
            _ = Network.RunAsync(_cts.Token);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }

}
