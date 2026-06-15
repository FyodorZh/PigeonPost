using System;

namespace PigeonPost.Bridge.Protocol;

public static class Ipv4PacketParser
{
    public static Ipv4PacketInfo? TryParse(byte[] packet)
    {
        if (packet == null || packet.Length < 20)
            return null;

        int version = packet[0] >> 4;
        if (version != 4)
            return null;

        int ihl = packet[0] & 0x0F;
        if (ihl < 5)
            return null;

        int headerLength = ihl * 4;
        if (packet.Length < headerLength)
            return null;

        uint sourceAddress = ((uint)packet[12] << 24)
                           | ((uint)packet[13] << 16)
                           | ((uint)packet[14] << 8)
                           | packet[15];

        uint destinationAddress = ((uint)packet[16] << 24)
                                | ((uint)packet[17] << 16)
                                | ((uint)packet[18] << 8)
                                | packet[19];

        byte protocol = packet[9];

        return new Ipv4PacketInfo
        {
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            HeaderLength = headerLength,
            Protocol = protocol
        };
    }
}
