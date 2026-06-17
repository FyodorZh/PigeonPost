using System;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public sealed record ClientHandshake
{
    public ClientId ClientId { get; }
    public IPv4 AdvertisedHostIpv4 { get; }

    public ClientHandshake(ClientId clientId, IPv4 advertisedHostIpv4)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        AdvertisedHostIpv4 = advertisedHostIpv4;
    }
}
