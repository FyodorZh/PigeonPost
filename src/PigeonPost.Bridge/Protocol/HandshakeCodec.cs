using System;

namespace PigeonPost.Bridge.Protocol;

public static class HandshakeCodec
{
    public static byte[] EncodeRequest(ClientHandshake handshake)
    {
        throw new NotImplementedException();
    }

    public static ClientHandshake? DecodeRequest(byte[] buffer)
    {
        throw new NotImplementedException();
    }

    public static byte[] EncodeAck(HandshakeAck ack)
    {
        throw new NotImplementedException();
    }

    public static HandshakeAck? DecodeAck(byte[] buffer)
    {
        throw new NotImplementedException();
    }
}
