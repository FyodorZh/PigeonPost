namespace PigeonPost.Bridge;

public static class VpnSubnetClassifier
{
    private const uint VpnSubnetBase = 0x0A000A00;
    private const uint VpnSubnetMask = 0xFFFFFF00;
    private const uint ServerTunIp = 0x0A000A01;
    private const uint LinuxRangeStart = 0x0A000A02;
    private const uint LinuxRangeEnd = 0x0A000A0A;
    private const uint EndpointRangeStart = 0x0A000A0B;
    private const uint EndpointRangeEnd = 0x0A000AFE;

    public static bool IsInVpnSubnet(uint ip) => (ip & VpnSubnetMask) == VpnSubnetBase;

    public static bool IsServerIp(uint ip) => ip == ServerTunIp;

    public static bool IsLinuxClient(uint ip) => ip >= LinuxRangeStart && ip <= LinuxRangeEnd;

    public static bool IsEndpointClient(uint ip) => ip >= EndpointRangeStart && ip <= EndpointRangeEnd;
}
