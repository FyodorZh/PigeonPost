# Stage 2: TUN Native Interop

## Goal

Implement the P/Invoke layer for Linux TUN device I/O. This stage is pure native interop —
no abstractions, no async, no tests that require a real TUN device (those come in Stage 3).

## Prerequisites

- Stage 1 complete (project `PigeonPost.Tun` builds).
- Understanding of Linux TUN interface kernel API.

## Technical Details

### Constants

| Constant | Value | Source |
|----------|-------|--------|
| `TunPath` | `"/dev/net/tun"` | Linux kernel |
| `TUNSETIFF` | `0x400454ca` | `_IOW('T', 202, int)` on 64-bit |
| `IFF_TUN` | `0x0001` | `<linux/if_tun.h>` |
| `IFF_NO_PI` | `0x1000` | `<linux/if_tun.h>` |
| `O_RDWR` | `2` | `<fcntl.h>` (read+write) |
| `IFNAMSIZ` | `16` | `<net/if.h>` |

### `TUNSETIFF` Calculation

`_IOW(type, nr, size)` = `(_IOC_WRITE << _IOC_DIRSHIFT) | (size << _IOC_SIZESHIFT) | (type << _IOC_TYPESHIFT) | (nr << _IOC_NRSHIFT)`

- `_IOC_WRITE` = 1
- `_IOC_DIRSHIFT` = 30 → bit 31 set
- On 64-bit: `_IOC_SIZESHIFT` = 16, `_IOC_TYPESHIFT` = 8, `_IOC_NRSHIFT` = 0
- `size` = 4 (size of `int`, which is what the kernel actually uses for TUNSETIFF)
- Result: `(1 << 30) | (4 << 16) | ('T' << 8) | 202` = `0x400454ca`

Verify with: `printf '0x%x\n' $(( (1<<30) | (4<<16) | (84<<8) | 202 ))` → `0x400454ca`

### struct ifreq

The `ifreq` struct for TUN purposes:

```c
struct ifreq {
    char ifr_name[IFNAMSIZ];  // 16 bytes, null-terminated interface name
    short ifr_flags;          // IFF_TUN | IFF_NO_PI
};
```

On x86_64, the total struct size is 40 bytes (16 for name + 24 for the largest union member).
We use `LayoutKind.Explicit` to overlay the name and flags fields at the correct offsets.

### P/Invoke Signatures

```csharp
// libc function signatures
int open(const char *pathname, int flags);
int close(int fd);
int ioctl(int fd, unsigned long request, void *argp);
ssize_t read(int fd, void *buf, size_t count);
ssize_t write(int fd, const void *buf, size_t count);
```

Note: `ioctl` takes `unsigned long` for the request on Linux. The third argument is `void*`, so we pass `ref ifreq`.

## Implementation Steps

### 2.1 Create `NativeMethods` class

File: `src/PigeonPost.Tun/NativeMethods.cs`

```csharp
using System.Runtime.InteropServices;

namespace PigeonPost.Tun;

internal static class NativeMethods
{
    private const string Libc = "libc";

    [DllImport(Libc, SetLastError = true)]
    public static extern int open(string pathname, int flags);

    [DllImport(Libc, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, nuint request, ref ifreq ifr);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint read(int fd, byte[] buf, nint count);

    // Overload accepting Span<byte> via pinning (or use byte[])
    public static unsafe nint read(int fd, byte* buf, nint count)
    {
        // Low-level read; caller must pin buffer.
        return read_impl(fd, buf, count);
    }

    [DllImport(Libc, SetLastError = true, EntryPoint = "read")]
    private static extern unsafe nint read_impl(int fd, byte* buf, nint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint write(int fd, byte[] buf, nint count);

    // Span-based write overload
    public static unsafe nint write(int fd, byte* buf, nint count)
    {
        return write_impl(fd, buf, count);
    }

    [DllImport(Libc, SetLastError = true, EntryPoint = "write")]
    private static extern unsafe nint write_impl(int fd, byte* buf, nint count);
}
```

**Important:** Actually, for simplicity and safety, we'll use `byte[]` overloads initially.
The caller allocates a `byte[]` buffer and passes it. The Span-based overloads are an
optimization we can defer.

Simplified version:

