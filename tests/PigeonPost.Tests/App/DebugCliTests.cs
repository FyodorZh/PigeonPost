using System.IO;
using NUnit.Framework;

namespace PigeonPost.Tests;

[TestFixture]
public class DebugCliTests
{
    [Test]
    public void Client_Role_RequiresTunAndUrl()
    {
        var cfg = CliParser.Parse(new[] { "--role", "client", "--tun", "tun0", "--url", "url" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
        Assert.That(cfg!.Role, Is.EqualTo(Role.Client));
    }

    [Test]
    public void Client_Role_MissingTun_Fails()
    {
        var cfg = CliParser.Parse(new[] { "--role", "client", "--url", "url" }, TextWriter.Null);
        Assert.That(cfg, Is.Null);
    }

    [Test]
    public void Debug_Role_DefaultClientCount_IsOne()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
        Assert.That(cfg!.DebugClientCount, Is.EqualTo(1));
    }

    [Test]
    public void Debug_Role_WithThreeClients_Parses()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--debug-clients", "3", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
        Assert.That(cfg!.DebugClientCount, Is.EqualTo(3));
    }

    [Test]
    public void Debug_Role_ClientCountZero_Fails()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--debug-clients", "0", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Null);
    }

    [Test]
    public void Server_Role_DoesNotRequireClientId()
    {
        var cfg = CliParser.Parse(new[] { "--role", "server", "--tun", "tun0", "--url", "url" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
        Assert.That(cfg!.Role, Is.EqualTo(Role.Server));
    }
}
