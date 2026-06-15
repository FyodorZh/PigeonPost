using NUnit.Framework;
using PigeonPost.Bridge.Protocol;

namespace PigeonPost.Bridge.Tests.Protocol;

[TestFixture]
public class HandshakeCodecTests
{
    [Test]
    [Ignore("Production codec not yet implemented (action-03)")]
    public void EncodeRequest_DecodeRequest_RoundTrips()
    {
        var clientId = new ClientId("test-client-1");
        var handshake = new ClientHandshake(clientId, 0xC0A80101);
        byte[] encoded = HandshakeCodec.EncodeRequest(handshake);
        var decoded = HandshakeCodec.DecodeRequest(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.ClientId, Is.EqualTo(clientId));
        Assert.That(decoded.AdvertisedHostIpv4, Is.EqualTo(0xC0A80101u));
    }

    [Test]
    [Ignore("Production codec not yet implemented (action-03)")]
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
    [Ignore("Production codec not yet implemented (action-03)")]
    public void EncodeAck_DecodeAck_RoundTrips_Rejected()
    {
        var ack = HandshakeAck.Rejected(HandshakeRejectCode.DuplicateClientId);
        byte[] encoded = HandshakeCodec.EncodeAck(ack);
        var decoded = HandshakeCodec.DecodeAck(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Status, Is.EqualTo(HandshakeAckStatus.Rejected));
        Assert.That(decoded.RejectCode, Is.EqualTo(HandshakeRejectCode.DuplicateClientId));
    }

    [Test]
    [Ignore("Production codec not yet implemented (action-03)")]
    public void DecodeRequest_TooShortBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeRequest(new byte[3]), Is.Null);
    }

    [Test]
    [Ignore("Production codec not yet implemented (action-03)")]
    public void DecodeAck_TooShortBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeAck(new byte[3]), Is.Null);
    }
}
