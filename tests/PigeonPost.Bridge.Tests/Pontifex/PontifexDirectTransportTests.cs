using System;
using System.Text;
using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Transports.Direct;
using Pontifex.Utils;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests.Pontifex;

[TestFixture]
public class PontifexDirectTransportTests
{
    private int _serverCount;

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public void ServerAndClient_Handshake_Succeeds()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        var handler = new BridgeClientHandler(clientBridge, MakeTestHandshake());
        client.Init(handler);

        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1), "Server should have one active session");
        Assert.That(clientBridge.IsConnected, Is.True, "Client bridge should be connected");

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));

        Thread.Sleep(100);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(0), "Session should be removed after disconnect");
    }

    [Test]
    public void SendPacket_ServerToClient_Delivered()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        byte[] testPacket = Encoding.UTF8.GetBytes("test-packet-data");
        var msg = PontifexPacketConverter.CreateMessage(testPacket);

        var hubEndpoint = hub.GetEndpointForTesting();
        Assert.That(hubEndpoint, Is.Not.Null, "Server hub should have a registered endpoint");
        hubEndpoint!.Send(msg);

        Thread.Sleep(200);

        Assert.That(clientBridge.ReceivedPackets, Has.Count.EqualTo(1));
        Assert.That(clientBridge.ReceivedPackets[0], Is.EqualTo(testPacket));

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void SendPacket_ClientToServer_Delivered()
    {
        string name = NextName();
        var hub = new TestServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        byte[] testPacket = new byte[] { 0x45, 0x00, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x40, 0x00,
            0x00, 0x00, 0xC0, 0xA8, 0x01, 0x01, 0x0A, 0x00, 0x00, 0x01 };
        var msg = PontifexPacketConverter.CreateMessage(testPacket);
        clientBridge.Endpoint!.Send(msg);

        Thread.Sleep(200);

        Assert.That(hub.ReceivedPackets, Has.Count.EqualTo(1));
        Assert.That(hub.ReceivedPackets[0], Is.EqualTo(testPacket));

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void ClientDisconnect_ServerHandler_OnDisconnectedCalled()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));

        Thread.Sleep(200);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(0), "Session should be removed on disconnect");
    }

    [Test]
    public void ClientReconnects_NewHandlerCreated()
    {
        string name = NextName();
        var hub = new ServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        {
            var cb = new FakeBridge();
            var client = CreateClient(name, cb);
            var cs = new ManualResetEventSlim(false);
            client.Start(_ => cs.Set());
            Thread.Sleep(200);
            Assert.That(hub.ActiveSessionCount, Is.EqualTo(1));
            client.Stop();
            cs.Wait(TimeSpan.FromSeconds(5));
        }

        Thread.Sleep(200);
        Assert.That(hub.ActiveSessionCount, Is.EqualTo(0));

        {
            var cb2 = new FakeBridge();
            var client2 = CreateClient(name, cb2);
            var cs2 = new ManualResetEventSlim(false);
            client2.Start(_ => cs2.Set());
            Thread.Sleep(200);
            Assert.That(hub.ActiveSessionCount, Is.EqualTo(1), "Server should accept reconnection");
            client2.Stop();
            cs2.Wait(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public void SendManyPackets_AllDelivered_InOrder()
    {
        string name = NextName();
        var hub = new TestServerHub(StaticLogger.Instance);
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(hub));
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var cs = new ManualResetEventSlim(false);
        client.Start(_ => cs.Set());

        Thread.Sleep(200);

        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            byte[] pkt = BuildTestIpv4Packet(i);
            clientBridge.Endpoint!.Send(PontifexPacketConverter.CreateMessage(pkt));
        }

        Thread.Sleep(500);

        Assert.That(hub.ReceivedPackets, Has.Count.EqualTo(count));

        client.Stop();
        cs.Wait(TimeSpan.FromSeconds(5));
    }

    private string NextName()
    {
        return "test_server_" + Interlocked.Increment(ref _serverCount);
    }

    private static ClientHandshake MakeTestHandshake(string clientId = "test-client")
    {
        return new ClientHandshake(new ClientId(clientId), (IPv4)0xC0A80101);
    }

    private static AckRawDirectClient CreateClient(string name, FakeBridge clientBridge, string clientId = "test-client")
    {
        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        var handshake = new ClientHandshake(new ClientId(clientId), (IPv4)0xC0A80101);
        client.Init(new BridgeClientHandler(clientBridge, handshake));
        return client;
    }

    private static byte[] BuildTestIpv4Packet(int seq)
    {
        var packet = new byte[20];
        packet[0] = 0x45;
        packet[2] = 0x00;
        packet[3] = 20;
        packet[12] = 0xC0; packet[13] = 0xA8; packet[14] = 0x01; packet[15] = 0x01;
        packet[16] = 0x0A; packet[17] = 0x00; packet[18] = 0x00; packet[19] = (byte)(seq & 0xFF);
        return packet;
    }
}
