# Stage 5: Packet Buffer

## Goal

Implement a thread-safe packet buffer with configurable capacity and drop-newest overflow policy.
Lives in `PigeonPost.Bridge`.

## Prerequisites

- Stage 1 complete (project `PigeonPost.Bridge` builds).

## Technical Details

### Design

The buffer stores raw IP packet bytes (`byte[]`). When the total stored bytes exceeds the
configured capacity, newly arriving packets are **dropped** (drop-newest policy).

The buffer is consumed by the Pontifex sender thread: when a connection is established,
all buffered packets are drained and sent. New packets arriving while connected bypass
the buffer and go directly to the Pontifex endpoint.

This is a FIFO queue with a byte-size cap — not a ring buffer in the classic sense,
since packets are variable-sized IP datagrams.

### Interface

```csharp
namespace PigeonPost.Bridge;

public interface IPacketBuffer
{
    int Capacity { get; }       // max total bytes
    int Count { get; }          // number of packets currently stored
    int TotalBytes { get; }     // total bytes currently stored
    long DroppedPackets { get; } // cumulative dropped packet count

    /// <summary>
    /// Try to enqueue a packet. Returns false if the packet was dropped (buffer full).
    /// </summary>
    bool TryEnqueue(byte[] packet);

    /// <summary>
    /// Try to dequeue the oldest packet. Returns false if buffer is empty.
    /// </summary>
    bool TryDequeue(out byte[] packet);
}
```

### Thread Safety

All public methods acquire a lock. This is a low-contention lock since:
- Enqueue is called from the TUN reader thread.
- Dequeue is called from the Pontifex send side (same thread, or after connection).
- The `Count`, `TotalBytes`, `DroppedPackets` properties are for diagnostics only.

### Implementation

```csharp
namespace PigeonPost.Bridge;

public sealed class PacketBuffer : IPacketBuffer
{
    private readonly int _capacity;
    private readonly Queue<byte[]> _queue;
    private readonly object _lock = new();
    private int _totalBytes;
    private long _dropped;

    public int Capacity => _capacity;
    public int Count { get { lock (_lock) return _queue.Count; } }
    public int TotalBytes { get { lock (_lock) return _totalBytes; } }
    public long DroppedPackets { get { lock (_lock) return _dropped; } }

    public PacketBuffer(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _queue = new Queue<byte[]>();
    }

    public bool TryEnqueue(byte[] packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        lock (_lock)
        {
            if (_totalBytes + packet.Length > _capacity)
            {
                _dropped++;
                return false;
            }

            _queue.Enqueue(packet);
            _totalBytes += packet.Length;
            return true;
        }
    }

    public bool TryDequeue(out byte[] packet)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                packet = null!;
                return false;
            }

            packet = _queue.Dequeue();
            _totalBytes -= packet.Length;
            return true;
        }
    }
}
```

### Notes

1. **Capacity = 0**: if `TryEnqueue` is called when `_capacity == 0`, all packets are dropped (returns `false`). This is the degenerate case — the caller should set a reasonable capacity.
2. **Packet ownership**: the buffer does not copy the `byte[]`. The caller owns the buffer after `TryDequeue` returns it.
3. **Drop-newest**: the arriving packet is dropped, not the oldest one. This means buffer contents are delivered in order, and only the tail is lost under overflow.

## Tests (PigeonPost.Bridge.Tests)

```csharp
[TestFixture]
public class PacketBufferTests
{
    [Test]
    public void NewBuffer_HasZeroCount()
    {
        var buf = new PacketBuffer(1000);
        Assert.That(buf.Count, Is.EqualTo(0));
        Assert.That(buf.TotalBytes, Is.EqualTo(0));
        Assert.That(buf.DroppedPackets, Is.EqualTo(0));
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
        Assert.That(r1, Is.SameAs(p1)); // reference equality
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
        var buf = new PacketBuffer(100); // 100 byte capacity
        buf.TryEnqueue(new byte[60]);    // 60 bytes, ok
        buf.TryEnqueue(new byte[50]);    // would be 110, exceeds 100 → drop

        Assert.That(buf.Count, Is.EqualTo(1));
        Assert.That(buf.TotalBytes, Is.EqualTo(60));
        Assert.That(buf.DroppedPackets, Is.EqualTo(1));
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
        Assert.That(buf.DroppedPackets, Is.EqualTo(1));
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

    // Concurrency smoke test
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
```

## Success Criteria

1. All unit tests pass.
2. Thread safety verified by concurrent test.
3. Buffer respects capacity and FIFO order.
4. Drop-newest policy is correctly implemented.

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost.Bridge/IPacketBuffer.cs` | Create |
| `src/PigeonPost.Bridge/PacketBuffer.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/PacketBufferTests.cs` | Create |
