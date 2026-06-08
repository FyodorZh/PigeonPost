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
