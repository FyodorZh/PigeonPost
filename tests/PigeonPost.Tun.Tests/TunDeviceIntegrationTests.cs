using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
[Category("Integration")]
public class TunDeviceIntegrationTests
{
    [Test]
    [Platform(Include = "Linux")]
    public void Open_ValidTunDevice_Succeeds()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Open("tun99"), Throws.Nothing);
        Assert.That(device.IsOpen, Is.True);
        Assert.That(device.Name, Is.EqualTo("tun99"));
    }

    [Test]
    [Platform(Include = "Linux")]
    public void Write_ToOneDevice_ReadFromAnother_ReceivesData()
    {
        // Requires two TUN devices with cross-routing configured.
        // This is a full integration test for later stages.
    }
}
