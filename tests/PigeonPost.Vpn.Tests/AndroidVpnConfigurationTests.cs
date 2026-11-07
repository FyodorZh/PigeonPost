using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class AndroidVpnConfigurationTests
{
    [Test]
    public void FromProfile_SetsClientIp()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.ClientIp, Is.EqualTo("10.0.10.15"));
    }

    [Test]
    public void FromProfile_SetsPrefixLength()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.PrefixLength, Is.EqualTo(24));
    }

    [Test]
    public void FromProfile_SetsDnsServers()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.DnsServers, Has.Length.EqualTo(2));
        Assert.That(config.DnsServers[0], Is.EqualTo(VpnDefaults.DnsPrimary));
        Assert.That(config.DnsServers[1], Is.EqualTo(VpnDefaults.DnsSecondary));
    }

    [Test]
    public void FromProfile_SetsFullRoute()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.Route, Is.EqualTo("0.0.0.0/0"));
    }

    [Test]
    public void FromProfile_SetsMtu()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.Mtu, Is.EqualTo(1500));
    }

    [Test]
    public void FromProfile_SetsSessionName()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.SessionName, Is.EqualTo("PigeonPost VPN"));
    }

    [Test]
    public void FromProfile_DifferentOctet_ChangesClientIp()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 42);
        var config = AndroidVpnConfiguration.FromProfile(profile);

        Assert.That(config.ClientIp, Is.EqualTo("10.0.10.42"));
    }

    [Test]
    public void Constructor_StoresValues()
    {
        var config = new AndroidVpnConfiguration(
            ClientIp: "10.0.10.99",
            PrefixLength: 24,
            DnsServers: ["8.8.8.8"],
            Route: "0.0.0.0/0",
            Mtu: 1400,
            SessionName: "Test");

        Assert.Multiple(() =>
        {
            Assert.That(config.ClientIp, Is.EqualTo("10.0.10.99"));
            Assert.That(config.PrefixLength, Is.EqualTo(24));
            Assert.That(config.DnsServers, Is.EquivalentTo(new[] { "8.8.8.8" }));
            Assert.That(config.Route, Is.EqualTo("0.0.0.0/0"));
            Assert.That(config.Mtu, Is.EqualTo(1400));
            Assert.That(config.SessionName, Is.EqualTo("Test"));
        });
    }
}
