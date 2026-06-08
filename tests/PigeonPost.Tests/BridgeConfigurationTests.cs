using NUnit.Framework;

namespace PigeonPost.Tests;

[TestFixture]
public class BridgeConfigurationTests
{
    [Test]
    public void DefaultBufferSize_Is10MB()
    {
        var cfg = new BridgeConfiguration();
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(10_485_760));
    }

    [Test]
    public void TunNames_DefaultsToEmpty()
    {
        var cfg = new BridgeConfiguration();
        Assert.That(cfg.TunNames, Is.Empty);
    }
}
