using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PigeonPost.Tun;

public static class TunIpv4AddressResolver
{
    public static uint ResolveIpv4Address(string tunName)
    {
        var iface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => string.Equals(ni.Name, tunName, StringComparison.Ordinal));

        if (iface == null)
            throw new InvalidOperationException($"TUN interface '{tunName}' not found in network interfaces.");

        var ipv4Addresses = iface.GetIPProperties().UnicastAddresses
            .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(ua => ua.Address)
            .ToList();

        if (ipv4Addresses.Count == 0)
            throw new InvalidOperationException(
                $"TUN interface '{tunName}' has no IPv4 address configured.");

        if (ipv4Addresses.Count > 1)
            throw new InvalidOperationException(
                $"TUN interface '{tunName}' has multiple IPv4 addresses configured. Only one is supported.");

        byte[] bytes = ipv4Addresses[0].GetAddressBytes();
        return ((uint)bytes[0] << 24)
             | ((uint)bytes[1] << 16)
             | ((uint)bytes[2] << 8)
             | bytes[3];
    }
}
