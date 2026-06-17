using System;
using NUnit.Framework;
using PigeonPost.Bridge;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests.Protocol;

[TestFixture]
public class HandshakeCodecTests
{
    [Test]
    public void EncodeRequest_DecodeRequest_RoundTrips()
    {
        var handshake = new ClientHandshake(new IPv4(0xC0A80101));
        byte[] encoded = HandshakeCodec.EncodeRequest(handshake);
        var decoded = HandshakeCodec.DecodeRequest(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.AdvertisedHostIpv4, Is.EqualTo(new IPv4(0xC0A80101)));
    }

    [Test]
    public void EncodeAck_DecodeAck_RoundTrips_Accepted()
    {
        var ack = HandshakeAck.Accepted();
        byte[] encoded = HandshakeCodec.EncodeAck(ack);
        var decoded = HandshakeCodec.DecodeAck(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Status, Is.EqualTo(HandshakeAckStatus.Accepted));
        Assert.That(decoded.RejectCode, Is.EqualTo(HandshakeRejectCode.None));
    }

    [Test]
    public void EncodeAck_DecodeAck_RoundTrips_Rejected()
    {
        var ack = HandshakeAck.Rejected(HandshakeRejectCode.DuplicateHostIp);
        byte[] encoded = HandshakeCodec.EncodeAck(ack);
        var decoded = HandshakeCodec.DecodeAck(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Status, Is.EqualTo(HandshakeAckStatus.Rejected));
        Assert.That(decoded.RejectCode, Is.EqualTo(HandshakeRejectCode.DuplicateHostIp));
    }

    [Test]
    public void DecodeRequest_TooShortBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeRequest(new byte[3]), Is.Null);
    }

    [Test]
    public void DecodeRequest_NullBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeRequest(null!), Is.Null);
    }

    [Test]
    public void DecodeRequest_AnyData_InterpretsAsIp()
    {
        byte[] buf = new byte[] { 0xC0, 0xA8, 0x01, 0x01 };
        var result = HandshakeCodec.DecodeRequest(buf);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.AdvertisedHostIpv4, Is.EqualTo(new IPv4(0xC0A80101)));
    }

    [Test]
    public void DecodeAck_TooShortBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeAck(new byte[1]), Is.Null);
    }

    [Test]
    public void DecodeAck_NullBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeAck(null!), Is.Null);
    }

    [Test]
    public void DecodeAck_BadStatus_ReturnsNull()
    {
        byte[] buf = new byte[] { 0xFF, 0x00 };
        Assert.That(HandshakeCodec.DecodeAck(buf), Is.Null);
    }
}
