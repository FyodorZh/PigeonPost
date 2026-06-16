using NUnit.Framework;
using PigeonPost.Bridge;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class ClientSessionRegistryTests
{
    private FakeServerHub _hub = null!;

    [SetUp]
    public void Setup()
    {
        _hub = new FakeServerHub();
    }

    [Test]
    public void Register_TwoDistinctClients_BothAccepted()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        var h2 = new ClientHandshake(new ClientId("client-b"), 0xC0A80102);

        Assert.That(_hub.TryRegisterSession(h1, out var s1), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s1, Is.Not.Null);
        Assert.That(_hub.TryRegisterSession(h2, out var s2), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s2, Is.Not.Null);

        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(2));
    }

    [Test]
    public void Register_DuplicateClientId_Rejected()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        var h2 = new ClientHandshake(new ClientId("client-a"), 0xC0A80102);

        Assert.That(_hub.TryRegisterSession(h1, out var s1), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s1, Is.Not.Null);
        Assert.That(_hub.TryRegisterSession(h2, out var s2), Is.EqualTo(SessionRegistrationResult.RejectedDuplicateId));
        Assert.That(s2, Is.Null);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }

    [Test]
    public void Register_DuplicateHostIp_Rejected()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        var h2 = new ClientHandshake(new ClientId("client-b"), 0xC0A80101);

        Assert.That(_hub.TryRegisterSession(h1, out var s1), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s1, Is.Not.Null);
        Assert.That(_hub.TryRegisterSession(h2, out var s2), Is.EqualTo(SessionRegistrationResult.RejectedDuplicateHostIp));
        Assert.That(s2, Is.Null);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }

    [Test]
    public void RemoveSession_OnlyRemovesTargetClient()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        var h2 = new ClientHandshake(new ClientId("client-b"), 0xC0A80102);

        _hub.TryRegisterSession(h1, out _);
        _hub.TryRegisterSession(h2, out _);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(2));

        _hub.RemoveSession(new ClientId("client-a"));
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }

    [Test]
    public void RemoveSession_KeepsOtherClientsActive()
    {
        var h1 = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        var h2 = new ClientHandshake(new ClientId("client-b"), 0xC0A80102);

        _hub.TryRegisterSession(h1, out _);
        _hub.TryRegisterSession(h2, out _);

        _hub.RemoveSession(new ClientId("client-a"));

        _hub.TryRegisterSession(new ClientHandshake(new ClientId("client-c"), 0xC0A80103), out _);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(2));
    }

    [Test]
    public void Reconnect_WithSameId_AfterRemoval_Succeeds()
    {
        _hub.TryRegisterSession(new ClientHandshake(new ClientId("client-a"), 0xC0A80101), out _);
        _hub.RemoveSession(new ClientId("client-a"));
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(0));

        var h = new ClientHandshake(new ClientId("client-a"), 0xC0A80101);
        Assert.That(_hub.TryRegisterSession(h, out var s), Is.EqualTo(SessionRegistrationResult.Accepted));
        Assert.That(s, Is.Not.Null);
        Assert.That(_hub.ActiveSessionCount, Is.EqualTo(1));
    }
}
