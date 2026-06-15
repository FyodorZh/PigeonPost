using System;

namespace PigeonPost.Bridge.Protocol;

public sealed record ClientHandshake
{
    public ClientId ClientId { get; }
    public uint AdvertisedHostIpv4 { get; }

    public ClientHandshake(ClientId clientId, uint advertisedHostIpv4)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        AdvertisedHostIpv4 = advertisedHostIpv4;
    }
}
