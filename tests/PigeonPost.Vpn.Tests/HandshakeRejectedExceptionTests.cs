using NUnit.Framework;
using PigeonPost.Bridge;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class HandshakeRejectedExceptionTests
{
    [Test]
    public void Constructor_SetsRejectCode()
    {
        var ex = new HandshakeRejectedException(HandshakeRejectCode.DuplicateHostIp);

        Assert.That(ex.RejectCode, Is.EqualTo(HandshakeRejectCode.DuplicateHostIp));
    }

    [Test]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new HandshakeRejectedException(HandshakeRejectCode.ServerShuttingDown, "Server is shutting down");

        Assert.That(ex.RejectCode, Is.EqualTo(HandshakeRejectCode.ServerShuttingDown));
        Assert.That(ex.Message, Does.Contain("Server is shutting down"));
    }

    [Test]
    public void Constructor_DefaultMessage_ContainsRejectCode()
    {
        var ex = new HandshakeRejectedException(HandshakeRejectCode.InvalidHandshake);

        Assert.That(ex.Message, Does.Contain("InvalidHandshake"));
    }

    [Test]
    public void Thrown_CanBeCaughtAsHandshakeRejectedException()
    {
        static void Thrower()
        {
            throw new HandshakeRejectedException(HandshakeRejectCode.DuplicateHostIp);
        }

        var ex = Assert.Throws<HandshakeRejectedException>(Thrower);
        Assert.That(ex!.RejectCode, Is.EqualTo(HandshakeRejectCode.DuplicateHostIp));
    }
}
