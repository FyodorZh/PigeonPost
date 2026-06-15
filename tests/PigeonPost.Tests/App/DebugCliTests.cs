using System.IO;
using NUnit.Framework;

namespace PigeonPost.Tests;

[TestFixture]
public class DebugCliTests
{
    [Test]
    [Ignore("--client-id CLI argument not yet implemented (action-06)")]
    public void Client_Role_RequiresClientId_Fails_WhenMissing()
    {
        var cfg = CliParser.Parse(new[] { "--role", "client", "--tun", "tun0", "--url", "url" }, TextWriter.Null);
        Assert.That(cfg, Is.Null);
    }

    [Test]
    [Ignore("--client-id CLI argument not yet implemented (action-06)")]
    public void Client_Role_WithClientId_Parses()
    {
        var cfg = CliParser.Parse(new[] { "--role", "client", "--client-id", "my-client", "--tun", "tun0", "--url", "url" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
    }

    [Test]
    [Ignore("--debug-clients CLI argument not yet implemented (action-06)")]
    public void Debug_Role_DefaultClientCount_IsOne()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
    }

    [Test]
    [Ignore("--debug-clients CLI argument not yet implemented (action-06)")]
    public void Debug_Role_WithThreeClients_Parses()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--debug-clients", "3", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
    }

    [Test]
    [Ignore("--debug-clients CLI argument not yet implemented (action-06)")]
    public void Debug_Role_ClientCountZero_Fails()
    {
        var cfg = CliParser.Parse(new[] { "--role", "debug", "--debug-clients", "0", "--url", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Null);
    }
}
