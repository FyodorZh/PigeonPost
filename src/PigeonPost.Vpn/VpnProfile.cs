namespace PigeonPost.Vpn;

public sealed record VpnProfile(string ServerUrl, byte ClientIpLastOctet)
{
    public string FullClientIp => $"10.0.10.{ClientIpLastOctet}";
}
