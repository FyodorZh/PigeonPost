using System;
using Android.App;
using Android.Content;
using Android.Net;

namespace PigeonPost.VpnClientView.Android;

[Service(Permission = "android.permission.BIND_VPN_SERVICE", Exported = false)]
[IntentFilter(new[] { "android.net.VpnService" })]
public class PigeonPostVpnService : VpnService
{
    private const string NotificationChannelId = "pigeonpost_vpn";
    private const int NotificationId = 1001;

    public static Action? OnVpnRevoked;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        var notification = BuildNotification();
        StartForeground(NotificationId, notification);
        return StartCommandResult.Sticky;
    }

    public override void OnRevoke()
    {
        base.OnRevoke();
        OnVpnRevoked?.Invoke();
        StopForeground(true);
        StopSelf();
    }

    public override void OnDestroy()
    {
        StopForeground(true);
        base.OnDestroy();
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
