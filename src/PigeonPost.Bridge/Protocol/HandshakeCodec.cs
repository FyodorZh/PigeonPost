using System;
using System.Text;

namespace PigeonPost.Bridge.Protocol;

public static class HandshakeCodec
{
    private static readonly byte[] Magic = { (byte)'P', (byte)'P', (byte)'H', (byte)'M' };

    public static byte[] EncodeRequest(ClientHandshake handshake)
    {
        ArgumentNullException.ThrowIfNull(handshake);

        byte[] clientIdBytes = Encoding.UTF8.GetBytes(handshake.ClientId.Value);

        if (clientIdBytes.Length > 255)
            throw new ArgumentException("clientId must be 255 bytes or fewer in UTF-8 encoding.", nameof(handshake));

        int totalLength = Magic.Length + 1 + clientIdBytes.Length + 4;
        byte[] buffer = new byte[totalLength];

        Buffer.BlockCopy(Magic, 0, buffer, 0, Magic.Length);
        buffer[Magic.Length] = (byte)clientIdBytes.Length;

        Buffer.BlockCopy(clientIdBytes, 0, buffer, Magic.Length + 1, clientIdBytes.Length);

        uint ipv4 = handshake.AdvertisedHostIpv4;
        int ipOffset = Magic.Length + 1 + clientIdBytes.Length;
        buffer[ipOffset] = (byte)(ipv4 >> 24);
        buffer[ipOffset + 1] = (byte)(ipv4 >> 16);
        buffer[ipOffset + 2] = (byte)(ipv4 >> 8);
        buffer[ipOffset + 3] = (byte)ipv4;

        return buffer;
    }

    public static ClientHandshake? DecodeRequest(byte[] buffer)
    {
        if (buffer == null)
            return null;

        if (buffer.Length < Magic.Length + 1 + 1 + 4)
            return null;

        for (int i = 0; i < Magic.Length; i++)
        {
            if (buffer[i] != Magic[i])
                return null;
        }

        int clientIdLength = buffer[Magic.Length];
        int expectedLength = Magic.Length + 1 + clientIdLength + 4;

        if (buffer.Length < expectedLength)
            return null;

        string clientIdStr = Encoding.UTF8.GetString(buffer, Magic.Length + 1, clientIdLength);

        if (string.IsNullOrEmpty(clientIdStr))
            return null;

        int ipOffset = Magic.Length + 1 + clientIdLength;
        uint ipv4 = ((uint)buffer[ipOffset] << 24)
                  | ((uint)buffer[ipOffset + 1] << 16)
                  | ((uint)buffer[ipOffset + 2] << 8)
                  | buffer[ipOffset + 3];

        return new ClientHandshake(new ClientId(clientIdStr), ipv4);
    }

    public static byte[] EncodeAck(HandshakeAck ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        byte[] buffer = new byte[Magic.Length + 1 + 1];
        Buffer.BlockCopy(Magic, 0, buffer, 0, Magic.Length);
        buffer[Magic.Length] = (byte)ack.Status;
        buffer[Magic.Length + 1] = (byte)ack.RejectCode;

        return buffer;
    }

    public static HandshakeAck? DecodeAck(byte[] buffer)
    {
        if (buffer == null)
            return null;

        if (buffer.Length < Magic.Length + 1 + 1)
            return null;

        for (int i = 0; i < Magic.Length; i++)
        {
            if (buffer[i] != Magic[i])
                return null;
        }

        byte statusByte = buffer[Magic.Length];
        byte rejectCodeByte = buffer[Magic.Length + 1];

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
