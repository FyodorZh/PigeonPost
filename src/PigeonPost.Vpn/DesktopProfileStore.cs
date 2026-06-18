using System;
using System.IO;
using System.Text.Json;

namespace PigeonPost.Vpn;

public sealed class DesktopProfileStore : IProfileStore
{
    private readonly string _filePath;

    public DesktopProfileStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PigeonPost",
            "profile.json"))
    {
    }

    public DesktopProfileStore(string filePath)
    {
        _filePath = filePath;
    }

    public VpnProfile? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            var doc = JsonSerializer.Deserialize<ProfileDocument>(json);
            return doc?.Version == 1 ? doc.Profile : null;
        }
        catch
        {
            return null;
        }
    }

    public void Save(VpnProfile profile)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var doc = new ProfileDocument(1, profile);
        var json = JsonSerializer.Serialize(doc);
        File.WriteAllText(_filePath, json);
    }

    private sealed record ProfileDocument(int Version, VpnProfile? Profile);
}
