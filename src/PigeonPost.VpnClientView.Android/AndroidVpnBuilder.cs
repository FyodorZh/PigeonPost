using System;
using Android.Net;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

public static class AndroidVpnBuilder
{
    public static VpnService.Builder Configure(
        VpnService service,
        AndroidVpnConfiguration config)
    {
        var builder = new VpnService.Builder(service);
        builder.SetSession(config.SessionName);
        builder.SetMtu(config.Mtu);
        builder.AddAddress(config.ClientIp, config.PrefixLength);
        foreach (var dns in config.DnsServers)
            builder.AddDnsServer(dns);
        var parts = config.Route.Split('/');
        builder.AddRoute(parts[0], int.Parse(parts[1]));
        return builder;
    }
}
