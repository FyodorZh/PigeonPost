using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class VpnDefaultsTests
{
    [Test]
    public void DnsPrimary_IsCloudflare()
    {
        Assert.That(VpnDefaults.DnsPrimary, Is.EqualTo("1.1.1.1"));
    }

    [Test]
    public void DnsSecondary_IsCloudflareSecondary()
    {
        Assert.That(VpnDefaults.DnsSecondary, Is.EqualTo("1.0.0.1"));
    }

    [Test]
    public void VpnSubnet_IsCorrect()
    {
        Assert.That(VpnDefaults.VpnSubnet, Is.EqualTo("10.0.10.0/24"));
    }

    [Test]
    public void ServerTunIp_IsCorrect()
    {
        Assert.That(VpnDefaults.ServerTunIp, Is.EqualTo("10.0.10.1"));
    }

    [Test]
    public void EndpointAllowedRange_IsCorrect()
    {
        Assert.That(VpnDefaults.EndpointAllowedRange, Is.EqualTo("10.0.10.11-254"));
    }
}
