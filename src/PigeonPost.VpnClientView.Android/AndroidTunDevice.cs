using System;
using System.IO;
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
        if (_closed)
            throw new ObjectDisposedException(Name);
        try
        {
            return _inputStream.Read(buffer, 0, buffer.Length);
        }
        catch (Java.IO.IOException ex) when (_closed)
        {
            throw new ObjectDisposedException(Name, ex);
        }
        catch (Java.IO.IOException ex)
        {
            throw new System.IO.IOException("TUN read failed", ex);
        }
    }

    public void Write(byte[] buffer)
    {
        if (_closed)
            throw new ObjectDisposedException(Name);
        try
        {
            _outputStream.Write(buffer, 0, buffer.Length);
            _outputStream.Flush();
        }
        catch (Java.IO.IOException ex) when (_closed)
        {
            throw new ObjectDisposedException(Name, ex);
        }
        catch (Java.IO.IOException ex)
        {
            throw new System.IO.IOException("TUN write failed", ex);
        }
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
