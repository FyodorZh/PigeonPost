using System;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

[Service(Permission = "android.permission.BIND_VPN_SERVICE", Exported = false)]
[IntentFilter(new[] { "android.net.VpnService" })]
public class PigeonPostVpnService : VpnService
{
    private const string NotificationChannelId = "pigeonpost_vpn";
    private const int NotificationId = 1001;

    private ParcelFileDescriptor? _vpnInterface;
    private static VpnProfile? _pendingProfile;

    public static PigeonPostVpnService? Current { get; private set; }
    public static Action? OnVpnRevoked;
    public static Action<bool>? OnVpnInterfaceResult;

    public AndroidTunDevice? TunDevice { get; private set; }
    public AndroidSocketProtector? SocketProtector { get; private set; }

    public bool IsVpnInterfaceEstablished => _vpnInterface is not null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Current = this;
        CreateNotificationChannel();
        var notification = BuildNotification();
        StartForeground(NotificationId, notification);

        if (_pendingProfile is { } profile)
        {
            _pendingProfile = null;
            var success = EstablishVpnInterface(profile);
            OnVpnInterfaceResult?.Invoke(success);
        }

        return StartCommandResult.Sticky;
    }

    public override void OnRevoke()
    {
        base.OnRevoke();
        CloseVpnInterface();
        OnVpnRevoked?.Invoke();
        StopForeground(true);
        StopSelf();
    }

    public override void OnDestroy()
    {
        CloseVpnInterface();
        Current = null;
        StopForeground(true);
        base.OnDestroy();
    }

    public bool EstablishVpnInterface(VpnProfile profile)
    {
        if (_vpnInterface is not null)
            return true;

        try
        {
            var config = AndroidVpnConfiguration.FromProfile(profile);
            var builder = AndroidVpnBuilder.Configure(this, config);
            _vpnInterface = builder.Establish();
            if (_vpnInterface is not null)
            {
                TunDevice = new AndroidTunDevice(_vpnInterface);
                SocketProtector = new AndroidSocketProtector(this);
            }
            return _vpnInterface is not null;
        }
        catch
        {
            return false;
        }
    }

    public void CloseVpnInterface()
    {
        if (TunDevice is not null)
        {
            try { TunDevice.Close(); } catch { }
            TunDevice = null;
        }

        SocketProtector = null;

        if (_vpnInterface is not null)
        {
            try { _vpnInterface.Close(); } catch { }
            try { _vpnInterface.Dispose(); } catch { }
            _vpnInterface = null;
        }
    }

    public static void RequestEstablishVpnInterface(VpnProfile profile)
    {
        if (Current is { } service)
        {
            var success = service.EstablishVpnInterface(profile);
            OnVpnInterfaceResult?.Invoke(success);
        }
        else
        {
            _pendingProfile = profile;
        }
    }

    private void CreateNotificationChannel()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(
                NotificationChannelId,
                "PigeonPost VPN",
                NotificationImportance.Default);
            var manager = GetSystemService(NotificationService) as NotificationManager;
            manager?.CreateNotificationChannel(channel);
        }
    }

    private Notification BuildNotification()
    {
        var builder = new Notification.Builder(this, NotificationChannelId)
            .SetContentTitle("PigeonPost VPN")
            .SetContentText("Connected")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation)
            .SetOngoing(true);

        return builder.Build();
    }

    public static void StartVpn(Context context)
    {
        var intent = new Intent(context, typeof(PigeonPostVpnService));
        context.StartForegroundService(intent);
    }

    public static void StopVpn(Context context)
    {
        context.StopService(new Intent(context, typeof(PigeonPostVpnService)));
    }
}
