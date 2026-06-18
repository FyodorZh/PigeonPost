namespace PigeonPost.Vpn;

public interface IProfileStore
{
    VpnProfile? Load();
    void Save(VpnProfile profile);
}
