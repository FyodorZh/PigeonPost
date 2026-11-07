using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Tests;

public sealed class TestProfileStore : IProfileStore
{
    private readonly VpnProfile? _profile;

    public TestProfileStore(VpnProfile? profile)
    {
        _profile = profile;
    }

    public VpnProfile? Load() => _profile;

    public void Save(VpnProfile profile)
    {
    }
}
