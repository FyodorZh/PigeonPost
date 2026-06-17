using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using PigeonPost.Tun.Virtual;
using Scriba;

namespace PigeonPost;

internal sealed class DebugApp : BaseApp
{
    private const int MessagesPerClient = 100;
    private static readonly TimeSpan PeriodBetweenMessages = TimeSpan.FromMilliseconds(10);
    private static readonly IPv4 ServerIp = new(192, 168, 0, 1);
    private static readonly IPv4 ClientBaseIp = new(192, 168, 0, 2);

    private Func<string, bool, ITransport> TransportFactory =>
        (url, isServer) => CreateTransport(url, isServer);

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
            harness.ServerDevice, serverUrl, _logger, TransportFactory,
            _config.BufferSizeBytes, _config.Verbose);

        var clientSides = new List<ClientSideLogic>();

        for (int i = 0; i < clientCount; i++)
        {
            string clientId = $"debug-client-{i + 1}";
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

            var clientSide = new ClientSideLogic(
                clientTun, clientId, clientIp, ServerIp,
                clientUrl, _logger, _cts.Token, TransportFactory,
                serverSide.RemoveClient, pending,
                _config.BufferSizeBytes, _config.Verbose,
                harness.Network);

            clientSides.Add(clientSide);
            serverSide.AddClient(clientId);
        }

        serverSide.Start();

        foreach (var c in clientSides)
            c.Start();

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

    private sealed class ServerSideLogic
    {
        private readonly ITunDevice _tun;
        private readonly string _serverUrl;
        private readonly ILogger _logger;
        private readonly Func<string, bool, ITransport> _transportFactory;
        private readonly int _bufferSizeBytes;
        private readonly bool _verbose;

        private ServerHub? _hub;
        private BridgeImpl? _bridge;
        private ITransport? _transport;
        private int _activeClients;
        private readonly TaskCompletionSource _completionTcs = new();

        public Task Completion => _completionTcs.Task;

        public ServerSideLogic(
            ITunDevice tun,
            string serverUrl,
            ILogger logger,
            Func<string, bool, ITransport> transportFactory,
            int bufferSizeBytes,
            bool verbose)
        {
            _tun = tun;
            _serverUrl = serverUrl;
            _logger = logger;
            _transportFactory = transportFactory;
            _bufferSizeBytes = bufferSizeBytes;
            _verbose = verbose;
        }

        public void AddClient(string clientId)
        {
            Interlocked.Increment(ref _activeClients);
        }

        public void RemoveClient(string clientId)
        {
            if (Interlocked.Decrement(ref _activeClients) == 0)
            {
                Stop();
                _completionTcs.TrySetResult();
            }
        }

        public void Start()
        {
            _hub = new ServerHub(_logger, _tun);
            var buffer = new PacketBuffer(_bufferSizeBytes);
            _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);
            _bridge.SetPacketHandler(_hub.OnPacketFromTun);

            var transport = _transportFactory(_serverUrl, true);
            if (transport is not IAckRawServer ackServer)
                throw new InvalidOperationException("Server transport is not an IAckRawServer.");

            ackServer.Init(new BridgeServerAcknowledger(_hub));
            _transport = transport;

            _bridge.Start();
            ackServer.Start(reason => _logger.i($"Server transport stopped: {reason.Type}"));
        }

        public void Stop()
        {
            _hub?.StopAccepting();
            _hub?.StopAll(Pontifex.StopReason.UserIntention);
            _bridge?.Stop(Pontifex.StopReason.UserIntention);
            _transport?.Stop(Pontifex.StopReason.UserIntention);
        }
    }

    private sealed class ClientSideLogic
    {
        private readonly ITunDevice _tun;
        private readonly string _clientId;
        private readonly IPv4 _clientIp;
        private readonly IPv4 _serverIp;
        private readonly string _clientUrl;
        private readonly ILogger _logger;
        private readonly CancellationToken _externalCt;
        private readonly Func<string, bool, ITransport> _transportFactory;
        private readonly Action<string> _removeClient;
        private readonly Queue<byte[]> _pending;
        private readonly int _bufferSizeBytes;
        private readonly bool _verbose;
        private readonly VirtualNetwork _network;

        private BridgeImpl? _bridge;
        private ITransport? _transport;
        private bool _stopped;

        public ClientSideLogic(
            ITunDevice tun,
            string clientId,
            IPv4 clientIp,
            IPv4 serverIp,
            string clientUrl,
            ILogger logger,
            CancellationToken externalCt,
            Func<string, bool, ITransport> transportFactory,
            Action<string> removeClient,
            Queue<byte[]> pending,
            int bufferSizeBytes,
            bool verbose,
            VirtualNetwork network)
        {
            _tun = tun;
            _clientId = clientId;
            _clientIp = clientIp;
            _serverIp = serverIp;
            _clientUrl = clientUrl;
            _logger = logger;
            _externalCt = externalCt;
            _transportFactory = transportFactory;
            _removeClient = removeClient;
            _pending = pending;
            _bufferSizeBytes = bufferSizeBytes;
            _verbose = verbose;
            _network = network;
        }

        public void Start()
        {
            var buffer = new PacketBuffer(_bufferSizeBytes);
            _bridge = new BridgeImpl(_tun, buffer, _logger, _verbose);

            var handshake = new ClientHandshake(new ClientId(_clientId), _clientIp.Value);
            var handler = new BridgeClientHandler(_bridge, handshake);

            var transport = _transportFactory(_clientUrl, false);
            if (transport is not IAckRawClient ackClient)
                throw new InvalidOperationException("Client transport is not an IAckRawClient.");

            ackClient.Init(handler);
            _transport = transport;

            _bridge.Start();
            ackClient.Start(reason => _logger.i($"Client {_clientId} transport stopped: {reason.Type}"));

            Task.Run(RunAsync);
        }

        public void Stop()
        {
            if (_stopped)
                return;
            _stopped = true;

            _bridge?.Stop(Pontifex.StopReason.UserIntention);
            _transport?.Stop(Pontifex.StopReason.UserIntention);
        }

        private async Task RunAsync()
        {
            try
            {
                for (int seq = 0; seq < MessagesPerClient; seq++)
                {
                    await Task.Delay(PeriodBetweenMessages, _externalCt);

                    int size = Random.Shared.Next(1, 1025);
                    byte[] msg = new byte[size];
                    Random.Shared.NextBytes(msg);

                    lock (_pending)
                        _pending.Enqueue(msg);

                    _network.SendFromTo(_clientIp, _serverIp, msg);
                }

                while (true)
                {
                    if (_externalCt.IsCancellationRequested)
                        return;

                    lock (_pending)
                    {
                        if (_pending.Count == 0)
                            break;
                    }

                    await Task.Delay(10, _externalCt);
                }

                _logger.i($"Client {_clientId} completed: all {MessagesPerClient} messages sent and verified.");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Stop();
                _removeClient(_clientId);
            }
        }
    }
}
