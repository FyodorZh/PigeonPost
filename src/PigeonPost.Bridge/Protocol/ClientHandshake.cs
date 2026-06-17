using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public sealed record ClientHandshake
{
    public IPv4 AdvertisedHostIpv4 { get; }

    public ClientHandshake(IPv4 advertisedHostIpv4)
    {
        AdvertisedHostIpv4 = advertisedHostIpv4;
    }
}
