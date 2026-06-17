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

    private readonly VirtualTrafficHarness _harness;
    private readonly ServerSideLogic _serverLogic;
    private readonly List<ClientSideLogic> _clientLogics;
    private readonly HashSet<string> _activeClients;

    public DebugApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Debug)
            throw new ArgumentException("Role must be Debug.", nameof(config));
        
        int clientCount = _config.DebugClientCount;
        string serverUrl = _config.DebugServerUrl;
        string clientUrl = _config.DebugClientUrl;

        _harness = new VirtualTrafficHarness();
        _activeClients = new HashSet<string>();

        _serverLogic = new ServerSideLogic(
            _harness.ServerDevice, serverUrl, _logger, _serverTransportFactory,
            _config.BufferSizeBytes, _config.Verbose);

        _clientLogics = new List<ClientSideLogic>();
        for (int i = 0; i < clientCount; i++)
        {
            var clientId = new ClientId($"debug-client-{i + 1}");
            uint ipVal = (uint)(ClientBaseIp.Value + (uint)i);
            var clientIp = new IPv4(ipVal);
            string name = $"client{i}";

            var pending = new Queue<byte[]>();

            var clientTun = _harness.Network.CreateNode(name, clientIp, (fromIp, toIp, packet) =>
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

            var client = new ClientSideDebugLogic(
                clientTun, clientId, clientIp, ServerIp,
                clientUrl, _logger, _clientTransportFactory,
                _config.BufferSizeBytes, _config.Verbose,
                pending, _harness.Network);

            client.Stopped += id => RemoveClient(id.Value);
            _clientLogics.Add(client);
            AddClient(clientId.Value);
        }
        
        _logger.i($"Debug mode starting: {clientCount} virtual client(s), server={serverUrl}");
    }

    private void AddClient(string clientId)
    {
        if (!_activeClients.Add(clientId))
            _logger.w($"Duplicate client ID '{clientId}' in debug mode.");

        _logger.i($"Debug client '{clientId}' active ({_activeClients.Count} total).");
    }

    private void RemoveClient(string clientId)
    {
        if (_activeClients.Remove(clientId) && _activeClients.Count == 0)
            _serverLogic.Stop();
    }

    public override void RequestShutdown()
    {
        foreach (var c in _clientLogics)
        {
            c.RequestShutdown();
        }
    }

    public override async Task RunAsync()
    {
        TaskCompletionSource _tcs = new TaskCompletionSource();
        _serverLogic.Stopped += () => _tcs.TrySetResult();
        _serverLogic.Start();

        foreach (var c in _clientLogics)
            _ = c.Start().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.e($"Client error: {t.Exception?.GetBaseException().Message}");
            });

        await _tcs.Task;
        
        _harness.Dispose();
        
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
