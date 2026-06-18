using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class PlaceholderTests
{
    [Test]
    public void PlaceholderService_ReturnsExpectedStatus()
    {
        var service = new PlaceholderService();
        Assert.That(service.GetStatus(), Is.EqualTo("PigeonPost VPN Runtime"));
    }
}
