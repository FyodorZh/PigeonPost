using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PigeonPost.Tun;

public sealed class TunDevice : ITunDevice
{
    private int _fd = -1;
    private string _name = string.Empty;

    public string Name => _name;
    public bool IsOpen => _fd >= 0;

    public void Open(string name)
    {
        if (IsOpen) throw new InvalidOperationException("TUN device already open.");

        string path = TunConstants.TunPath;
        int fd = NativeMethods.open(path, TunConstants.O_RDWR);
        if (fd < 0)
            throw new IOException($"Failed to open {path}: errno={Marshal.GetLastWin32Error()}");

        var ifr = new ifreq
        {
            ifr_name = name,
            ifr_flags = (short)(TunConstants.IFF_TUN | TunConstants.IFF_NO_PI)
        };

        int result = NativeMethods.ioctl(fd, TunConstants.TUNSETIFF, ref ifr);
        if (result < 0)
        {
            NativeMethods.close(fd);
            throw new IOException($"TUNSETIFF failed for '{name}': errno={Marshal.GetLastWin32Error()}");
        }

        _fd = fd;
        _name = name;
    }

    public void SetSendBufferSize(int size)
    {
        if (!IsOpen) throw new InvalidOperationException("TUN device not open.");
        int result = NativeMethods.ioctl(_fd, TunConstants.TUNSETSNDBUF, ref size);
        if (result < 0)
            throw new IOException($"TUNSETSNDBUF failed: errno={Marshal.GetLastWin32Error()}");
    }

    public int Read(byte[] buffer)
    {
        if (!IsOpen) throw new InvalidOperationException("TUN device not open.");
        nint n = NativeMethods.read(_fd, buffer, (nint)buffer.Length);
        if (n < 0) throw new IOException($"TUN read failed: errno={Marshal.GetLastWin32Error()}");
        return (int)n;
    }

    public void Write(byte[] buffer)
    {
        if (!IsOpen) throw new InvalidOperationException("TUN device not open.");
        nint n = NativeMethods.write(_fd, buffer, (nint)buffer.Length);
        if (n < 0) throw new IOException($"TUN write failed: errno={Marshal.GetLastWin32Error()}");
    }

    public async ValueTask<int> ReadAsync(byte[] buffer, CancellationToken ct = default)
    {
        return await Task.Run(() => Read(buffer), ct).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default)
    {
        await Task.Run(() => Write(buffer), ct).ConfigureAwait(false);
    }

    public void Close()
    {
        if (_fd >= 0)
        {
            NativeMethods.close(_fd);
            _fd = -1;
            _name = string.Empty;
        }
    }

    public void Dispose() => Close();
}
