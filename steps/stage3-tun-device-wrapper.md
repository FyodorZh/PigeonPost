# Stage 3: TUN Device Wrapper

## Goal

Implement a public `ITunDevice` interface and `TunDevice` class that wraps the native
P/Invoke layer from Stage 2. Provide both synchronous and asynchronous I/O methods.
Write unit tests using a mockable interface; integration tests require a real TUN device.

## Prerequisites

- Stage 2 complete (native interop types in `PigeonPost.Tun`).

## Technical Details

### Interface Design

```csharp
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
```

### Design Decisions

1. **`Read`/`Write` take `byte[]`**: caller allocates buffer and passes it. `Read` returns the number of bytes read (or -1 on error, 0 on EOF which should not happen for TUN).
2. **`ReadAsync`/`WriteAsync`**: wrap the synchronous calls in `Task.Run` for use with async/await consumers. This is intentionally not a "truly async" implementation (no `epoll`/`io_uring`) — the dedicated thread model means we use blocking I/O on background threads. The async methods are convenience wrappers.
3. **`Open` must be called before I/O**: sets `IsOpen = true` and stores the file descriptor.
4. **`Close` is idempotent**: safe to call multiple times.
5. **`Dispose` calls `Close`**.

### Implementation Outline

```csharp
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
        if (fd < 0) throw new IOException($"Failed to open {path}: errno={Marshal.GetLastWin32Error()}");

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
        // We could assert n == buffer.Length, but partial writes shouldn't happen on TUN.
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
```

### Error Handling

- `open()` returns -1 → throw `IOException` with errno.
- `ioctl()` returns -1 → close fd, throw `IOException` with errno.
- `read()` returns -1 → throw `IOException` (EAGAIN should not happen on blocking fd).
- `write()` returns -1 → throw `IOException`.
- `read()` returns 0 → treat as no data (should not happen for TUN in blocking mode, but handle gracefully).
- `close()` best-effort; don't throw if it fails.

### Thread Safety

`TunDevice` is **not** thread-safe. It is the caller's responsibility to ensure that
`Read` and `Write` are called from one thread each, or externally synchronized.
This is documented on the interface.

## Tests

### Unit Tests (PigeonPost.Tun.Tests)

Since we can't easily create TUN devices in unit tests without root, we test:
1. The interface contract via a mock/fake.
2. Error paths by partially mocking (or using reflection to set bad fd).

#### `TunDeviceContractTests`

```csharp
[TestFixture]
public class TunDeviceContractTests
{
    [Test]
    public void NewDevice_IsNotOpen()
    {
        using var device = new TunDevice();
        Assert.That(device.IsOpen, Is.False);
        Assert.That(device.Name, Is.Empty);
    }

    [Test]
    public void Read_WhenNotOpen_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Read(new byte[1500]), Throws.InvalidOperationException);
    }

    [Test]
    public void Write_WhenNotOpen_ThrowsInvalidOperationException()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Write(new byte[1500]), Throws.InvalidOperationException);
    }

    [Test]
    public void Open_WhenAlreadyOpen_ThrowsInvalidOperationException()
    {
        // This test requires a real TUN device or a creative mock.
        // For now, we can test via subclass or skip if no TUN available.
    }

    [Test]
    public void Close_IsIdempotent()
    {
        var device = new TunDevice();
        device.Close();
        device.Close(); // should not throw
        Assert.That(device.IsOpen, Is.False);
    }

    [Test]
    public void Dispose_ClosesDevice()
    {
        var device = new TunDevice();
        device.Dispose();
        Assert.That(device.IsOpen, Is.False);
    }
}
```

#### `TunDeviceIntegrationTests`

Marked with `[Category("Integration")]` — requires a pre-created TUN device (e.g. `ip tuntap add dev tun99 mode tun`).

```csharp
[TestFixture]
[Category("Integration")]
public class TunDeviceIntegrationTests
{
    [Test]
    public void Open_ValidTunDevice_Succeeds()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Open("tun99"), Throws.Nothing);
        Assert.That(device.IsOpen, Is.True);
        Assert.That(device.Name, Is.EqualTo("tun99"));
    }

    [Test]
    public void Write_ToOneDevice_ReadFromAnother_ReceivesData()
    {
        // Requires two TUN devices with cross-routing configured.
        // This is a full integration test for later stages.
    }
}
```

### Mockable Interface

Because `ITunDevice` is an interface, tests in `PigeonPost.Bridge` can mock it:

```csharp
// In PigeonPost.Bridge.Tests:
internal class FakeTunDevice : ITunDevice
{
    private readonly Queue<byte[]> _incoming = new();
    public Queue<byte[]> Sent { get; } = new();
    public string Name => "fake";
    public bool IsOpen { get; private set; }

    public void Open(string name) => IsOpen = true;
    public int Read(byte[] buffer) { /* copy from _incoming */ }
    public void Write(byte[] buffer) => Sent.Enqueue(buffer);
    /* ... */
}
```

## Success Criteria

1. All unit tests pass without requiring a real TUN device.
2. Integration tests pass when a TUN device is available on the test machine.
3. `ITunDevice` interface is clean and mockable.
4. Error messages include errno for debugging.

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost.Tun/ITunDevice.cs` | Create |
| `src/PigeonPost.Tun/TunDevice.cs` | Create |
| `tests/PigeonPost.Tun.Tests/TunDeviceContractTests.cs` | Create |
| `tests/PigeonPost.Tun.Tests/TunDeviceIntegrationTests.cs` | Create |
| `tests/PigeonPost.Tun.Tests/FakeTunDevice.cs` | Create (or in Bridge.Tests later) |
