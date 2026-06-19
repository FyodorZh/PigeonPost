using NUnit.Framework;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class IcmpHelperTests
{
    [Test]
    public void CreateEchoRequest_ProducesValidPacket()
    {
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 1, 1);

        Assert.That(packet[0] & 0xF0, Is.EqualTo(0x40));
        int totalLength = (packet[2] << 8) | packet[3];
        Assert.That(totalLength, Is.EqualTo(76));
        Assert.That(packet[9], Is.EqualTo(1));
        Assert.That(packet[8], Is.EqualTo(64));
        Assert.That(packet[16], Is.EqualTo(1));
        Assert.That(packet[17], Is.EqualTo(1));
        Assert.That(packet[18], Is.EqualTo(1));
        Assert.That(packet[19], Is.EqualTo(1));
        Assert.That(packet[12], Is.EqualTo(10));
        Assert.That(packet[13], Is.EqualTo(0));
        Assert.That(packet[14], Is.EqualTo(10));
        Assert.That(packet[15], Is.EqualTo(15));
        Assert.That(packet[20], Is.EqualTo(8));
    }

    [Test]
    public void CreateEchoRequest_WithCustomPayload_UsesGivenPayload()
    {
        byte[] customPayload = [1, 2, 3, 4];
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 1, 1, customPayload);

        int totalLength = (packet[2] << 8) | packet[3];
        Assert.That(totalLength, Is.EqualTo(20 + 8 + 4));
        Assert.That(packet[28], Is.EqualTo(1));
        Assert.That(packet[29], Is.EqualTo(2));
        Assert.That(packet[30], Is.EqualTo(3));
        Assert.That(packet[31], Is.EqualTo(4));
    }

    [Test]
    public void TryParseEchoReply_Valid()
    {
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 0x1234, 0x5678);

        int ipHeaderLen = (packet[0] & 0x0F) * 4;
        int totalLen = (packet[2] << 8) | packet[3];
        packet[20] = 0;
        ushort icmpChecksum = IcmpHelper.ComputeChecksum(packet, ipHeaderLen, totalLen - ipHeaderLen);
        packet[ipHeaderLen + 2] = (byte)(icmpChecksum >> 8);
        packet[ipHeaderLen + 3] = (byte)icmpChecksum;

        bool result = IcmpHelper.TryParseEchoReply(packet, out var id, out var sequence);

        Assert.That(result, Is.True);
        Assert.That(id, Is.EqualTo(0x1234));
        Assert.That(sequence, Is.EqualTo(0x5678));
    }

    [Test]
    public void TryParseEchoReply_NonIcmpProtocol_ReturnsFalse()
    {
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 1, 1);
        packet[9] = 6;

        bool result = IcmpHelper.TryParseEchoReply(packet, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryParseEchoReply_WrongType_ReturnsFalse()
    {
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 1, 1);
        packet[20] = 3;

        bool result = IcmpHelper.TryParseEchoReply(packet, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryParseEchoReply_TruncatedPacket_ReturnsFalse()
    {
        byte[] truncated = [0x45, 0, 0, 20];
        bool result = IcmpHelper.TryParseEchoReply(truncated, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryParseEchoReply_NotIpv4_ReturnsFalse()
    {
        byte[] packet = new byte[40];
        packet[0] = 0x60;
        packet[9] = 1;

        bool result = IcmpHelper.TryParseEchoReply(packet, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ComputeChecksum_KnownValue()
    {
        byte[] buffer = new byte[20];
        buffer[0] = 0x45;
        buffer[2] = 0x00; buffer[3] = 0x4C;
        buffer[8] = 64;
        buffer[9] = 1;
        buffer[12] = 10; buffer[13] = 0; buffer[14] = 10; buffer[15] = 15;
        buffer[16] = 1; buffer[17] = 1; buffer[18] = 1; buffer[19] = 1;

        ushort checksum = IcmpHelper.ComputeChecksum(buffer, 0, 20);

        Assert.That(checksum, Is.Not.EqualTo(0));
        Assert.That(checksum, Is.Not.EqualTo(0xFFFF));
        Assert.That(checksum, Is.EqualTo(0x64A1));
    }

    [Test]
    public void ComputeChecksum_OddLength()
    {
        byte[] buffer = [0x01, 0x02, 0x03];
        ushort checksum = IcmpHelper.ComputeChecksum(buffer, 0, 3);

        Assert.That(checksum, Is.Not.EqualTo(0));
    }

    [Test]
    public void CreateEchoRequest_Checksum_VerifiesCorrectly()
    {
        var packet = IcmpHelper.CreateEchoRequest(0x0A000A0F, 0x42, 0x99);

        int ipHeaderLen = (packet[0] & 0x0F) * 4;

        ushort ipCheck = IcmpHelper.ComputeChecksum(packet, 0, ipHeaderLen);
        Assert.That(ipCheck, Is.EqualTo(0));
    }
}
