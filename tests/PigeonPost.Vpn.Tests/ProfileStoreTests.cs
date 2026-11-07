using System.IO;
using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class ProfileStoreTests
{
    [Test]
    public void SaveThenLoad_Roundtrip()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new DesktopProfileStore(path);
            var profile = new VpnProfile("tcp|203.0.113.10:9000/30", 15);
            store.Save(profile);

            var loaded = store.Load();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.ServerUrl, Is.EqualTo(profile.ServerUrl));
            Assert.That(loaded.ClientIpLastOctet, Is.EqualTo(profile.ClientIpLastOctet));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new DesktopProfileStore(path);
        var result = store.Load();
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CorruptJson_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "this is not json");
            var store = new DesktopProfileStore(path);
            var result = store.Load();
            Assert.That(result, Is.Null);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void Save_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var path = Path.Combine(dir, "profile.json");
        try
        {
            var store = new DesktopProfileStore(path);
            var profile = new VpnProfile("tcp|10.0.0.1:9000/30", 100);
            store.Save(profile);

            Assert.That(File.Exists(path), Is.True);
            var loaded = store.Load();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.ServerUrl, Is.EqualTo(profile.ServerUrl));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
