using System;
using System.Threading;
using PigeonPost.Tun;

namespace PigeonPost.Vpn;

public sealed class CountingTunDevice : ITunDevice
{
    private readonly ITunDevice _inner;
    private long _bytesSent;
    private long _bytesReceived;
    private long _packetsSent;
    private long _packetsReceived;

    public string Name => _inner.Name;
    public bool IsOpen => _inner.IsOpen;

    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);
    public long PacketsSent => Interlocked.Read(ref _packetsSent);
    public long PacketsReceived => Interlocked.Read(ref _packetsReceived);

    public CountingTunDevice(ITunDevice inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public int Read(byte[] buffer)
    {
        var bytesRead = _inner.Read(buffer);
        if (bytesRead > 0)
        {
            Interlocked.Add(ref _bytesReceived, bytesRead);
            Interlocked.Increment(ref _packetsReceived);
        }
        return bytesRead;
    }

    public void Write(byte[] buffer)
    {
        _inner.Write(buffer);
        Interlocked.Add(ref _bytesSent, buffer.Length);
        Interlocked.Increment(ref _packetsSent);
    }

    public void Close() => _inner.Close();
    public void Dispose() => _inner.Dispose();
}
