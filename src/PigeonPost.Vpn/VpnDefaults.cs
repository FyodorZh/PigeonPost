namespace PigeonPost.Vpn;

public static class VpnDefaults
{
    public const string VpnSubnet = "10.0.10.0/24";
    public const string ServerTunIp = "10.0.10.1";
    public const string LinuxReservedRange = "10.0.10.2-10";
    public const string EndpointAllowedRange = "10.0.10.11-254";
    public const string DnsPrimary = "1.1.1.1";
    public const string DnsSecondary = "1.0.0.1";
}
