namespace PigeonPost.Vpn;

public sealed record AndroidVpnConfiguration(
    string ClientIp,
    int PrefixLength,
    string[] DnsServers,
    string Route,
    int Mtu,
    string SessionName)
{
    public static AndroidVpnConfiguration FromProfile(VpnProfile profile)
    {
        return new AndroidVpnConfiguration(
            ClientIp: profile.FullClientIp,
            PrefixLength: 24,
            DnsServers: [VpnDefaults.DnsPrimary, VpnDefaults.DnsSecondary],
            Route: "0.0.0.0/0",
            Mtu: 1500,
            SessionName: "PigeonPost VPN");
    }
}
