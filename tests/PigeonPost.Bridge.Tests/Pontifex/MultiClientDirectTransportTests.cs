using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex;
using Pontifex.Transports.Direct;
using PigeonPost.Bridge;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests.Pontifex;

[TestFixture]
public class MultiClientDirectTransportTests
{
    private int _counter;

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public void Server_Accepts_TwoClients_Simultaneously()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridge1 = new FakeBridge();
        var client1 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client1.Init(new BridgeClientHandler(bridge1,
            new ClientHandshake(new ClientId("client-1"), 0xC0A80101)));

        var bridge2 = new FakeBridge();
        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client2.Init(new BridgeClientHandler(bridge2,
            new ClientHandshake(new ClientId("client-2"), 0xC0A80102)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(2));
        Assert.That(bridge1.IsConnected, Is.True);
        Assert.That(bridge2.IsConnected, Is.True);

        client1.Stop(); client2.Stop();
        cs1.Wait(5000); cs2.Wait(5000);
    }

    [Test]
    public void DuplicateClientId_IsRejected()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridge1 = new FakeBridge();
        var client1 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client1.Init(new BridgeClientHandler(bridge1,
            new ClientHandshake(new ClientId("dup-id"), 0xC0A80101)));

        var bridge2 = new FakeBridge();
        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client2.Init(new BridgeClientHandler(bridge2,
            new ClientHandshake(new ClientId("dup-id"), 0xC0A80102)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
        Assert.That(bridge1.IsConnected, Is.True, "First client should be connected");

        client1.Stop(); client2.Stop();
        cs1.Wait(5000); cs2.Wait(5000);
    }

    [Test]
    public void OneClientDisconnect_DoesNotAffect_Others()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridge1 = new FakeBridge();
        var client1 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client1.Init(new BridgeClientHandler(bridge1,
            new ClientHandshake(new ClientId("client-1"), 0xC0A80101)));

        var bridge2 = new FakeBridge();
        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client2.Init(new BridgeClientHandler(bridge2,
            new ClientHandshake(new ClientId("client-2"), 0xC0A80102)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(2));

        client1.Stop();
        cs1.Wait(5000);
        Thread.Sleep(200);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
        Assert.That(bridge2.IsConnected, Is.True, "Client 2 should still be connected");

        client2.Stop();
        cs2.Wait(5000);
    }

    [Test]
    public void Traffic_ToAdvertisedHost_ReachesCorrectClient()
    {
        string name = NextName();
        var hub = new TestServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridge1 = new FakeBridge();
        var client1 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client1.Init(new BridgeClientHandler(bridge1,
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101)));

        var bridge2 = new FakeBridge();
        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client2.Init(new BridgeClientHandler(bridge2,
            new ClientHandshake(new ClientId("client-b"), 0xC0A80102)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(2));
        Assert.That(hub.ReceivedPackets, Has.Count.EqualTo(0));

        client1.Stop(); client2.Stop();
        cs1.Wait(5000); cs2.Wait(5000);
    }

    private string NextName()
    {
        return "multiclient_" + Interlocked.Increment(ref _counter);
    }
}
