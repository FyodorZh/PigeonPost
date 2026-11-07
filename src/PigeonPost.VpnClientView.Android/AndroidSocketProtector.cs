using System;
using System.Net.Sockets;
using Android.Net;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

public sealed class AndroidSocketProtector : ISocketProtector
{
    private readonly VpnService _service;

    public AndroidSocketProtector(VpnService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    public bool ProtectSocket(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        return _service.Protect(socket.Handle.ToInt32());
    }
}
