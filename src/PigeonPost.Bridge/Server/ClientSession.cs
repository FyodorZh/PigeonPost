using System;
using Pontifex.Abstractions.Endpoints;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public sealed class ClientSession
{
    public IPv4 AdvertisedHostIpv4 { get; }
    public IAckRawBaseEndpoint Endpoint { get; }
    public DateTime ConnectedAt { get; }

    public ClientSession(IPv4 advertisedHostIpv4, IAckRawBaseEndpoint endpoint)
    {
        AdvertisedHostIpv4 = advertisedHostIpv4;
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        ConnectedAt = DateTime.UtcNow;
    }
}
