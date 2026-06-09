namespace PigeonPost.Tun;

internal static class TunConstants
{
    public const string TunPath = "/dev/net/tun";
    public const nuint TUNSETIFF = 0x400454ca;
    public const nuint TUNSETSNDBUF = 0x400454dc;
    public const short IFF_TUN = 0x0001;
    public const short IFF_NO_PI = 0x1000;
    public const int O_RDWR = 2;
    public const int IFNAMSIZ = 16;
}
