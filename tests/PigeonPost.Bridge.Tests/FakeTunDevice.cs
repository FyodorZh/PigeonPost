using System;
using System.Collections.Generic;
using System.Threading;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests;

internal class FakeTunDevice : ITunDevice
{
    private readonly Queue<byte[]> _incoming = new();
    private readonly object _lock = new();

    public string Name => "fake_tun";
    public bool IsOpen { get; private set; } = true;
    public List<byte[]> WrittenPackets { get; } = new();

    public void EnqueueIncoming(byte[] packet)
    {
        lock (_lock) _incoming.Enqueue(packet);
    }

    public void Open(string name)
    {
        IsOpen = true;
    }

    public int Read(byte[] buffer)
    {
        lock (_lock)
        {
            if (_incoming.Count == 0)
            {
                Thread.Sleep(50);
                return 0;
            }

            var packet = _incoming.Dequeue();
            Array.Copy(packet, buffer, packet.Length);
            return packet.Length;
        }
    }

    public void Write(byte[] buffer)
    {
        WrittenPackets.Add((byte[])buffer.Clone());
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Dispose()
    {
        Close();
    }
}
