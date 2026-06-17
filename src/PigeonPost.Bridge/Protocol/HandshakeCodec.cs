using System;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public static class HandshakeCodec
{
    public static byte[] EncodeRequest(ClientHandshake handshake)
    {
        ArgumentNullException.ThrowIfNull(handshake);

        uint ipv4 = handshake.AdvertisedHostIpv4.Value;
        return
        [
            (byte)(ipv4 >> 24),
            (byte)(ipv4 >> 16),
            (byte)(ipv4 >> 8),
            (byte)ipv4,
        ];
    }

    public static ClientHandshake? DecodeRequest(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 4)
            return null;

        uint ipv4 = ((uint)buffer[0] << 24)
                  | ((uint)buffer[1] << 16)
                  | ((uint)buffer[2] << 8)
                  | buffer[3];

        return new ClientHandshake(new IPv4(ipv4));
    }

    public static byte[] EncodeAck(HandshakeAck ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        return
        [
            (byte)ack.Status,
            (byte)ack.RejectCode,
        ];
    }

    public static HandshakeAck? DecodeAck(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 2)
            return null;

        byte statusByte = buffer[0];
        byte rejectCodeByte = buffer[1];

        if (!Enum.IsDefined(typeof(HandshakeAckStatus), statusByte))
            return null;

        if (!Enum.IsDefined(typeof(HandshakeRejectCode), rejectCodeByte))
            return null;

        var status = (HandshakeAckStatus)statusByte;
        var rejectCode = (HandshakeRejectCode)rejectCodeByte;

        if (status == HandshakeAckStatus.Accepted && rejectCode != HandshakeRejectCode.None)
            return null;

        return new HandshakeAck(status, rejectCode);
    }
}
