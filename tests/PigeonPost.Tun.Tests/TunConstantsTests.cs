using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
public class TunConstantsTests
{
    [Test]
    public void TunPath_IsCorrect() => Assert.That(TunConstants.TunPath, Is.EqualTo("/dev/net/tun"));

    [Test]
    public void IffTunFlag_IsCorrect() => Assert.That(TunConstants.IFF_TUN, Is.EqualTo((short)0x0001));

    [Test]
    public void IffNoPiFlag_IsCorrect() => Assert.That(TunConstants.IFF_NO_PI, Is.EqualTo((short)0x1000));

    [Test]
    public void Tunsetiff_IsCorrect() => Assert.That(TunConstants.TUNSETIFF, Is.EqualTo((nuint)0x400454ca));

    [Test]
    public void ORdwr_IsCorrect() => Assert.That(TunConstants.O_RDWR, Is.EqualTo(2));

    [Test]
    public void Ifnamsiz_IsCorrect() => Assert.That(TunConstants.IFNAMSIZ, Is.EqualTo(16));
}
