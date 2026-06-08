using System;

namespace PigeonPost.Tests.Integration;

internal static class PacketBuilder
{
    public static byte[] BuildIcmpPacket(string srcIp, string dstIp, byte[] payload)
    {
        byte[] src = ParseIp(srcIp);
        byte[] dst = ParseIp(dstIp);

        int totalLength = 20 + 8 + payload.Length;
        var packet = new byte[totalLength];

        packet[0] = 0x45;
        packet[1] = 0x00;
        packet[2] = (byte)(totalLength >> 8);
        packet[3] = (byte)(totalLength & 0xFF);
        packet[4] = 0x00;
        packet[5] = 0x01;
        packet[6] = 0x00;
        packet[7] = 0x00;
        packet[8] = 0x40;
        packet[9] = 0x01;
        packet[10] = 0x00;
        packet[11] = 0x00;
        Array.Copy(src, 0, packet, 12, 4);
        Array.Copy(dst, 0, packet, 16, 4);

        ushort ipChecksum = ComputeChecksum(packet, 0, 20);
        packet[10] = (byte)(ipChecksum >> 8);
        packet[11] = (byte)(ipChecksum & 0xFF);

        packet[20] = 0x08;
        packet[21] = 0x00;
        packet[22] = 0x00;
        packet[23] = 0x00;
        packet[24] = 0x00;
        packet[25] = 0x01;
        packet[26] = 0x00;
        packet[27] = 0x01;

        Array.Copy(payload, 0, packet, 28, payload.Length);

        ushort icmpChecksum = ComputeChecksum(packet, 20, 8 + payload.Length);
        packet[22] = (byte)(icmpChecksum >> 8);
        packet[23] = (byte)(icmpChecksum & 0xFF);

        return packet;
    }

    internal static byte[] ParseIp(string ip)
    {
        var parts = ip.Split('.');
        return new byte[]
        {
            byte.Parse(parts[0]),
            byte.Parse(parts[1]),
            byte.Parse(parts[2]),
            byte.Parse(parts[3])
        };
    }

    internal static ushort ComputeChecksum(byte[] data, int offset, int length)
    {
        uint sum = 0;
        for (int i = 0; i < length; i += 2)
        {
            ushort word = (ushort)(data[offset + i] << 8);
            if (i + 1 < length)
                word |= data[offset + i + 1];
            sum += word;
        }
        while ((sum >> 16) > 0)
            sum = (sum & 0xFFFF) + (sum >> 16);
        return (ushort)(~sum & 0xFFFF);
    }
}
