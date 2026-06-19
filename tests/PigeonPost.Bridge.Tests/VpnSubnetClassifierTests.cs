using NUnit.Framework;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests;

[TestFixture]
public class VpnSubnetClassifierTests
{
    [Test]
    public void LinuxRangeStart_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 2);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void LinuxRangeEnd_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 10);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void MiddleOfLinuxRange_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 5);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
    }

    [Test]
    public void EndpointRangeStart_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 11);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void EndpointRangeEnd_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 254);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void MiddleOfEndpointRange_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 100);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
    }

    [Test]
    public void ServerTunIp_ClassifiedCorrectly()
    {
        var ip = new IPv4(10, 0, 10, 1);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
    }

    [Test]
    public void NetworkAddress_ClassifiedInSubnet()
    {
        var ip = new IPv4(10, 0, 10, 0);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void SubnetBroadcast_ClassifiedInSubnet()
    {
        var ip = new IPv4(10, 0, 10, 255);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.True);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
    }

    [Test]
    public void IpOutsideVpnSubnet_NotClassified()
    {
        var ip = new IPv4(10, 0, 20, 1);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsServerIp(ip), Is.False);
    }

    [Test]
    public void InternetIp_NotClassified()
    {
        var ip = new IPv4(1, 1, 1, 1);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsLinuxClient(ip), Is.False);
        Assert.That(VpnSubnetClassifier.IsEndpointClient(ip), Is.False);
    }

    [Test]
    public void PrivateNonVpnIp_NotClassified()
    {
        var ip = new IPv4(192, 168, 1, 1);
        Assert.That(VpnSubnetClassifier.IsInVpnSubnet(ip), Is.False);
    }
}
