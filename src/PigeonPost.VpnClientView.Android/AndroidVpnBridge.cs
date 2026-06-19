using System;
using Android.App;
using Android.Content;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

public sealed class AndroidVpnBridge : IAndroidServiceBridge
{
    private readonly WeakReference<Activity> _activityRef;
    private AndroidServiceState _serviceState;
    private VpnProfile? _profile;
    private bool _vpnInterfaceEstablished;
    private AndroidVpnConfiguration? _currentConfiguration;
    private IVpnRuntime? _runtime;

    public const int VpnPermissionRequestCode = 9001;

    public AndroidServiceState ServiceState
    {
        get => _serviceState;
        private set
        {
            _serviceState = value;
            ServiceStateChanged?.Invoke(value);
        }
    }

    public bool IsVpnInterfaceEstablished => _vpnInterfaceEstablished;

    public AndroidVpnConfiguration? CurrentConfiguration => _currentConfiguration;

    public event Action<AndroidServiceState>? ServiceStateChanged;

    public AndroidVpnBridge(Activity activity)
    {
        _activityRef = new WeakReference<Activity>(activity);
        PigeonPostVpnService.OnVpnRevoked += OnVpnRevoked;
        PigeonPostVpnService.OnVpnInterfaceResult += OnVpnInterfaceResult;
    }

    public void RequestVpnPermission()
    {
        if (!_activityRef.TryGetTarget(out var activity))
            return;

        var intent = global::Android.Net.VpnService.Prepare(activity);
        if (intent is not null)
        {
            ServiceState = AndroidServiceState.Preparing;
            activity.StartActivityForResult(intent, VpnPermissionRequestCode);
        }
        else
        {
            StartVpnService();
        }
    }

    public void OnVpnPermissionResult(bool granted)
    {
        if (granted)
        {
            StartVpnService();
        }
        else
        {
            ServiceState = AndroidServiceState.Idle;
        }
    }

    public void StartVpnService()
    {
        if (!_activityRef.TryGetTarget(out var activity))
            return;

        PigeonPostVpnService.StartVpn(activity);
        ServiceState = AndroidServiceState.Running;

        if (_profile is not null)
        {
            PigeonPostVpnService.RequestEstablishVpnInterface(_profile);
        }
    }

    public void StartVpnService(VpnProfile profile)
    {
        _profile = profile;
        StartVpnService();
    }

    public void SetRuntime(IVpnRuntime runtime)
    {
        _runtime = runtime;
    }

    public void StopVpnService()
    {
        PigeonPostVpnService.StopVpn(global::Android.App.Application.Context);
        _vpnInterfaceEstablished = false;
        _currentConfiguration = null;
        ServiceState = AndroidServiceState.Idle;
    }

    private void OnVpnRevoked()
    {
        _vpnInterfaceEstablished = false;
        _currentConfiguration = null;
        ServiceState = AndroidServiceState.Revoked;
    }

    private void OnVpnInterfaceResult(bool success)
    {
        _vpnInterfaceEstablished = success;
        if (success && _profile is not null)
        {
            _currentConfiguration = AndroidVpnConfiguration.FromProfile(_profile);

            if (_runtime is VpnClientRuntime clientRuntime)
            {
                var service = PigeonPostVpnService.Current;
                if (service?.TunDevice is { } tun)
                    clientRuntime.SetCustomTunDevice(tun);
                if (service?.SocketProtector is { } p)
                    clientRuntime.SetSocketProtector(p);
            }
        }
        else
        {
            _currentConfiguration = null;
        }

        ServiceStateChanged?.Invoke(_serviceState);
    }
}
