using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using PigeonPost.Tun;

namespace PigeonPost.Vpn;

public sealed class ProbeTunDevice : ITunDevice
{
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<byte[]> _probeQueue = new();
    private readonly Dictionary<ushort, (DateTime sentTime, ushort id)> _sentProbes = new();
    private ushort _nextId;
    private ushort _nextSequence;
    private readonly Lock _idLock = new();

    public string Name => "probe";
    public bool IsOpen => true;

    public event Action<ushort, ushort, long>? ProbeReplyReceived;
    public event Action<ushort, ushort>? ProbeSent;

    public ushort GenerateNextId()
    {
        lock (_idLock) return _nextId++;
    }

    public ushort GenerateNextSequence()
    {
        lock (_idLock) return _nextSequence++;
    }

    public void EnqueueProbe(byte[] packet)
    {
        _probeQueue.Enqueue(packet);
    }

    public void RecordSentProbe(ushort id, ushort sequence)
    {
        lock (_sentProbes)
        {
            _sentProbes[sequence] = (DateTime.UtcNow, id);
        }
        ProbeSent?.Invoke(id, sequence);
    }

    public int Read(byte[] buffer)
    {
        if (_probeQueue.TryDequeue(out var packet))
        {
            Array.Copy(packet, buffer, packet.Length);
            return packet.Length;
        }

        Thread.Sleep(50);
        return 0;
    }

    public void Write(byte[] buffer)
    {
        if (!IcmpHelper.TryParseEchoReply(buffer, out var id, out var sequence))
            return;

        lock (_sentProbes)
        {
            if (_sentProbes.TryGetValue(sequence, out var entry) && entry.id == id)
            {
                _sentProbes.Remove(sequence);
                long rtt = (long)(DateTime.UtcNow - entry.sentTime).TotalMilliseconds;
                ProbeReplyReceived?.Invoke(id, sequence, rtt);
            }
        }
    }

    public void CheckTimeouts(TimeSpan timeout, Action<ushort> onTimeout)
    {
        var now = DateTime.UtcNow;
        lock (_sentProbes)
        {
            var timedOut = new List<ushort>();
            foreach (var kvp in _sentProbes)
            {
                if (now - kvp.Value.sentTime >= timeout)
                    timedOut.Add(kvp.Key);
            }

            foreach (var seq in timedOut)
            {
                _sentProbes.Remove(seq);
                onTimeout(seq);
            }
        }
    }

    public void ClearProbes()
    {
        while (_probeQueue.TryDequeue(out _))
        {
        }

        lock (_sentProbes)
        {
            _sentProbes.Clear();
        }
    }

    public void Close()
    {
    }

    public void Dispose()
    {
        ClearProbes();
    }
}
