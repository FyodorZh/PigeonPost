namespace PigeonPost.Vpn;

public sealed class NullSocketProtector : ISocketProtector
{
    public bool ProtectSocket(System.Net.Sockets.Socket socket) => true;
}
