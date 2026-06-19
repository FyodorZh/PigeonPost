using System.Net.Sockets;
using NUnit.Framework;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class NullSocketProtectorTests
{
    [Test]
    public void ProtectSocket_ReturnsTrue()
    {
        var protector = new NullSocketProtector();
        bool result = protector.ProtectSocket(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        Assert.That(result, Is.True);
    }

    [Test]
    public void ProtectSocket_NullSocket_DoesNotThrow()
    {
        var protector = new NullSocketProtector();
        Assert.DoesNotThrow(() => protector.ProtectSocket(null!));
    }
}
