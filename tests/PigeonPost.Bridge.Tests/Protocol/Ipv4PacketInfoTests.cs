using NUnit.Framework;
using PigeonPost.Bridge.Protocol;

namespace PigeonPost.Bridge.Tests.Protocol;

[TestFixture]
public class Ipv4PacketInfoTests
{
    [Test]
    [Ignore("Production parser not yet implemented (action-03)")]
    public void TryParse_ValidIpv4Packet_ReturnsInfo()
    {
        byte[] packet = BuildMinimalIpv4Packet(
            versionIhl: 0x45,
            source: 0xC0A80101,
            dest: 0xC0A80102,
            totalLength: 20);

        var info = Ipv4PacketParser.TryParse(packet);
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.SourceAddress, Is.EqualTo(0xC0A80101u));
        Assert.That(info.DestinationAddress, Is.EqualTo(0xC0A80102u));
    }

    [Test]
    [Ignore("Production parser not yet implemented (action-03)")]
    public void TryParse_TooShort_ReturnsNull()
    {
        Assert.That(Ipv4PacketParser.TryParse(new byte[19]), Is.Null);
    }

    [Test]
    [Ignore("Production parser not yet implemented (action-03)")]
    public void TryParse_InvalidVersion_ReturnsNull()
    {
        byte[] packet = BuildMinimalIpv4Packet(
            versionIhl: 0x65,
            source: 0,
            dest: 0,
            totalLength: 20);

        Assert.That(Ipv4PacketParser.TryParse(packet), Is.Null);
    }

    [Test]
    [Ignore("Production parser not yet implemented (action-03)")]
    public void TryParse_InvalidIhl_ReturnsNull()
    {
        byte[] packet = BuildMinimalIpv4Packet(
            versionIhl: 0x44,
            source: 0,
            dest: 0,
            totalLength: 20);

        Assert.That(Ipv4PacketParser.TryParse(packet), Is.Null);
    }

    [Test]
    [Ignore("Production parser not yet implemented (action-03)")]
    public void TryParse_NonIpv4_ReturnsNull()
    {
        byte[] packet = BuildMinimalIpv4Packet(
            versionIhl: 0x60,
            source: 0,
            dest: 0,
            totalLength: 20);

        Assert.That(Ipv4PacketParser.TryParse(packet), Is.Null);
    }

    private static byte[] BuildMinimalIpv4Packet(byte versionIhl, uint source, uint dest, ushort totalLength)
    {
        var packet = new byte[totalLength];
        packet[0] = versionIhl;
        packet[1] = 0x00;
        packet[2] = (byte)(totalLength >> 8);
        packet[3] = (byte)(totalLength & 0xFF);
        packet[4] = 0x00; packet[5] = 0x00;
        packet[6] = 0x00; packet[7] = 0x00;
        packet[8] = 0x40;
        packet[9] = 0x00;
        packet[10] = 0x00; packet[11] = 0x00;
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
