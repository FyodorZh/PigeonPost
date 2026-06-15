using NUnit.Framework;
using PigeonPost.Bridge.Protocol;
using PigeonPost.Bridge.Server;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class InboundSourceValidationTests
{
    private FakeServerHub _hub = null!;

    [SetUp]
    public void Setup()
    {
        _hub = new FakeServerHub();
    }

    [Test]
    public void ClientPacket_SourceEqualsAdvertisedIp_Accepted()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);

        byte[] packet = BuildIpv4Packet(source: 0xC0A80101, dest: 0x0A000001);

        _hub.OnPacketFromClient(new ClientId("client-a"), packet);

        Assert.That(_hub.DroppedInvalidSource, Is.EqualTo(0));
        Assert.That(_hub.PacketsWrittenToTun, Has.Count.EqualTo(1));
    }

    [Test]
    public void ClientPacket_SourceDiffersFromAdvertisedIp_Dropped()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);

        byte[] packet = BuildIpv4Packet(source: 0xC0A80199, dest: 0x0A000001);

        _hub.OnPacketFromClient(new ClientId("client-a"), packet);

        Assert.That(_hub.DroppedInvalidSource, Is.EqualTo(1));
        Assert.That(_hub.PacketsWrittenToTun, Has.Count.EqualTo(0));
    }

    [Test]
    public void ClientPacket_MalformedIpv4_Dropped()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);

        byte[] packet = new byte[] { 0x45, 0x00 };

        _hub.OnPacketFromClient(new ClientId("client-a"), packet);

        Assert.That(_hub.DroppedMalformedIpv4, Is.EqualTo(1));
        Assert.That(_hub.PacketsWrittenToTun, Has.Count.EqualTo(0));
    }

    [Test]
    public void MultipleClients_EachValidatedSeparately()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-b"), 0xC0A80102), out _);

        byte[] validA = BuildIpv4Packet(source: 0xC0A80101, dest: 0x0A000001);
        byte[] spoofedB = BuildIpv4Packet(source: 0xC0A80199, dest: 0x0A000002);
        byte[] validB = BuildIpv4Packet(source: 0xC0A80102, dest: 0x0A000002);

        _hub.OnPacketFromClient(new ClientId("client-a"), validA);
        _hub.OnPacketFromClient(new ClientId("client-b"), spoofedB);
        _hub.OnPacketFromClient(new ClientId("client-b"), validB);

        Assert.That(_hub.DroppedInvalidSource, Is.EqualTo(1));
        Assert.That(_hub.DroppedMalformedIpv4, Is.EqualTo(0));
        Assert.That(_hub.PacketsWrittenToTun, Has.Count.EqualTo(2));
    }

    private static byte[] BuildIpv4Packet(uint source, uint dest)
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
