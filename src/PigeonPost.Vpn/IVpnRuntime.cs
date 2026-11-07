using System;
using System.Threading;
using System.Threading.Tasks;

namespace PigeonPost.Vpn;

public interface IVpnRuntime
{
    ConnectionState State { get; }
    VpnSessionSnapshot CurrentSession { get; }
    bool IsReconnecting { get; }

    event Action<VpnSessionSnapshot>? SessionUpdated;
    event Action<VpnLogEntry>? LogEmitted;

    Task ConnectAsync(VpnProfile profile, CancellationToken ct);
    Task DisconnectAsync();
}
