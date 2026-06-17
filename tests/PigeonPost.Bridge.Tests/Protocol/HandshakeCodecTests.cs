using System;
using System.Text;
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
        var clientId = new ClientId("test-client-1");
        var handshake = new ClientHandshake(clientId, (IPv4)0xC0A80101);
        byte[] encoded = HandshakeCodec.EncodeRequest(handshake);
        var decoded = HandshakeCodec.DecodeRequest(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.ClientId, Is.EqualTo(clientId));
        Assert.That(decoded.AdvertisedHostIpv4, Is.EqualTo(new IPv4(0xC0A80101)));
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
        var ack = HandshakeAck.Rejected(HandshakeRejectCode.DuplicateClientId);
        byte[] encoded = HandshakeCodec.EncodeAck(ack);
        var decoded = HandshakeCodec.DecodeAck(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Status, Is.EqualTo(HandshakeAckStatus.Rejected));
        Assert.That(decoded.RejectCode, Is.EqualTo(HandshakeRejectCode.DuplicateClientId));
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
    public void DecodeRequest_BadMagic_ReturnsNull()
    {
        byte[] buf = new byte[10];
        buf[0] = (byte)'B'; buf[1] = (byte)'A'; buf[2] = (byte)'D'; buf[3] = (byte)'!';
        Assert.That(HandshakeCodec.DecodeRequest(buf), Is.Null);
    }

    [Test]
    public void DecodeAck_TooShortBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeAck(new byte[3]), Is.Null);
    }

    [Test]
    public void DecodeAck_NullBuffer_ReturnsNull()
    {
        Assert.That(HandshakeCodec.DecodeAck(null!), Is.Null);
    }

    [Test]
    public void DecodeAck_BadMagic_ReturnsNull()
    {
        byte[] buf = new byte[6];
        buf[0] = (byte)'X'; buf[1] = (byte)'X'; buf[2] = (byte)'X'; buf[3] = (byte)'X';
        Assert.That(HandshakeCodec.DecodeAck(buf), Is.Null);
    }

    [Test]
    public void DecodeRequest_ClientIdTooLong_ForDeclaredLength_ReturnsNull()
    {
        var clientId = new ClientId("ab");
        var hs = new ClientHandshake(clientId, (IPv4)0xC0A80101);
        byte[] encoded = HandshakeCodec.EncodeRequest(hs);

        byte[] truncated = new byte[encoded.Length - 5];
        Array.Copy(encoded, truncated, truncated.Length);
        Assert.That(HandshakeCodec.DecodeRequest(truncated), Is.Null);
    }

    [Test]
    public void ClientId_WithMaxUtf8Length_RoundTrips()
    {
        string id = new string('x', 255);
        var clientId = new ClientId(id);
        var handshake = new ClientHandshake(clientId, (IPv4)0x0A000001);
        byte[] encoded = HandshakeCodec.EncodeRequest(handshake);
        var decoded = HandshakeCodec.DecodeRequest(encoded);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.ClientId.Value, Is.EqualTo(id));
    }

    [Test]
    public void EncodeRequest_ClientIdTooLong_Throws()
    {
        string id = new string('x', 256);
        var clientId = new ClientId(id);
        var handshake = new ClientHandshake(clientId, (IPv4)0);

        Assert.That(
            () => HandshakeCodec.EncodeRequest(handshake),
            Throws.ArgumentException);
    }

    [Test]
    public void DecodeRequest_EmptyClientId_ReturnsNull()
    {
        var ms = new System.IO.MemoryStream();
        ms.Write(new byte[] { (byte)'P', (byte)'P', (byte)'H', (byte)'M' }, 0, 4);
        ms.WriteByte(0);
        ms.Write(BitConverter.GetBytes(0xC0A80101u), 0, 4);

        byte[] buf = ms.ToArray();
        Assert.That(HandshakeCodec.DecodeRequest(buf), Is.Null);
    }
}
