using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
public class NativeMethodsTests
{
    [Test]
    public void CanCall_NativeOpen_OnInvalidPath_ReturnsMinusOne()
    {
        int fd = NativeMethods.open("/nonexistent/file/for/test", TunConstants.O_RDWR);
        Assert.That(fd, Is.LessThan(0));
    }
}
