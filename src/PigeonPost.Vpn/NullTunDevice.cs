using System;
using System.Threading;
using PigeonPost.Tun;

namespace PigeonPost.Vpn;

public sealed class NullTunDevice : ITunDevice
{
    public string Name => "null";
    public bool IsOpen => true;

    public int Read(byte[] buffer)
    {
        Thread.Sleep(100);
        return 0;
    }

    public void Write(byte[] buffer)
    {
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }
}
