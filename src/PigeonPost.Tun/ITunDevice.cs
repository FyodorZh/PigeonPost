using System;

namespace PigeonPost.Tun;

public interface ITunDevice : IDisposable
{
    string Name { get; }
    bool IsOpen { get; }

    void Open(string name);
    int Read(byte[] buffer);
    void Write(byte[] buffer);
    void Close();
}
