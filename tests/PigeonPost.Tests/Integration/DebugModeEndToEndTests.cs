using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex;
using Pontifex.Transports.Direct;
using PigeonPost.Bridge;
using PigeonPost.Bridge.Handlers;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;
using BridgeClass = PigeonPost.Bridge.Bridge;

namespace PigeonPost.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class DebugModeEndToEndTests
{
    private const string TunAName = "tunA";
    private const string TunBName = "tunB";
    private const string DirectServerName = "e2e_test_server";

    private AckRawDirectServer _server = null!;
    private AckRawDirectClient _client = null!;
    private TunDevice _tunA = null!;
    private TunDevice _tunB = null!;
    private BridgeClass _bridgeA = null!;
    private BridgeClass _bridgeB = null!;

    [SetUp]
    public void SetUp()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());

        _tunA = new TunDevice();
        _tunB = new TunDevice();
        _tunA.Open(TunAName);
        _tunB.Open(TunBName);

        var bufferA = new PacketBuffer(100_000);
        var bufferB = new PacketBuffer(100_000);

        _bridgeA = new BridgeClass(_tunA, bufferA, StaticLogger.Instance, verbose: true);
        _bridgeB = new BridgeClass(_tunB, bufferB, StaticLogger.Instance, verbose: true);

        _server = new AckRawDirectServer(DirectServerName, StaticLogger.Instance, MemoryRental.Shared);
        _server.Init(new BridgeServerAcknowledger(_bridgeA));

        _client = new AckRawDirectClient(DirectServerName, StaticLogger.Instance, MemoryRental.Shared);
        _client.Init(new BridgeClientHandler(_bridgeB));

        _bridgeA.Start();
        _bridgeB.Start();

        _server.Start(_ => { });
        _client.Start(_ => { });

        Thread.Sleep(200);
    }

    [TearDown]
    public void TearDown()
    {
        if (_client != null) _client.Stop(StopReason.UserIntention);
        if (_bridgeB != null) _bridgeB.Stop(StopReason.UserIntention);
        if (_bridgeA != null) _bridgeA.Stop(StopReason.UserIntention);
        if (_tunB != null) _tunB.Close();
        if (_tunA != null) _tunA.Close();
    }

    [Test]
    public void PacketFromA_ArrivesAtB()
    {
        byte[] packet = PacketBuilder.BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { 0x01, 0x02, 0x03 });

        _tunA.Write(packet);
        Thread.Sleep(500);

        byte[] readBuf = new byte[65536];
        int bytesRead = _tunB.Read(readBuf);

        Assert.That(bytesRead, Is.GreaterThan(0));
        Assert.That(readBuf[0] >> 4, Is.EqualTo(4));
    }

    [Test]
    public void PacketFromB_ArrivesAtA()
    {
        byte[] packet = PacketBuilder.BuildIcmpPacket("10.99.0.2", "10.99.0.1", new byte[] { 0xAA, 0xBB });

        _tunB.Write(packet);
        Thread.Sleep(500);

        byte[] readBuf = new byte[65536];
        int bytesRead = _tunA.Read(readBuf);

        Assert.That(bytesRead, Is.GreaterThan(0));
    }

    [Test]
    public void MultiplePackets_AllDelivered_InOrder()
    {
        const int count = 50;

        for (int i = 0; i < count; i++)
        {
            byte[] packet = PacketBuilder.BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { (byte)i });
            _tunA.Write(packet);
            Thread.Sleep(2);
        }

        byte[] readBuf = new byte[65536];
        int received = 0;
        var receivedPayloads = new List<byte>();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (received < count && DateTime.UtcNow < deadline)
        {
            int n = _tunB.Read(readBuf);
            if (n > 0)
            {
                received++;
                receivedPayloads.Add(readBuf[n - 1]);
            }
        }

        Assert.That(received, Is.EqualTo(count));

        for (int i = 0; i < count; i++)
        {
            Assert.That(receivedPayloads[i], Is.EqualTo((byte)i));
        }
    }
}
