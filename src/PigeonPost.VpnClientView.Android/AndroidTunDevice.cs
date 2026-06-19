using System;
using Android.OS;
using Java.IO;
using PigeonPost.Tun;

namespace PigeonPost.VpnClientView.Android;

public sealed class AndroidTunDevice : ITunDevice
{
    private readonly ParcelFileDescriptor _pfd;
    private readonly FileInputStream _inputStream;
    private readonly FileOutputStream _outputStream;
    private bool _closed;

    public string Name => "tun-android";

    public bool IsOpen
    {
        get
        {
            if (_closed)
                return false;

            var fd = _pfd.FileDescriptor;
            return fd is not null && fd.Valid();
        }
    }

    public AndroidTunDevice(ParcelFileDescriptor pfd)
    {
        ArgumentNullException.ThrowIfNull(pfd);
        _pfd = pfd;
        _inputStream = new FileInputStream(pfd.FileDescriptor);
        _outputStream = new FileOutputStream(pfd.FileDescriptor);
    }

    public int Read(byte[] buffer)
    {
        return _inputStream.Read(buffer, 0, buffer.Length);
    }

    public void Write(byte[] buffer)
    {
        _outputStream.Write(buffer, 0, buffer.Length);
        _outputStream.Flush();
    }

    public void Close()
    {
        if (_closed)
            return;

        _closed = true;

        try { _inputStream.Close(); } catch { }
        try { _outputStream.Close(); } catch { }
        try { _pfd.Close(); } catch { }
        try { _pfd.Dispose(); } catch { }
    }

    public void Dispose() => Close();
}
