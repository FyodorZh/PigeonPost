using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
[Platform(Include = "Linux")]
public class TunDeviceContractTests
{
    [Test]
    [Category("Integration")]
    public void Constructor_OpensDevice()
    {
        using var device = new TunDevice("tun99");
        Assert.That(device.IsOpen, Is.True);
        Assert.That(device.Name, Is.EqualTo("tun99"));
    }

    [Test]
    [Category("Integration")]
    public void Close_IsIdempotent()
    {
        var device = new TunDevice("tun99");
        device.Close();
        device.Close();
        Assert.That(device.IsOpen, Is.False);
    }

    [Test]
    [Category("Integration")]
    public void Dispose_ClosesDevice()
    {
        var device = new TunDevice("tun99");
        device.Dispose();
        Assert.That(device.IsOpen, Is.False);
    }

    [Test]
    [Category("Integration")]
    public void Read_AfterClose_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice("tun99");
        device.Close();
        Assert.That(() => device.Read(new byte[1500]), Throws.InvalidOperationException);
    }

    [Test]
    [Category("Integration")]
    public void Write_AfterClose_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice("tun99");
        device.Close();
        Assert.That(() => device.Write(new byte[1500]), Throws.InvalidOperationException);
    }
}
