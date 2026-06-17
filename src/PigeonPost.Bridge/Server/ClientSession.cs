using System;
using Pontifex.Abstractions.Endpoints;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public sealed class ClientSession
{
    public ClientId ClientId { get; }
    public IPv4 AdvertisedHostIpv4 { get; }
    public IAckRawBaseEndpoint Endpoint { get; }
    public DateTime ConnectedAt { get; }

    public ClientSession(ClientId clientId, IPv4 advertisedHostIpv4, IAckRawBaseEndpoint endpoint)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        AdvertisedHostIpv4 = advertisedHostIpv4;
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        ConnectedAt = DateTime.UtcNow;
    }
}
