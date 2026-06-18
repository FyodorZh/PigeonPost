using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class SpeedHistoryBufferTests
{
    [Test]
    public void Capacity_Is30()
    {
        var buffer = new SpeedHistoryBuffer();
        Assert.That(buffer.Capacity, Is.EqualTo(30));
    }

    [Test]
    public void InitialHistory_ReturnsAllZeros()
    {
        var buffer = new SpeedHistoryBuffer();
        var sent = buffer.SentHistory;
        var received = buffer.ReceivedHistory;

        Assert.That(sent.Count, Is.EqualTo(30));
        Assert.That(received.Count, Is.EqualTo(30));
        Assert.That(sent.All(v => v == 0.0), Is.True);
        Assert.That(received.All(v => v == 0.0), Is.True);
    }

    [Test]
    public void AddSample_StoresValues()
    {
        var buffer = new SpeedHistoryBuffer();
        buffer.AddSample(1000, 2000);

        var sent = buffer.SentHistory;
        var received = buffer.ReceivedHistory;

        Assert.That(sent[29], Is.EqualTo(1000.0));
        Assert.That(received[29], Is.EqualTo(2000.0));
    }

    [Test]
    public void AddSample_MultipleSamples_ShiftsRing()
    {
        var buffer = new SpeedHistoryBuffer();

        for (var i = 0; i < 30; i++)
            buffer.AddSample(i, i * 10);

        var sent = buffer.SentHistory;
        var received = buffer.ReceivedHistory;

        Assert.That(sent[29], Is.EqualTo(29.0));
        Assert.That(received[29], Is.EqualTo(290.0));
        Assert.That(sent[0], Is.EqualTo(0.0));
        Assert.That(received[0], Is.EqualTo(0.0));
    }

    [Test]
    public void AddSample_Overflow_WrapsAround()
    {
        var buffer = new SpeedHistoryBuffer();

        for (var i = 0; i < 35; i++)
            buffer.AddSample(i, i * 10);

        var sent = buffer.SentHistory;
        var received = buffer.ReceivedHistory;

        Assert.That(sent[29], Is.EqualTo(34.0));
        Assert.That(sent[0], Is.EqualTo(5.0));
        Assert.That(received[29], Is.EqualTo(340.0));
        Assert.That(received[0], Is.EqualTo(50.0));
    }

    [Test]
    public void ThreadSafety_MultipleThreads_NoException()
    {
        var buffer = new SpeedHistoryBuffer();
        var passed = true;

        var thread1 = new Thread(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                buffer.AddSample(i, i * 10);
                Thread.Sleep(1);
            }
        });
        var thread2 = new Thread(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    _ = buffer.SentHistory;
                    _ = buffer.ReceivedHistory;
                }
                catch
                {
                    passed = false;
                }
                Thread.Sleep(1);
            }
        });

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        Assert.That(passed, Is.True);
    }

    [Test]
    public void SentHistory_And_ReceivedHistory_AreIndependent()
    {
        var buffer = new SpeedHistoryBuffer();
        buffer.AddSample(100, 200);
        buffer.AddSample(300, 400);

        var sent = buffer.SentHistory;
        var received = buffer.ReceivedHistory;

        Assert.That(sent[28], Is.EqualTo(100.0));
        Assert.That(sent[29], Is.EqualTo(300.0));
        Assert.That(received[28], Is.EqualTo(200.0));
        Assert.That(received[29], Is.EqualTo(400.0));

        buffer.AddSample(500, 600);

        sent = buffer.SentHistory;
        received = buffer.ReceivedHistory;

        Assert.That(sent[27], Is.EqualTo(100.0));
        Assert.That(sent[28], Is.EqualTo(300.0));
        Assert.That(sent[29], Is.EqualTo(500.0));
        Assert.That(received[29], Is.EqualTo(600.0));
    }
}
