using System.Collections.Generic;
using Pontifex;
using Pontifex.Abstractions.Endpoints;
using PigeonPost.Bridge;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests.Server;

internal sealed class FakeServerHub : IServerHub
{
    private readonly object _lock = new();
    private readonly Dictionary<IPv4, ClientSession> _sessions = new();

    public int ActiveSessionCount
    {
        get { lock (_lock) return _sessions.Count; }
    }

    public long DroppedNoRoute { get; private set; }
    public long DroppedInvalidSource { get; private set; }
    public long DroppedMalformedIpv4 { get; private set; }
    public long DroppedNonIpv4 { get; private set; }
    public long DroppedIsolationPolicy { get; set; }

    public List<byte[]> PacketsWrittenToTun { get; } = new();
    public Dictionary<string, List<byte[]>> PacketsSentToClient { get; } = new();

    public SessionRegistrationResult TryRegisterSession(ClientHandshake handshake, out ClientSession? session)
    {
        session = null;
        lock (_lock)
        {
            if (_sessions.ContainsKey(handshake.AdvertisedHostIpv4))
                return SessionRegistrationResult.RejectedDuplicateHostIp;

            var fakeEndpoint = new FakeEndpoint();
            session = new ClientSession(handshake.AdvertisedHostIpv4, fakeEndpoint);

            _sessions[handshake.AdvertisedHostIpv4] = session;

            string ipStr = handshake.AdvertisedHostIpv4.ToString();
            if (!PacketsSentToClient.ContainsKey(ipStr))
                PacketsSentToClient[ipStr] = new List<byte[]>();

            return SessionRegistrationResult.Accepted;
        }
    }

    public void RemoveSession(IPv4 hostIp)
    {
        lock (_lock)
        {
            _sessions.Remove(hostIp);
        }
    }

    public void OnPacketFromTun(byte[] packet)
    {
        if (!TryParseIpv4Addresses(packet, out uint source, out uint dest))
        {
            DroppedMalformedIpv4++;
            return;
        }

        if ((packet[0] >> 4) != 4)
        {
            DroppedNonIpv4++;
            return;
        }

        lock (_lock)
        {
            if (_sessions.TryGetValue((IPv4)dest, out var session))
            {
                string ipStr = session.AdvertisedHostIpv4.ToString();
                if (PacketsSentToClient.TryGetValue(ipStr, out var list))
                    list.Add(packet);
                return;
            }
        }

        DroppedNoRoute++;
    }

    public void OnPacketFromClient(IPv4 hostIp, byte[] packet)
    {
        if (!TryParseIpv4Addresses(packet, out uint source, out _))
        {
            DroppedMalformedIpv4++;
            return;
        }

        if ((packet[0] >> 4) != 4)
        {
            DroppedNonIpv4++;
            return;
        }

        lock (_lock)
        {
            if (_sessions.TryGetValue(hostIp, out var session))
            {
                if (source != session.AdvertisedHostIpv4.Value)
                {
                    DroppedInvalidSource++;
                    return;
                }

                PacketsWrittenToTun.Add(packet);
            }
        }
    }

    private static bool TryParseIpv4Addresses(byte[] packet, out uint source, out uint dest)
    {
        source = 0;
        dest = 0;

        if (packet == null || packet.Length < 20)
            return false;

        if ((packet[0] >> 4) != 4)
            return false;

        int ihl = packet[0] & 0x0F;
        if (ihl < 5)
            return false;

        source = ((uint)packet[12] << 24)
               | ((uint)packet[13] << 16)
               | ((uint)packet[14] << 8)
               | packet[15];

        dest = ((uint)packet[16] << 24)
             | ((uint)packet[17] << 16)
             | ((uint)packet[18] << 8)
             | packet[19];

        return true;
    }

    public void StopAll(StopReason reason)
    {
        lock (_lock)
        {
            _sessions.Clear();
        }
    }
}
