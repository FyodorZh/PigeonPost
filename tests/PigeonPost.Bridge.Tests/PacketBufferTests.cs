using System;
using System.Threading;
using NUnit.Framework;

namespace PigeonPost.Bridge.Tests;

[TestFixture]
public class PacketBufferTests
{
    [Test]
    public void NewBuffer_HasZeroCount()
    {
        var buf = new PacketBuffer(1000);
        Assert.That(buf.Count, Is.EqualTo(0));
        Assert.That(buf.TotalBytes, Is.EqualTo(0));
    }

    [Test]
    public void Enqueue_SinglePacket_Succeeds()
    {
        var buf = new PacketBuffer(1000);
        bool ok = buf.TryEnqueue(new byte[100]);
        Assert.That(ok, Is.True);
        Assert.That(buf.Count, Is.EqualTo(1));
        Assert.That(buf.TotalBytes, Is.EqualTo(100));
    }

    [Test]
    public void Dequeue_ReturnsOldestFirst()
    {
        var buf = new PacketBuffer(1000);
        var p1 = new byte[] { 1, 2, 3 };
        var p2 = new byte[] { 4, 5 };
        buf.TryEnqueue(p1);
        buf.TryEnqueue(p2);

        Assert.That(buf.TryDequeue(out var r1), Is.True);
        Assert.That(r1, Is.SameAs(p1));
        Assert.That(buf.TryDequeue(out var r2), Is.True);
        Assert.That(r2, Is.SameAs(p2));
    }

    [Test]
    public void Dequeue_Empty_ReturnsFalse()
    {
        var buf = new PacketBuffer(1000);
        Assert.That(buf.TryDequeue(out _), Is.False);
    }

    [Test]
    public void Enqueue_WhenFull_DropsNewest()
    {
        var buf = new PacketBuffer(100);
        buf.TryEnqueue(new byte[60]);
        buf.TryEnqueue(new byte[50]);

        Assert.That(buf.Count, Is.EqualTo(1));
        Assert.That(buf.TotalBytes, Is.EqualTo(60));
    }

    [Test]
    public void Enqueue_ExactCapacity_Succeeds()
    {
        var buf = new PacketBuffer(100);
        Assert.That(buf.TryEnqueue(new byte[100]), Is.True);
        Assert.That(buf.TryEnqueue(new byte[1]), Is.False);
    }

    [Test]
    public void Enqueue_Null_Throws()
    {
        var buf = new PacketBuffer(100);
        Assert.That(() => buf.TryEnqueue(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void CapacityZero_DropsAll()
    {
        var buf = new PacketBuffer(0);
        Assert.That(buf.TryEnqueue(new byte[1]), Is.False);
    }

    [Test]
    public void Dequeue_ReducesTotalBytes()
    {
        var buf = new PacketBuffer(1000);
        buf.TryEnqueue(new byte[42]);
        buf.TryDequeue(out _);
        Assert.That(buf.TotalBytes, Is.EqualTo(0));
    }

    [Test]
    public void AfterDequeue_CanEnqueueAgain()
    {
        var buf = new PacketBuffer(100);
        buf.TryEnqueue(new byte[100]);
        buf.TryDequeue(out _);
        Assert.That(buf.TryEnqueue(new byte[100]), Is.True);
    }

    [Test]
    public void NegativeCapacity_Throws()
    {
        Assert.That(() => new PacketBuffer(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConcurrentEnqueueDequeue_NoCorruption()
    {
        var buf = new PacketBuffer(1_000_000);
        var done = new ManualResetEventSlim(false);
        long enqueued = 0, dequeued = 0;

        var producer = new Thread(() =>
        {
            for (int i = 0; i < 10_000; i++)
            {
                if (buf.TryEnqueue(new byte[10]))
                    Interlocked.Increment(ref enqueued);
            }
            done.Set();
        });

        var consumer = new Thread(() =>
        {
            while (!done.IsSet || buf.Count > 0)
            {
                if (buf.TryDequeue(out _))
                    Interlocked.Increment(ref dequeued);
                Thread.Yield();
            }
        });

        producer.Start();
        consumer.Start();
        producer.Join();
        consumer.Join();

        Assert.That(dequeued, Is.EqualTo(enqueued));
    }
}
