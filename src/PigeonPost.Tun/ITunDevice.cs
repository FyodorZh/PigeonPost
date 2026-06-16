using System;

namespace PigeonPost.Tun;

public interface ITunDevice : IDisposable
{
    string Name { get; }
    bool IsOpen { get; }

    int Read(byte[] buffer);
    void Write(byte[] buffer);
    void Close();
}
