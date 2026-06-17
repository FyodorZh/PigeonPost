using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PigeonPost.Tun.Virtual;

public class VirtualNetwork
{
    private readonly Dictionary<IPv4, (VirtualTunDevice Device, Ipv4PacketHandler Processor)> _nodesByIp = new();
    private readonly Queue<(IPv4 FromIp, IPv4 ToIp, byte[] Data, Ipv4PacketHandler Processor)> _incoming = new();

    public ITunDevice CreateNode(string name, IPv4 ip, Ipv4PacketHandler processor)
    {
        lock (_nodesByIp)
        {
            if (_nodesByIp.ContainsKey(ip))
                throw new InvalidOperationException($"Node with IP {ip} already exists.");

            var device = new VirtualTunDevice(name);
            device.PacketWritten += (packet) =>
            {
                var parsed = TryParsePacket(packet);
                if (parsed == null)
                    throw new InvalidOperationException("Received malformed or invalid IPv4 packet.");

                var (srcIp, dstIp, data) = parsed.Value;
                
                if (dstIp != ip)
                    throw new InvalidOperationException($"Received packet for IP {dstIp} but node is {ip}.");
                
                lock (_nodesByIp)
                {
                    _incoming.Enqueue((srcIp, dstIp, data, processor));
                }
            };
            _nodesByIp[ip] = (device, processor);
            return device;
        }
    }

    public void SendFromTo(IPv4 fromIp, IPv4 toIp, byte[] data)
    {
        VirtualTunDevice fromDevice;
        lock (_nodesByIp)
        {
            if (!_nodesByIp.TryGetValue(fromIp, out var entry))
                throw new InvalidOperationException($"Source IP {fromIp} not found.");
            fromDevice = entry.Device;
            if (!_nodesByIp.TryGetValue(toIp, out entry))
                throw new InvalidOperationException($"Destination IP {toIp} not found.");
        }

        byte[] hash = MD5.HashData(data);
        byte[] payload = new byte[16 + data.Length];
        Buffer.BlockCopy(hash, 0, payload, 0, 16);
        Buffer.BlockCopy(data, 0, payload, 16, data.Length);

        byte[] ipPacket = BuildIpv4Packet(fromIp, toIp, payload);
        fromDevice.InjectPacket(ipPacket);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            (IPv4 FromIp, IPv4 ToIp, byte[] Data, Ipv4PacketHandler Processor)? item;
            lock (_nodesByIp)
            {
                if (_incoming.Count == 0)
                    item = null;
                else
                    item = _incoming.Dequeue();
            }

            if (item == null)
            {
                await Task.Delay(10, ct);
                continue;
            }

            item.Value.Processor(item.Value.FromIp, item.Value.ToIp, item.Value.Data);
        }
    }

    internal static byte[] BuildIpv4Packet(IPv4 srcIp, IPv4 dstIp, byte[] payload)
    {
        int totalLen = 20 + payload.Length;
        byte[] packet = new byte[totalLen];
        packet[0] = 0x45;
        packet[2] = (byte)(totalLen >> 8);
        packet[3] = (byte)totalLen;
        packet[8] = 64;
        srcIp.CopyTo(packet.AsSpan(12, 4));
        dstIp.CopyTo(packet.AsSpan(16, 4));
        Buffer.BlockCopy(payload, 0, packet, 20, payload.Length);
        return packet;
    }

    internal static (IPv4 SrcIp, IPv4 DstIp, byte[] Data)? TryParsePacket(byte[] packet)
    {
        if (packet == null! || packet.Length < 20)
            return null;

        int version = packet[0] >> 4;
        if (version != 4)
            return null;

        int ihl = packet[0] & 0x0F;
        if (ihl < 5)
            return null;

        int headerLen = ihl * 4;
        if (packet.Length < headerLen + 16)
            return null;

        var srcIp = new IPv4(packet, 12);
        var dstIp = new IPv4(packet, 16);

        int dataLen = packet.Length - headerLen - 16;
        if (dataLen < 0)
            return null;

        byte[] storedHash = new byte[16];
        Buffer.BlockCopy(packet, headerLen, storedHash, 0, 16);

        byte[] data = new byte[dataLen];
        Buffer.BlockCopy(packet, headerLen + 16, data, 0, dataLen);

        if (!storedHash.AsSpan().SequenceEqual(MD5.HashData(data).AsSpan()))
            return null;

        return (srcIp, dstIp, data);
    }
}
