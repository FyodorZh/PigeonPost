using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using PigeonPost.Tun;

namespace PigeonPost.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class TunDeviceIntegrationTests
{
    [Test]
    public void Open_PreConfiguredTunDevice_Succeeds()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        using var device = new TunDevice();
        Assert.That(() => device.Open("tunA"), Throws.Nothing);
        Assert.That(device.IsOpen, Is.True);
        Assert.That(device.Name, Is.EqualTo("tunA"));
    }

    [Test]
    public void Open_NonexistentTun_FailsWithIOException()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        using var device = new TunDevice();
        Assert.That(() => device.Open("nonexistent_tun_xyz"), Throws.InstanceOf<IOException>());
    }

    [Test]
    public void Open_ThenClose_ThenOpenAgain_Succeeds()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        using var device = new TunDevice();
        device.Open("tunA");
        device.Close();
        Assert.That(device.IsOpen, Is.False);
        device.Open("tunA");
        Assert.That(device.IsOpen, Is.True);
    }

    [Test]
    public void WriteToTun_CanReadFromPeerTun()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        using var tunA = new TunDevice();
        using var tunB = new TunDevice();
        tunA.Open("tunA");
        tunB.Open("tunB");

        byte[] packet = PacketBuilder.BuildIcmpPacket(
            srcIp: "10.99.0.1",
            dstIp: "10.99.0.2",
            payload: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        tunA.Write(packet);

        byte[] readBuf = new byte[65536];
        int bytesRead = ReadWithTimeout(tunB, readBuf, TimeSpan.FromSeconds(2));
        Assert.That(bytesRead, Is.GreaterThan(0));
        Assert.That(readBuf[0] >> 4, Is.EqualTo(4));
    }

    private static int ReadWithTimeout(TunDevice device, byte[] buffer, TimeSpan timeout)
    {
        int result = -1;
        var thread = new Thread(() =>
        {
            try { result = device.Read(buffer); }
            catch (IOException) { result = -1; }
        });
        thread.Start();
        if (!thread.Join(timeout))
            return -1;
        return result;
    }
}
