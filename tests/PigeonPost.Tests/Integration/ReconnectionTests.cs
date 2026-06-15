using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Transports.Direct;
using PigeonPost.Bridge;
using PigeonPost.Bridge.Handlers;
using PigeonPost.Bridge.Protocol;
using PigeonPost.Bridge.Server;
using PigeonPost.Bridge.Utils;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;
using BridgeClass = PigeonPost.Bridge.Bridge;

namespace PigeonPost.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class ReconnectionTests
{
    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public void ClientReconnects_DirectTransport()
    {
        string name = "reconn_test_" + Guid.NewGuid().ToString("N");
        var serverHub = new TestServerHub(StaticLogger.Instance);
        var clientBridge = new RecordingBridge();

        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(serverHub));
        server.Start(_ => { });

        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(clientBridge, new ClientHandshake(new ClientId("reconn-c1"), 0xC0A80101)));
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(300);

        Assert.That(serverHub.ActiveSessionCount, Is.EqualTo(1), "Server should have one active session");
        Assert.That(clientBridge.IsConnected, Is.True, "Client should be connected");

        byte[] packet1 = BuildTestPacket(seq: 1);
        clientBridge.Endpoint!.Send(PontifexPacketConverter.CreateMessage(packet1));
        Thread.Sleep(200);

        Assert.That(serverHub.ReceivedPackets, Has.Count.EqualTo(1));

        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
        Thread.Sleep(200);

        Assert.That(serverHub.ActiveSessionCount, Is.EqualTo(0), "Session should be removed");

        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        var clientBridge2 = new RecordingBridge();
        client2.Init(new BridgeClientHandler(clientBridge2, new ClientHandshake(new ClientId("reconn-c2"), 0xC0A80102)));
        var client2Stopped = new ManualResetEventSlim(false);
        client2.Start(_ => client2Stopped.Set());

        Thread.Sleep(300);

        Assert.That(serverHub.ActiveSessionCount, Is.EqualTo(1), "Server should accept reconnection");
        Assert.That(clientBridge2.IsConnected, Is.True, "Client2 should be connected");

        byte[] packet2 = BuildTestPacket(seq: 2);
        clientBridge2.Endpoint!.Send(PontifexPacketConverter.CreateMessage(packet2));
        Thread.Sleep(200);

        Assert.That(serverHub.ReceivedPackets, Has.Count.EqualTo(2));

        client2.Stop();
        client2Stopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void ClientReconnects_AfterServerRestart()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        string name = "reconn_e2e_" + Guid.NewGuid().ToString("N");

        using var tunA = new TunDevice();
        using var tunB = new TunDevice();
        tunA.Open("tunA");
        tunB.Open("tunB");

        var bufferA = new PacketBuffer(100_000);
        var bufferB = new PacketBuffer(100_000);

        using var bridgeA = new BridgeClass(tunA, bufferA, StaticLogger.Instance, verbose: true);
        using var bridgeB = new BridgeClass(tunB, bufferB, StaticLogger.Instance, verbose: true);

        var serverHub = new ServerHub(StaticLogger.Instance, tunA);
        bridgeA.SetPacketHandler(packet => serverHub.OnPacketFromTun(packet));

        bridgeA.Start();
        bridgeB.Start();

        var server = new AckRawDirectServer(name, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(serverHub));

        var client = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(bridgeB, new ClientHandshake(new ClientId("e2e-client"), 0x0A630001)));

        server.Start(_ => { });
        client.Start(_ => { });

        Thread.Sleep(300);

        byte[] packet1 = PacketBuilder.BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { 0x01 });
        tunA.Write(packet1);
        Thread.Sleep(500);

        byte[] readBuf = new byte[65536];
        int bytesRead = tunB.Read(readBuf);

        Assert.That(bytesRead, Is.GreaterThan(0));

        client.Stop();
        Thread.Sleep(200);

        var client2 = new AckRawDirectClient(name, StaticLogger.Instance, MemoryRental.Shared);
        var handler2 = new BridgeClientHandler(bridgeB, new ClientHandshake(new ClientId("e2e-client"), 0x0A630001));
        client2.Init(handler2);
        client2.Start(_ => { });

        Thread.Sleep(300);

        byte[] packet2 = PacketBuilder.BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { 0x02 });
        tunA.Write(packet2);
        Thread.Sleep(500);

        bytesRead = tunB.Read(readBuf);
        Assert.That(bytesRead, Is.GreaterThan(0));

        client2.Stop();
    }

    private static byte[] BuildTestPacket(int seq)
    {
        var packet = new byte[20];
        packet[0] = 0x45;
        packet[2] = 0x00;
        packet[3] = 20;
        packet[12] = 0xC0; packet[13] = 0xA8; packet[14] = 0x01; packet[15] = 0x01;
        packet[16] = 0x0A; packet[17] = 0x00; packet[18] = 0x00; packet[19] = (byte)seq;
        return packet;
    }

    private sealed class TestServerHub : ServerHub
    {
        private readonly object _lock = new();
        public List<byte[]> ReceivedPackets { get; } = new();

        public TestServerHub(ILogger logger) : base(logger) { }

        public override void OnPacketFromClient(ClientId clientId, byte[] packet)
        {
            lock (_lock) ReceivedPackets.Add((byte[])packet.Clone());
            base.OnPacketFromClient(clientId, packet);
        }
    }

    private sealed class RecordingBridge : IBridge
    {
        private readonly object _lock = new();

        public bool IsConnected { get; private set; }
        public IAckRawBaseEndpoint? Endpoint { get; private set; }
        public List<byte[]> ReceivedPackets { get; } = new();
        public bool Stopped { get; private set; }
        public StopReason? StopReason { get; private set; }

        public void OnEndpointConnected(IAckRawBaseEndpoint endpoint)
        {
            lock (_lock)
            {
                IsConnected = true;
                Endpoint = endpoint;
            }
        }

        public void OnEndpointDisconnected()
        {
            lock (_lock)
            {
                IsConnected = false;
                Endpoint = null;
            }
        }

        public void OnPacketReceived(byte[] packet)
        {
            lock (_lock)
            {
                ReceivedPackets.Add((byte[])packet.Clone());
            }
        }

        public bool TryGetNextPacket(out byte[] packet)
        {
            packet = null!;
            return false;
        }

        public void OnTransportStopped(StopReason reason)
        {
            lock (_lock)
            {
                Stopped = true;
                StopReason = reason;
                IsConnected = false;
                Endpoint = null;
            }
        }
    }
}
