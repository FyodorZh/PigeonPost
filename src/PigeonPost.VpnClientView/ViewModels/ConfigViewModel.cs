using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.ViewModels;

public sealed partial class ConfigViewModel : ObservableObject
{
    private readonly IProfileStore? _store;
    private bool _loading;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _clientIpLastOctet = "11";

    public string FullIpPreview
    {
        get
        {
            if (int.TryParse(ClientIpLastOctet, out var octet) && octet >= 11 && octet <= 254)
                return $"10.0.10.{octet}";
            return "10.0.10.x";
        }
    }

    public bool IsValid { get; private set; }

    public bool HasLoadedProfile { get; private set; }

    public ObservableCollection<string> Errors { get; } = new();

    public ConfigViewModel(IProfileStore? store = null)
    {
        _store = store;
        _loading = true;

        if (_store?.Load() is { } profile)
        {
            ServerUrl = profile.ServerUrl;
            ClientIpLastOctet = profile.ClientIpLastOctet.ToString();
            HasLoadedProfile = true;
        }

        _loading = false;
        Revalidate();
    }

    partial void OnServerUrlChanged(string value)
    {
        Revalidate();
        AutoSave();
    }

    partial void OnClientIpLastOctetChanged(string value)
    {
        Revalidate();
        AutoSave();
    }

    private void Revalidate()
    {
        Errors.Clear();

        _ = int.TryParse(ClientIpLastOctet, out var octet);
        var errors = VpnProfileValidator.Validate(ServerUrl, octet);
        foreach (var error in errors)
            Errors.Add(error);

        IsValid = errors.Count == 0;
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(FullIpPreview));
    }

    private void AutoSave()
    {
        if (_loading || _store is null || !IsValid)
            return;

        if (int.TryParse(ClientIpLastOctet, out var octet))
        {
            var profile = new VpnProfile(ServerUrl, (byte)octet);
            _store.Save(profile);
        }
    }
}
