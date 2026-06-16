using System;
using System.Collections.Generic;

namespace PigeonPost.Bridge;

public sealed class PacketBuffer : IPacketBuffer
{
    private readonly int _capacity;
    private readonly Queue<byte[]> _queue;
    private readonly object _lock = new();
    private int _totalBytes;

    public int Capacity => _capacity;
    public int Count { get { lock (_lock) return _queue.Count; } }
    public int TotalBytes { get { lock (_lock) return _totalBytes; } }

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
