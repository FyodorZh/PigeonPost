using System;
using System.Threading;
using System.Threading.Tasks;

namespace PigeonPost.Tun;

public interface ITunDevice : IDisposable
{
    string Name { get; }
    bool IsOpen { get; }

    void Open(string name);
    int Read(byte[] buffer);
    ValueTask<int> ReadAsync(byte[] buffer, CancellationToken ct = default);
    void Write(byte[] buffer);
    ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default);
    void Close();
}
