using System;

namespace PigeonPost.Vpn;

public interface IAndroidServiceBridge
{
    AndroidServiceState ServiceState { get; }
    event Action<AndroidServiceState>? ServiceStateChanged;
    void RequestVpnPermission();
    void StartVpnService();
    void StopVpnService();
}
