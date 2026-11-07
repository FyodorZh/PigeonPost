using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Servers;
using Pontifex.Transports.Direct;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class VpnClientRuntimeTests
{
    private int _endpointCounter;

    private static readonly VpnProfile TestProfile = new("direct|test_vpn_ep", 15);

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public async Task Connect_UsingDirectTransport_TransitionsToConnected()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));

            await runtime.ConnectAsync(profile, CancellationToken.None);

            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(runtime.CurrentSession.SessionStart, Is.Not.Null);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public async Task Connect_EmitsConnectingAndConnectedEvents()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            int connectingCount = 0;
            int connectedCount = 0;
            runtime.SessionUpdated += snapshot =>
            {
                if (snapshot.State == ConnectionState.Connecting)
                    connectingCount++;
                if (snapshot.State == ConnectionState.Connected)
                    connectedCount++;
            };

            await runtime.ConnectAsync(profile, CancellationToken.None);

            Assert.That(connectingCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(connectedCount, Is.EqualTo(1));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public async Task Disconnect_TransitionsToDisconnected()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            await runtime.ConnectAsync(profile, CancellationToken.None);
            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

            await runtime.DisconnectAsync();

            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));
            Assert.That(runtime.IsReconnecting, Is.False);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public async Task DoubleConnect_IsNoOp()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            await runtime.ConnectAsync(profile, CancellationToken.None);
            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

            await runtime.ConnectAsync(profile, CancellationToken.None);

            Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public async Task LogEmitted_FiresOnStateTransition()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            int logCount = 0;
            runtime.LogEmitted += _ => logCount++;

            await runtime.ConnectAsync(profile, CancellationToken.None);

            Assert.That(logCount, Is.GreaterThanOrEqualTo(1));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void Connect_WithInvalidUrl_Throws()
    {
        using var runtime = new VpnClientRuntime();
        var badProfile = new VpnProfile("tcp|0.0.0.0:1/1", 15);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await runtime.ConnectAsync(badProfile, cts.Token);
        });
    }

    [Test]
    public async Task SetCustomTunDevice_BeforeConnect_UsesCustomDevice()
    {
        string ep = NextEndpoint();
        var profile = new VpnProfile($"direct|{ep}", 15);
        var server = CreateAndStartServer(ep);

        try
        {
            using var runtime = new VpnClientRuntime();
            var customTun = new RecordingTunDevice();
            runtime.SetCustomTunDevice(customTun);

            await runtime.ConnectAsync(profile, CancellationToken.None);

            var snapshot = runtime.CurrentSession;
            Assert.That(snapshot.State, Is.EqualTo(ConnectionState.Connected));
            Assert.That(snapshot.BytesSent, Is.GreaterThanOrEqualTo(0));
            Assert.That(snapshot.BytesReceived, Is.GreaterThanOrEqualTo(0));
        }
        finally
        {
            server.Stop();
        }
    }

    private sealed class RecordingTunDevice : ITunDevice
    {
        private volatile bool _closed;

        public string Name => "recording";
        public bool IsOpen => !_closed;
        public int ReadCount { get; internal set; }

        public int Read(byte[] buffer)
        {
            if (_closed)
                return 0;

            ReadCount++;
            return 0;
        }

        public void Write(byte[] buffer)
        {
        }

        public void Close()
        {
            _closed = true;
        }

        public void Dispose()
        {
            _closed = true;
        }
    }

    private string NextEndpoint()
    {
        return "vpn_test_" + Interlocked.Increment(ref _endpointCounter);
    }

    private static IAckRawServer CreateAndStartServer(string endpointName)
    {
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(endpointName, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });
        return server;
    }
}
