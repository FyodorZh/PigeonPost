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
    public static extern nint read(int fd, byte[] buffer, nint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint write(int fd, byte[] buffer, nint count);
}
