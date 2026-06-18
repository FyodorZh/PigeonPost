using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace PigeonPost.VpnClientView.Android;

[Activity(
    Label = "PigeonPost VPN Client",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
