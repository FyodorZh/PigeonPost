using System.Net.Sockets;

namespace PigeonPost.Vpn;

public interface ISocketProtector
{
    bool ProtectSocket(Socket socket);
}
