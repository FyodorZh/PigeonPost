using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class VpnProfileValidatorTests
{
    [Test]
    public void ValidUrl_Accepted()
    {
        var errors = VpnProfileValidator.Validate("tcp|203.0.113.10:9000/30", 15);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidUrl_DirectTransport_Accepted()
    {
        var errors = VpnProfileValidator.Validate("direct|ep_debug", 15);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void MalformedUrl_Rejected()
    {
        var errors = VpnProfileValidator.Validate("not-a-url", 15);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors[0], Does.Contain("Server URL"));
    }

    [Test]
    public void EmptyUrl_Rejected()
    {
        var errors = VpnProfileValidator.Validate("", 15);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors[0], Does.Contain("Server URL is required"));
    }

    [Test]
    public void OctetBelow11_Rejected()
    {
        var errors = VpnProfileValidator.Validate("tcp|203.0.113.10:9000/30", 3);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors, Does.Contain("Client IP last octet must be at least 11."));
    }

    [Test]
    public void OctetIs10_Rejected()
    {
        var errors = VpnProfileValidator.Validate("tcp|203.0.113.10:9000/30", 10);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors[0], Does.Contain("at least 11"));
    }

    [Test]
    public void OctetAbove254_Rejected()
    {
        var errors = VpnProfileValidator.Validate("tcp|203.0.113.10:9000/30", 255);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors[0], Does.Contain("at most 254"));
    }

    [Test]
    public void FullIpPreview_RendersCorrectly()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
        Assert.That(profile.FullClientIp, Is.EqualTo("10.0.10.15"));
    }

    [Test]
    public void FullIpPreview_UpperBoundary()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 254);
        Assert.That(profile.FullClientIp, Is.EqualTo("10.0.10.254"));
    }

    [Test]
    public void FullIpPreview_LowerBoundary()
    {
        var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 11);
        Assert.That(profile.FullClientIp, Is.EqualTo("10.0.10.11"));
    }

    [Test]
    public void MultipleErrors_ReturnedTogether()
    {
        var errors = VpnProfileValidator.Validate("bad", 0);
        Assert.That(errors.Count, Is.EqualTo(2));
    }
}
