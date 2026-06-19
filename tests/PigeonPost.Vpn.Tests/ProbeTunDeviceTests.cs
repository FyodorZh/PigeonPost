using System;
using System.Threading;
using NUnit.Framework;
using PigeonPost.Tun;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class ProbeTunDeviceTests
{
    private static readonly uint TestSourceIp = 0x0A000A0F;

    [Test]
    public void Read_ReturnsEnqueuedPacket()
    {
        var tun = new ProbeTunDevice();
        byte[] probe = [1, 2, 3, 4];
        tun.EnqueueProbe(probe);

        byte[] buffer = new byte[1024];
        int read = tun.Read(buffer);

        Assert.That(read, Is.EqualTo(4));
        Assert.That(buffer[0], Is.EqualTo(1));
        Assert.That(buffer[1], Is.EqualTo(2));
        Assert.That(buffer[2], Is.EqualTo(3));
        Assert.That(buffer[3], Is.EqualTo(4));
    }

    [Test]
    public void Read_ReturnsMultiplePacketsInOrder()
    {
        var tun = new ProbeTunDevice();
        tun.EnqueueProbe([10]);
        tun.EnqueueProbe([20]);
        tun.EnqueueProbe([30]);

        byte[] buffer = new byte[1024];
        Assert.That(tun.Read(buffer), Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(10));
        Assert.That(tun.Read(buffer), Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(20));
        Assert.That(tun.Read(buffer), Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(30));
    }

    [Test]
    public void Read_Empty_ReturnsZero()
    {
        var tun = new ProbeTunDevice();
        byte[] buffer = new byte[1024];
        int read = tun.Read(buffer);

        Assert.That(read, Is.EqualTo(0));
    }

    [Test]
    public void Write_WithEchoReply_FiresEvent()
    {
        var tun = new ProbeTunDevice();
        ushort expectedId = 0x42;
        ushort expectedSeq = 0x99;

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, expectedId, expectedSeq);
        tun.EnqueueProbe(request);
        tun.RecordSentProbe(expectedId, expectedSeq);

        byte[] readBuf = new byte[1024];
        tun.Read(readBuf);

        byte[] reply = BuildEchoReply(request);

        ushort? receivedId = null;
        ushort? receivedSeq = null;
        long? receivedRtt = null;
        tun.ProbeReplyReceived += (id, seq, rtt) =>
        {
            receivedId = id;
            receivedSeq = seq;
            receivedRtt = rtt;
        };

        tun.Write(reply);

        Assert.That(receivedId, Is.EqualTo(expectedId));
        Assert.That(receivedSeq, Is.EqualTo(expectedSeq));
        Assert.That(receivedRtt, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Write_WithEchoReply_RemovesFromSentProbes()
    {
        var tun = new ProbeTunDevice();

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, 1, 1);
        tun.EnqueueProbe(request);
        tun.RecordSentProbe(1, 1);

        byte[] readBuf = new byte[1024];
        tun.Read(readBuf);

        byte[] reply = BuildEchoReply(request);

        int eventCount = 0;
        tun.ProbeReplyReceived += (_, _, _) => eventCount++;

        tun.Write(reply);
        Assert.That(eventCount, Is.EqualTo(1));

        tun.Write(reply);
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void Write_NonIcmp_DoesNotFireEvent()
    {
        var tun = new ProbeTunDevice();
        int eventFired = 0;
        tun.ProbeReplyReceived += (_, _, _) => eventFired++;

        byte[] tcpPacket = new byte[40];
        tcpPacket[0] = 0x45;
        tcpPacket[9] = 6;
        tcpPacket[2] = 0;
        tcpPacket[3] = 40;

        tun.Write(tcpPacket);

        Assert.That(eventFired, Is.EqualTo(0));
    }

    [Test]
    public void Write_NonIpv4_DoesNotFireEvent()
    {
        var tun = new ProbeTunDevice();
        int eventFired = 0;
        tun.ProbeReplyReceived += (_, _, _) => eventFired++;

        byte[] garbage = [0, 1, 2, 3];
        tun.Write(garbage);

        Assert.That(eventFired, Is.EqualTo(0));
    }

    [Test]
    public void Write_WrongSequence_DoesNotFireEvent()
    {
        var tun = new ProbeTunDevice();

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, 1, 1);
        tun.EnqueueProbe(request);
        tun.RecordSentProbe(1, 1);

        byte[] readBuf = new byte[1024];
        tun.Read(readBuf);

        var wrongRequest = IcmpHelper.CreateEchoRequest(TestSourceIp, 2, 99);
        byte[] reply = BuildEchoReply(wrongRequest);

        int eventCount = 0;
        tun.ProbeReplyReceived += (_, _, _) => eventCount++;

        tun.Write(reply);

        Assert.That(eventCount, Is.EqualTo(0));
    }

    [Test]
    public void ClearProbes_EmptiesQueueAndSentProbes()
    {
        var tun = new ProbeTunDevice();
        tun.EnqueueProbe([1]);
        tun.EnqueueProbe([2]);
        tun.RecordSentProbe(1, 1);

        tun.ClearProbes();

        byte[] buffer = new byte[1024];
        int read = tun.Read(buffer);
        Assert.That(read, Is.EqualTo(0));

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, 1, 1);
        byte[] reply = BuildEchoReply(request);
        int eventCount = 0;
        tun.ProbeReplyReceived += (_, _, _) => eventCount++;
        tun.Write(reply);
        Assert.That(eventCount, Is.EqualTo(0));
    }

    [Test]
    public void CheckTimeouts_RemovesExpiredProbes()
    {
        var tun = new ProbeTunDevice();

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, 1, 1);
        tun.EnqueueProbe(request);
        tun.RecordSentProbe(1, 1);

        int timedOut = 0;
        tun.CheckTimeouts(TimeSpan.Zero, _ => timedOut++);

        Assert.That(timedOut, Is.EqualTo(1));
    }

    [Test]
    public void CheckTimeouts_DoesNotRemoveNonExpired()
    {
        var tun = new ProbeTunDevice();

        var request = IcmpHelper.CreateEchoRequest(TestSourceIp, 1, 1);
        tun.EnqueueProbe(request);
        tun.RecordSentProbe(1, 1);

        int timedOut = 0;
        tun.CheckTimeouts(TimeSpan.FromHours(1), _ => timedOut++);

        Assert.That(timedOut, Is.EqualTo(0));
    }

    [Test]
    public void GenerateNextId_Increments()
    {
        var tun = new ProbeTunDevice();
        Assert.That(tun.GenerateNextId(), Is.EqualTo(0));
        Assert.That(tun.GenerateNextId(), Is.EqualTo(1));
        Assert.That(tun.GenerateNextId(), Is.EqualTo(2));
    }

    [Test]
    public void GenerateNextSequence_Increments()
    {
        var tun = new ProbeTunDevice();
        Assert.That(tun.GenerateNextSequence(), Is.EqualTo(0));
        Assert.That(tun.GenerateNextSequence(), Is.EqualTo(1));
        Assert.That(tun.GenerateNextSequence(), Is.EqualTo(2));
    }

    [Test]
    public void Name_IsProbe()
    {
        var tun = new ProbeTunDevice();
        Assert.That(tun.Name, Is.EqualTo("probe"));
    }

    [Test]
    public void IsOpen_IsTrue()
    {
        var tun = new ProbeTunDevice();
        Assert.That(tun.IsOpen, Is.True);
    }

    [Test]
    public void ProbeSent_EventFires()
    {
        var tun = new ProbeTunDevice();
        ushort? receivedId = null;
        ushort? receivedSeq = null;
        tun.ProbeSent += (id, seq) =>
        {
            receivedId = id;
            receivedSeq = seq;
        };

        tun.RecordSentProbe(7, 42);

        Assert.That(receivedId, Is.EqualTo(7));
        Assert.That(receivedSeq, Is.EqualTo(42));
    }

    private static byte[] BuildEchoReply(byte[] request)
    {
        int ipHeaderLen = (request[0] & 0x0F) * 4;
        int totalLen = (request[2] << 8) | request[3];

        byte[] reply = new byte[request.Length];
        Array.Copy(request, reply, request.Length);
        reply[20] = 0;

        ushort icmpChecksum = IcmpHelper.ComputeChecksum(reply, ipHeaderLen, totalLen - ipHeaderLen);
        reply[ipHeaderLen + 2] = (byte)(icmpChecksum >> 8);
        reply[ipHeaderLen + 3] = (byte)icmpChecksum;

        return reply;
    }
}
