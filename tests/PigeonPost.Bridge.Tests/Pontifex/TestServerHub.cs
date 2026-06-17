using System.Collections.Generic;
using Pontifex.Abstractions.Endpoints;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Bridge.Tests.Pontifex;

internal class TestServerHub : ServerHub
{
    private readonly object _lock = new();
    public List<byte[]> ReceivedPackets { get; } = new();
    public List<IAckRawBaseEndpoint> ActivatedEndpoints { get; } = new();

    public TestServerHub(ILogger logger) : base(logger)
    {
    }

    public override void ActivateSessionEndpoint(IPv4 hostIp, IAckRawBaseEndpoint endpoint)
    {
        lock (_lock) ActivatedEndpoints.Add(endpoint);
        base.ActivateSessionEndpoint(hostIp, endpoint);
    }

    public override void OnPacketFromClient(IPv4 hostIp, byte[] packet)
    {
        lock (_lock) ReceivedPackets.Add((byte[])packet.Clone());
        base.OnPacketFromClient(hostIp, packet);
    }
}
