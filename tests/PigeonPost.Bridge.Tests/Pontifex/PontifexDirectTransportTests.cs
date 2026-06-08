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
using PigeonPost.Bridge.Handlers;
using PigeonPost.Bridge.Utils;
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
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        var bridge = new FakeBridge();
        server.Init(new BridgeServerAcknowledger(bridge));
        server.Start(_ => { });

        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        var clientBridge = new FakeBridge();
        var handler = new BridgeClientHandler(clientBridge);
        client.Init(handler);

        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        Assert.That(bridge.IsConnected, Is.True, "Server bridge should be connected");
        Assert.That(clientBridge.IsConnected, Is.True, "Client bridge should be connected");

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void SendPacket_ServerToClient_Delivered()
    {
        string name = NextName();
        var serverBridge = new FakeBridge();
        var server = CreateServer(name, serverBridge);
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        byte[] testPacket = Encoding.UTF8.GetBytes("test-packet-data");
        var msg = PontifexPacketConverter.CreateMessage(testPacket);
        serverBridge.Endpoint!.Send(msg);

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
        var serverBridge = new FakeBridge();
        var server = CreateServer(name, serverBridge);
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        byte[] testPacket = new byte[] { 0x45, 0x00, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x40, 0x00 };
        var msg = PontifexPacketConverter.CreateMessage(testPacket);
        clientBridge.Endpoint!.Send(msg);

        Thread.Sleep(200);

        Assert.That(serverBridge.ReceivedPackets, Has.Count.EqualTo(1));
        Assert.That(serverBridge.ReceivedPackets[0], Is.EqualTo(testPacket));

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void ClientDisconnect_ServerHandler_OnDisconnectedCalled()
    {
        string name = NextName();
        var serverBridge = new FakeBridge();
        var server = CreateServer(name, serverBridge);
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);
        Assert.That(serverBridge.IsConnected, Is.True);

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));

        Thread.Sleep(200);
        Assert.That(serverBridge.IsConnected, Is.False, "Server should detect client disconnect");
    }

    [Test]
    public void ClientReconnects_NewHandlerCreated()
    {
        string name = NextName();
        var serverBridge = new FakeBridge();
        var server = CreateServer(name, serverBridge);
        server.Start(_ => { });

        {
            var cb = new FakeBridge();
            var client = CreateClient(name, cb);
            var cs = new ManualResetEventSlim(false);
            client.Start(_ => cs.Set());
            Thread.Sleep(200);
            Assert.That(serverBridge.IsConnected, Is.True);
            client.Stop();
            cs.Wait(TimeSpan.FromSeconds(5));
        }

        Thread.Sleep(200);

        {
            var cb2 = new FakeBridge();
            var client2 = CreateClient(name, cb2);
            var cs2 = new ManualResetEventSlim(false);
            client2.Start(_ => cs2.Set());
            Thread.Sleep(200);
            Assert.That(serverBridge.IsConnected, Is.True, "Server should accept reconnection");
            client2.Stop();
            cs2.Wait(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public void SendManyPackets_AllDelivered_InOrder()
    {
        string name = NextName();
        var serverBridge = new FakeBridge();
        var server = CreateServer(name, serverBridge);
        server.Start(_ => { });

        var clientBridge = new FakeBridge();
        var client = CreateClient(name, clientBridge);
        var cs = new ManualResetEventSlim(false);
        client.Start(_ => cs.Set());

        Thread.Sleep(200);

        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            byte[] pkt = BitConverter.GetBytes(i);
            clientBridge.Endpoint!.Send(PontifexPacketConverter.CreateMessage(pkt));
        }

        Thread.Sleep(500);

        Assert.That(serverBridge.ReceivedPackets, Has.Count.EqualTo(count));
        for (int i = 0; i < count; i++)
        {
            Assert.That(BitConverter.ToInt32(serverBridge.ReceivedPackets[i], 0), Is.EqualTo(i));
        }

        client.Stop();
        cs.Wait(TimeSpan.FromSeconds(5));
    }

    private string NextName()
    {
        return "test_server_" + Interlocked.Increment(ref _serverCount);
    }

    private static AckRawDirectServer CreateServer(string name, FakeBridge serverBridge)
    {
        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(serverBridge));
        return server;
    }

    private static AckRawDirectClient CreateClient(string name, FakeBridge clientBridge)
    {
        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(clientBridge));
        return client;
    }
}
