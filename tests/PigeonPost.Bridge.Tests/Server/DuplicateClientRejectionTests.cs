using NUnit.Framework;
using PigeonPost.Bridge;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class DuplicateClientRejectionTests
{
    private FakeServerHub _hub = null!;

    [SetUp]
    public void Setup()
    {
        _hub = new FakeServerHub();
    }

    [Test]
    public void DuplicateClientId_FirstConnection_Unaffected()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);

        Assert.That(_hub.TryRegisterSession(h1, out var s1), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s1, Is.Not.Null);
        Assert.That(s1!.ClientId.Value, Is.EqualTo("client-a"));

        var h2 = new ClientHandshake(new ClientId("client-a"), 0xC0A80102);
        Assert.That(_hub.TryRegisterSession(h2, out var s2), Is.EqualTo(SessionRegistrationResult.RejectedDuplicateId));
        Assert.That(s2, Is.Null);

        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }

    [Test]
    public void DuplicateHostIp_FirstConnection_Unaffected()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        Assert.That(_hub.TryRegisterSession(h1, out _), Is.EqualTo(SessionRegistrationResult.Accepted));

        var h2 = new ClientHandshake(new ClientId("client-b"), 0xC0A80101);
        Assert.That(_hub.TryRegisterSession(h2, out _), Is.EqualTo(SessionRegistrationResult.RejectedDuplicateHostIp));

        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }

    [Test]
    public void RejectedDuplicate_DoesNotBlock_ThirdUniqueClient()
    {
        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);

        _hub.TryRegisterSession(
            new ClientHandshake(new ClientId("client-b"), 0xC0A80101), out _);

        var h3 = new ClientHandshake(new ClientId("client-c"), 0xC0A80102);
        Assert.That(_hub.TryRegisterSession(h3, out var s3), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s3, Is.Not.Null);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(2));
    }
}
