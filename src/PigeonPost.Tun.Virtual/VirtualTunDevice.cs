using System;
using System.Collections.Generic;

namespace PigeonPost.Tun.Virtual;

public sealed class VirtualTunDevice : ITunDevice
{
    private readonly string _name;
    private bool _isOpen;
    private readonly object _lock = new();
    private readonly Queue<byte[]> _queueToRead = new();

    string ITunDevice.Name => _name;
    bool ITunDevice.IsOpen => _isOpen;

    public VirtualTunDevice(string name)
    {
        _name = name;
        _isOpen = true;
    }

    int ITunDevice.Read(byte[] buffer)
    {
        lock (_lock)
        {
            if (!_isOpen)
                throw new InvalidOperationException("TUN device not open.");

            while (_queueToRead.Count == 0 && _isOpen)
                System.Threading.Monitor.Wait(_lock);

            if (!_isOpen)
            {
                return 0;
            }

            byte[] packet = _queueToRead.Dequeue();
            int len = Math.Min(packet.Length, buffer.Length);
            Buffer.BlockCopy(packet, 0, buffer, 0, len);
            return len;
        }
    }

    void ITunDevice.Write(byte[] buffer)
    {
        lock (_lock)
        {
            if (!_isOpen)
                throw new InvalidOperationException("TUN device not open.");

            var copy = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, copy, 0, buffer.Length);
            PacketWritten?.Invoke(copy);
        }
    }

    void ITunDevice.Close() => Shutdown();

    public void Dispose() => Shutdown();

    public void InjectPacket(byte[] packet)
    {
        lock (_lock)
        {
            byte[] copy = new byte[packet.Length];
            Buffer.BlockCopy(packet, 0, copy, 0, packet.Length);
            _queueToRead.Enqueue(copy);
            System.Threading.Monitor.Pulse(_lock);
        }
    }

    public event Action<byte[]>? PacketWritten;

    private void Shutdown()
    {
        lock (_lock)
        {
            if (!_isOpen)
                return;
            _isOpen = false;
            System.Threading.Monitor.PulseAll(_lock);
        }
    }
}
