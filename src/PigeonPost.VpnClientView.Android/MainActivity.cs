using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Android;

[Activity(
    Label = "PigeonPost VPN Client",
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private AndroidVpnBridge? _bridge;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _bridge = new AndroidVpnBridge(this);
        AndroidServiceBridgeLocator.Bridge = _bridge;

        base.OnCreate(savedInstanceState);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == AndroidVpnBridge.VpnPermissionRequestCode)
        {
            _bridge?.OnVpnPermissionResult(resultCode == Result.Ok);
        }
    }
}
