using System;

namespace PigeonPost.Vpn;

public static class IcmpHelper
{
    public static byte[] CreateEchoRequest(uint sourceIp, ushort id, ushort sequence, byte[]? payload = null)
    {
        byte[] icmpPayload = payload ?? new byte[48];
        int ipHeaderLen = 20;
        int totalLen = ipHeaderLen + 8 + icmpPayload.Length;

        byte[] packet = new byte[totalLen];

        packet[0] = 0x45;
        packet[1] = 0;
        packet[2] = (byte)(totalLen >> 8);
        packet[3] = (byte)totalLen;
        packet[4] = 0;
        packet[5] = 0;
        packet[6] = 0;
        packet[7] = 0;
        packet[8] = 64;
        packet[9] = 1;

        packet[12] = (byte)(sourceIp >> 24);
        packet[13] = (byte)(sourceIp >> 16);
        packet[14] = (byte)(sourceIp >> 8);
        packet[15] = (byte)sourceIp;

        packet[16] = 1;
        packet[17] = 1;
        packet[18] = 1;
        packet[19] = 1;

        ushort ipChecksum = ComputeChecksum(packet, 0, ipHeaderLen);
        packet[10] = (byte)(ipChecksum >> 8);
        packet[11] = (byte)ipChecksum;

        packet[20] = 8;
        packet[21] = 0;
        packet[24] = (byte)(id >> 8);
        packet[25] = (byte)id;
        packet[26] = (byte)(sequence >> 8);
        packet[27] = (byte)sequence;

        Array.Copy(icmpPayload, 0, packet, 28, icmpPayload.Length);

        ushort icmpChecksum = ComputeChecksum(packet, 20, totalLen - 20);
        packet[22] = (byte)(icmpChecksum >> 8);
        packet[23] = (byte)icmpChecksum;

        return packet;
    }

    public static bool TryParseEchoReply(byte[] packet, out ushort id, out ushort sequence)
    {
        id = 0;
        sequence = 0;

        if (packet.Length < 28)
            return false;

        if ((packet[0] & 0xF0) != 0x40)
            return false;

        int ipHeaderLen = (packet[0] & 0x0F) * 4;
        if (ipHeaderLen < 20)
            return false;

        if (packet[9] != 1)
            return false;

        int totalLength = (packet[2] << 8) | packet[3];
        if (packet.Length < totalLength)
            return false;
        if (totalLength < ipHeaderLen + 8)
            return false;

        if (packet[ipHeaderLen] != 0)
            return false;

        id = (ushort)((packet[ipHeaderLen + 4] << 8) | packet[ipHeaderLen + 5]);
        sequence = (ushort)((packet[ipHeaderLen + 6] << 8) | packet[ipHeaderLen + 7]);

        return true;
    }

    public static ushort ComputeChecksum(byte[] buffer, int offset, int length)
    {
        long sum = 0;
        int i = offset;
        int end = offset + length;

        while (i < end - 1)
        {
            sum += (buffer[i] << 8) | buffer[i + 1];
            i += 2;
        }

        if (i < end)
            sum += buffer[i] << 8;

        while ((sum >> 16) != 0)
            sum = (sum & 0xFFFF) + (sum >> 16);

        return (ushort)(~sum & 0xFFFF);
    }
}