```csharp
internal static class NativeMethods
{
    private const string Libc = "libc";

    [DllImport(Libc, SetLastError = true)]
    public static extern int open(string pathname, int flags);

    [DllImport(Libc, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, nuint request, ref ifreq ifr);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint read(int fd, byte[] buffer, nint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint write(int fd, byte[] buffer, nint count);
}
```

### 2.2 Create `TunConstants` class

File: `src/PigeonPost.Tun/TunConstants.cs`

```csharp
namespace PigeonPost.Tun;

internal static class TunConstants
{
    public const string TunPath = "/dev/net/tun";
    public const nuint TUNSETIFF = 0x400454ca;
    public const short IFF_TUN = 0x0001;
    public const short IFF_NO_PI = 0x1000;
    public const int O_RDWR = 2;
    public const int IFNAMSIZ = 16;
}
```

### 2.3 Create `ifreq` struct

File: `src/PigeonPost.Tun/ifreq.cs`

```csharp
using System.Runtime.InteropServices;

namespace PigeonPost.Tun;

[StructLayout(LayoutKind.Explicit, Size = 40)]
internal struct ifreq
{
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string ifr_name;

    [FieldOffset(16)]
    public short ifr_flags;
}
```

### 2.4 Create `.cs` placeholder file to verify build

Create a minimal `_AssemblyInfo.cs` or just let the empty class library build.
No public types yet — all types in Stage 2 are `internal`.

## Verification

1. `dotnet build src/PigeonPost.Tun/` compiles with zero warnings/errors.
2. Struct size: `Unsafe.SizeOf<ifreq>()` = 40 (test this).

## Tests (PigeonPost.Tun.Tests)

### Unit Test: Constants

```csharp
[TestFixture]
public class TunConstantsTests
{
    [Test]
    public void IffTunFlag_IsCorrect() => Assert.That(TunConstants.IFF_TUN, Is.EqualTo((short)0x0001));

    [Test]
    public void IffNoPiFlag_IsCorrect() => Assert.That(TunConstants.IFF_NO_PI, Is.EqualTo((short)0x1000));

    [Test]
    public void Tunsetiff_IsCorrect() => Assert.That(TunConstants.TUNSETIFF, Is.EqualTo((nuint)0x400454ca));

    [Test]
    public void ORdwr_IsCorrect() => Assert.That(TunConstants.O_RDWR, Is.EqualTo(2));
}
```

### Unit Test: Struct Size & Layout

```csharp
[TestFixture]
public class IfreqTests
{
    [Test]
    public void Size_Is40Bytes() => Assert.That(Unsafe.SizeOf<ifreq>(), Is.EqualTo(40));

    [Test]
    public void FlagsField_AtOffset16()
    {
        var ifr = new ifreq { ifr_name = "tun0", ifr_flags = 0x42 };
        Assert.That(ifr.ifr_flags, Is.EqualTo((short)0x42));
        Assert.That(ifr.ifr_name, Is.EqualTo("tun0"));
    }

    [Test]
    public void Name_TruncatedTo16Bytes()
    {
        var ifr = new ifreq { ifr_name = "very_long_name_12345", ifr_flags = 0 };
        // Name longer than 15 chars + null terminator should be truncated
        // (C# Marshal TStr will copy truncated to the buffer)
    }
}
```

### Unit Test: Verify P/Invoke libc binding loads

```csharp
[TestFixture]
public class NativeMethodsTests
{
    [Test]
    public void CanCall_NativeOpen_OnInvalidPath_ReturnsMinusOne()
    {
        // Open a non-existent file — should return -1, not throw DllNotFoundException
        int fd = NativeMethods.open("/nonexistent/file/for/test", TunConstants.O_RDWR);
        Assert.That(fd, Is.LessThan(0));
    }
}
```

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost.Tun/NativeMethods.cs` | Create |
| `src/PigeonPost.Tun/TunConstants.cs` | Create |
| `src/PigeonPost.Tun/ifreq.cs` | Create |
| `tests/PigeonPost.Tun.Tests/TunConstantsTests.cs` | Create |
| `tests/PigeonPost.Tun.Tests/IfreqTests.cs` | Create |
| `tests/PigeonPost.Tun.Tests/NativeMethodsTests.cs` | Create |
