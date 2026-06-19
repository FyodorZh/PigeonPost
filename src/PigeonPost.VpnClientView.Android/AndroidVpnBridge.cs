using System;
using Android.App;
using Android.Content;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

public sealed class AndroidVpnBridge : IAndroidServiceBridge
{
    private readonly WeakReference<Activity> _activityRef;
    private AndroidServiceState _serviceState;

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

    public event Action<AndroidServiceState>? ServiceStateChanged;

    public AndroidVpnBridge(Activity activity)
    {
        _activityRef = new WeakReference<Activity>(activity);
        PigeonPostVpnService.OnVpnRevoked += OnVpnRevoked;
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
    }

    public void StopVpnService()
    {
        PigeonPostVpnService.StopVpn(global::Android.App.Application.Context);
        ServiceState = AndroidServiceState.Idle;
    }

    private void OnVpnRevoked()
    {
        ServiceState = AndroidServiceState.Revoked;
    }
}
