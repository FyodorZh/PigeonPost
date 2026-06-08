using System;
using System.Threading;
using NUnit.Framework;
using Pontifex;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests;

[TestFixture]
public class BridgeTests
{
    private FakeTunDevice _tun = null!;
    private PacketBuffer _buffer = null!;
    private Bridge _bridge = null!;

    [SetUp]
    public void Setup()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
        _tun = new FakeTunDevice();
        _buffer = new PacketBuffer(1_000_000);
        _bridge = new Bridge(_tun, _buffer, StaticLogger.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _bridge.Stop(StopReason.UserIntention);
        _bridge.Dispose();
    }

    [Test]
    public void Start_OpensTunReader()
    {
        _bridge.Start();
        Thread.Sleep(100);
        Assert.That(true);
    }

    [Test]
    public void PacketsReadFromTun_Buffered_WhenNotConnected()
    {
        _tun.EnqueueIncoming(CreateIpPacket(64));
        _tun.EnqueueIncoming(CreateIpPacket(128));

        _bridge.Start();
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(2));
    }

    [Test]
    public void PacketsReadFromTun_SentDirectly_WhenConnected()
    {
        var endpoint = new FakeEndpoint();
        _bridge.OnEndpointConnected(endpoint);

        _tun.EnqueueIncoming(CreateIpPacket(64));
        _bridge.Start();
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(0));
        Assert.That(endpoint.SentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public void BufferedPackets_Drained_OnConnect()
    {
        _tun.EnqueueIncoming(CreateIpPacket(64));
        _tun.EnqueueIncoming(CreateIpPacket(128));

        _bridge.Start();
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(2));

        var endpoint = new FakeEndpoint();
        _bridge.OnEndpointConnected(endpoint);
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(0));
        Assert.That(endpoint.SentMessages, Has.Count.EqualTo(2));
    }

    [Test]
    public void IncomingPacket_WrittenToTun()
    {
        _bridge.Start();
        var packet = CreateIpPacket(100);

        _bridge.OnPacketReceived(packet);

        Assert.That(_tun.WrittenPackets, Has.Count.EqualTo(1));
        Assert.That(_tun.WrittenPackets[0], Is.EqualTo(packet));
    }

    [Test]
    public void OnTransportStopped_FiresEvent()
    {
        var fired = new ManualResetEventSlim(false);
        StopReason? reason = null;
        _bridge.OnStopped += r => { reason = r; fired.Set(); };

        _bridge.OnTransportStopped(StopReason.UserIntention);

        Assert.That(fired.Wait(TimeSpan.FromSeconds(1)), Is.True);
        Assert.That(reason, Is.SameAs(StopReason.UserIntention));
    }

    private static byte[] CreateIpPacket(int size)
    {
        var data = new byte[size];
        data[0] = 0x45;
        new Random().NextBytes(data);
        return data;
    }
}
