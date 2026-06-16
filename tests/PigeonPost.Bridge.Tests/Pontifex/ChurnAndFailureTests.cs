using System.Collections.Generic;
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
public class ChurnAndFailureTests
{
    private int _counter;

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public void ThreeClients_ConnectConcurrently_AllSucceed()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridges = new List<FakeBridge>();
        var clients = new List<AckRawDirectClient>();
        var signals = new List<ManualResetEventSlim>();

        for (int i = 0; i < 3; i++)
        {
            var bridge = new FakeBridge();
            bridges.Add(bridge);
            var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
            clients.Add(client);
            client.Init(new BridgeClientHandler(bridge,
                new ClientHandshake(new ClientId($"churn-{i}"), (uint)(0xC0A80101 + i))));

            var signal = new ManualResetEventSlim(false);
            signals.Add(signal);
            client.Start(_ => signal.Set());
        }

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(3));
        for (int i = 0; i < 3; i++)
            Assert.That(bridges[i].IsConnected, Is.True, $"Client {i} should be connected");

        for (int i = 0; i < 3; i++)
        {
            clients[i].Stop();
            signals[i].Wait(5000);
        }
    }

    [Test]
    public void TwoClients_SameId_Concurrent_OnlyOneAccepted()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridge1 = new FakeBridge();
        var client1 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client1.Init(new BridgeClientHandler(bridge1,
            new ClientHandshake(new ClientId("same-id"), 0xC0A80101)));

        var bridge2 = new FakeBridge();
        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client2.Init(new BridgeClientHandler(bridge2,
            new ClientHandshake(new ClientId("same-id"), 0xC0A80102)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));

        client1.Stop(); client2.Stop();
        cs1.Wait(5000); cs2.Wait(5000);
    }

    [Test]
    public void TwoClients_SameHostIp_Concurrent_OnlyOneAccepted()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
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
            new ClientHandshake(new ClientId("client-b"), 0xC0A80101)));

        var cs1 = new ManualResetEventSlim(false);
        var cs2 = new ManualResetEventSlim(false);
        client1.Start(_ => cs1.Set());
        client2.Start(_ => cs2.Set());

        Thread.Sleep(300);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));

        client1.Stop(); client2.Stop();
        cs1.Wait(5000); cs2.Wait(5000);
    }

    [Test]
    public void RepeatedConnectDisconnect_NoSessionLeaks()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        for (int cycle = 0; cycle < 5; cycle++)
        {
            var bridge = new FakeBridge();
            var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
            client.Init(new BridgeClientHandler(bridge,
                new ClientHandshake(new ClientId($"cycle-{cycle}"), (uint)(0xC0A80101 + cycle))));

            var signal = new ManualResetEventSlim(false);
            client.Start(_ => signal.Set());
            Thread.Sleep(100);

            Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(bridge.IsConnected, Is.True);

            client.Stop();
            signal.Wait(5000);
            Thread.Sleep(100);

            Assert.That(hub.ActiveSessionCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void DisconnectedClient_CanReconnect_WithSameId()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        {
            var bridge = new FakeBridge();
            var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
            client.Init(new BridgeClientHandler(bridge,
                new ClientHandshake(new ClientId("reconn-1"), 0xC0A80101)));
            var signal = new ManualResetEventSlim(false);
            client.Start(_ => signal.Set());
            Thread.Sleep(100);

            Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
            client.Stop();
            signal.Wait(5000);
            Thread.Sleep(100);
            Assert.That(hub.ActiveSessionCount, Is.EqualTo(0));
        }

        {
            var bridge = new FakeBridge();
            var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
            client.Init(new BridgeClientHandler(bridge,
                new ClientHandshake(new ClientId("reconn-1"), 0xC0A80101)));
            var signal = new ManualResetEventSlim(false);
            client.Start(_ => signal.Set());
            Thread.Sleep(100);

            Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(bridge.IsConnected, Is.True);
            client.Stop();
            signal.Wait(5000);
        }
    }

    [Test]
    public void ServerShutdown_StopsAllClients()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var bridges = new List<FakeBridge>();
        var clients = new List<AckRawDirectClient>();
        var signals = new List<ManualResetEventSlim>();

        for (int i = 0; i < 3; i++)
        {
            var bridge = new FakeBridge();
            bridges.Add(bridge);
            var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
            clients.Add(client);
            client.Init(new BridgeClientHandler(bridge,
                new ClientHandshake(new ClientId($"sc-{i}"), (uint)(0xC0A80101 + i))));

            var signal = new ManualResetEventSlim(false);
            signals.Add(signal);
            client.Start(_ => signal.Set());
        }

        Thread.Sleep(300);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(3));

        hub.StopAccepting();
        hub.StopAll(StopReason.UserIntention);

        Thread.Sleep(200);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(0));

        foreach (var signal in signals)
            signal.Wait(5000);
    }

    [Test]
    public void InvalidPackets_DoNotAffect_ValidTraffic()
    {
        var hub = new ServerHub(StaticLogger.Instance);

        hub.TryRegisterSession(
            new ClientHandshake(new ClientId("vp-client"), 0xC0A80101), out _);

        byte[] invalidSource = BuildPacket(source: 0xC0A80199, dest: 0x0A000001);
        byte[] validPacket = BuildPacket(source: 0xC0A80101, dest: 0x0A000001);
        byte[] malformed = new byte[] { 0x45 };

        hub.OnPacketFromClient(new ClientId("vp-client"), invalidSource);
        hub.OnPacketFromClient(new ClientId("vp-client"), validPacket);
        hub.OnPacketFromClient(new ClientId("vp-client"), malformed);

        Assert.That(hub.DroppedInvalidSource, Is.EqualTo(1));
        Assert.That(hub.DroppedMalformedIpv4, Is.EqualTo(1));
    }

    private string NextName()
    {
        return "churn_" + Interlocked.Increment(ref _counter);
    }

    private static byte[] BuildPacket(uint source, uint dest)
    {
        var packet = new byte[20];
        packet[0] = 0x45;
        packet[2] = 0x00;
        packet[3] = 20;
        packet[12] = (byte)(source >> 24);
        packet[13] = (byte)(source >> 16);
        packet[14] = (byte)(source >> 8);
        packet[15] = (byte)(source & 0xFF);
        packet[16] = (byte)(dest >> 24);
        packet[17] = (byte)(dest >> 16);
        packet[18] = (byte)(dest >> 8);
        packet[19] = (byte)(dest & 0xFF);
        return packet;
    }
}
