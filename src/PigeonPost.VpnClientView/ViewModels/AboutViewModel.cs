using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    public string Version { get; }
    public string ProductName { get; }
    public string Description { get; }
    public string BuildDate { get; }

    public AboutViewModel()
    {
        var assembly = typeof(AboutViewModel).Assembly;
        Version = assembly.GetName()?.Version?.ToString() ?? "1.0.0";
        ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "PigeonPost VPN Client";
        Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "PigeonPost VPN Client";
        BuildDate = "N/A";
    }
}
