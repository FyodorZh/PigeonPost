using System;

namespace PigeonPost.Vpn;

public interface IAndroidServiceBridge
{
    AndroidServiceState ServiceState { get; }
    bool IsVpnInterfaceEstablished { get; }
    AndroidVpnConfiguration? CurrentConfiguration { get; }
    event Action<AndroidServiceState>? ServiceStateChanged;
    void RequestVpnPermission();
    void StartVpnService();
    void StopVpnService();
    void SetRuntime(IVpnRuntime runtime);
}
