using System;
using NUnit.Framework;
using PigeonPost.Tun;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class CountingTunDeviceTests
{
    [Test]
    public void Read_IncrementsBytesReceived()
    {
        var inner = new RecordingTunDevice();
        var counting = new CountingTunDevice(inner);
        var buffer = new byte[1500];

        int read = counting.Read(buffer);

        Assert.That(read, Is.EqualTo(100));
        Assert.That(counting.BytesReceived, Is.EqualTo(100));
        Assert.That(counting.PacketsReceived, Is.EqualTo(1));
    }

    [Test]
    public void Write_IncrementsBytesSent()
    {
        var inner = new RecordingTunDevice();
        var counting = new CountingTunDevice(inner);
        var buffer = new byte[500];

        counting.Write(buffer);

        Assert.That(counting.BytesSent, Is.EqualTo(500));
        Assert.That(counting.PacketsSent, Is.EqualTo(1));
    }

    [Test]
    public void MultipleReads_Accumulate()
    {
        var inner = new RecordingTunDevice();
        var counting = new CountingTunDevice(inner);
        var buffer = new byte[1500];

        counting.Read(buffer);
        counting.Read(buffer);
        counting.Read(buffer);

        Assert.That(counting.BytesReceived, Is.EqualTo(300));
        Assert.That(counting.PacketsReceived, Is.EqualTo(3));
    }

    [Test]
    public void MultipleWrites_Accumulate()
    {
        var inner = new RecordingTunDevice();
        var counting = new CountingTunDevice(inner);

        counting.Write(new byte[100]);
        counting.Write(new byte[200]);
        counting.Write(new byte[300]);

        Assert.That(counting.BytesSent, Is.EqualTo(600));
        Assert.That(counting.PacketsSent, Is.EqualTo(3));
    }

    [Test]
    public void DelegatesToInner()
    {
        var inner = new RecordingTunDevice();
        var counting = new CountingTunDevice(inner);

        Assert.That(counting.Name, Is.EqualTo("recording"));
        Assert.That(counting.IsOpen, Is.True);

        counting.Close();
        Assert.That(inner.WasClosed, Is.True);
    }

    private sealed class RecordingTunDevice : ITunDevice
    {
        public string Name => "recording";
        public bool IsOpen => true;
        public bool WasClosed { get; private set; }

        public int Read(byte[] buffer)
        {
            buffer[0] = 0x45;
            return 100;
        }

        public void Write(byte[] buffer)
        {
        }

        public void Close() => WasClosed = true;
        public void Dispose() => Close();
    }
}
