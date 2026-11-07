using NUnit.Framework;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class PlaceholderTests
{
    [Test]
    public void Placeholder_TrivialPass()
    {
        Assert.That(true, Is.True);
    }
}
