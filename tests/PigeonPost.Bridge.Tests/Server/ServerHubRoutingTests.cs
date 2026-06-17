using NUnit.Framework;
using PigeonPost.Bridge;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class ServerHubRoutingTests
{
    private FakeServerHub _hub = null!;

    [SetUp]
    public void Setup()
    {
        _hub = new FakeServerHub();
    }

    [Test]
    public void Packet_DestinedToClientA_SentOnlyToClientA()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), (IPv4)0xC0A80101), out _);
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-b"), (IPv4)0xC0A80102), out _);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(0));
        Assert.That(_hub.PacketsSentToClient["client-a"], Has.Count.EqualTo(1));
        Assert.That(_hub.PacketsSentToClient["client-b"], Has.Count.EqualTo(0));
    }

    [Test]
    public void Packet_NoMatchingHostRoute_Dropped()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), (IPv4)0xC0A80101), out _);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A801FF, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(1));
        Assert.That(_hub.PacketsSentToClient["client-a"], Has.Count.EqualTo(0));
    }

    [Test]
    public void Packet_NoBroadcastBehavior()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), (IPv4)0xC0A80101), out _);
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-b"), (IPv4)0xC0A80102), out _);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.PacketsSentToClient["client-a"], Has.Count.EqualTo(1));
        Assert.That(_hub.PacketsSentToClient["client-b"], Has.Count.EqualTo(0));
    }

    [Test]
    public void DisconnectedClient_RouteDropped()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), (IPv4)0xC0A80101), out _);
        _hub.RemoveSession(new ClientId("client-a"));

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(1));
    }

    private static byte[] BuildIpv4Packet(uint dest, uint source)
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
