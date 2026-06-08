using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PigeonPost.Tun;

namespace PigeonPost.Bridge.Tests;

internal class FakeTunDevice : ITunDevice
{
    private readonly Queue<byte[]> _incoming = new();
    public Queue<byte[]> Sent { get; } = new();

    public string Name => "fake";
    public bool IsOpen { get; private set; }

    public void Open(string name)
    {
        IsOpen = true;
    }

    public int Read(byte[] buffer)
    {
        while (_incoming.Count == 0)
        {
            Thread.Sleep(10);
        }

        byte[] data = _incoming.Dequeue();
        int count = Math.Min(data.Length, buffer.Length);
        Array.Copy(data, 0, buffer, 0, count);
        return count;
    }

    public void Write(byte[] buffer)
    {
        byte[] copy = new byte[buffer.Length];
        Array.Copy(buffer, copy, buffer.Length);
        Sent.Enqueue(copy);
    }

    public ValueTask<int> ReadAsync(byte[] buffer, CancellationToken ct = default)
    {
        return new ValueTask<int>(Task.Run(() => Read(buffer), ct));
    }

    public ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default)
    {
        return new ValueTask(Task.Run(() => Write(buffer), ct));
    }

    public void EnqueueIncoming(byte[] data)
    {
        _incoming.Enqueue(data);
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
