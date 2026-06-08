using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
public class TunDeviceContractTests
{
    [Test]
    public void NewDevice_IsNotOpen()
    {
        using var device = new TunDevice();
        Assert.That(device.IsOpen, Is.False);
        Assert.That(device.Name, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Read_WhenNotOpen_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Read(new byte[1500]), Throws.InvalidOperationException);
    }

    [Test]
    public void Write_WhenNotOpen_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Write(new byte[1500]), Throws.InvalidOperationException);
    }

    [Test]
    public void Close_IsIdempotent()
    {
        var device = new TunDevice();
        device.Close();
        device.Close();
        Assert.That(device.IsOpen, Is.False);
    }

    [Test]
    public void Dispose_ClosesDevice()
    {
        var device = new TunDevice();
        device.Dispose();
        Assert.That(device.IsOpen, Is.False);
    }
}
