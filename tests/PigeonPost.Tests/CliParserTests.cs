using System.IO;
using NUnit.Framework;

namespace PigeonPost.Tests;

[TestFixture]
public class CliParserTests
{
    private static BridgeConfiguration Parse(params string[] args) => CliParser.Parse(args, TextWriter.Null)!;

    [Test]
    public void MinimalValidArgs_Server_ParsesCorrectly()
    {
        var cfg = Parse("--role", "server", "--tun", "tun0", "--url", "tcp|127.0.0.1:9000/30");
        Assert.That(cfg.Role, Is.EqualTo(Role.Server));
        Assert.That(cfg.TunNames, Is.EquivalentTo(new[] { "tun0" }));
        Assert.That(cfg.PontifexUrl, Is.EqualTo("tcp|127.0.0.1:9000/30"));
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(10_485_760));
        Assert.That(cfg.Verbose, Is.False);
    }

    [Test]
    public void MinimalValidArgs_Client_ParsesCorrectly()
    {
        var cfg = Parse("--role", "client", "--client-id", "c1", "-t", "tun1", "-u", "direct|ep1");
        Assert.That(cfg.Role, Is.EqualTo(Role.Client));
        Assert.That(cfg.TunNames[0], Is.EqualTo("tun1"));
        Assert.That(cfg.ClientId, Is.EqualTo("c1"));
    }

    [Test]
    public void Debug_Role_RequiresTwoTunNames()
    {
        var cfg = Parse("-r", "debug", "-t", "tunA", "-t", "tunB", "-u", "direct|ep");
        Assert.That(cfg.Role, Is.EqualTo(Role.Debug));
        Assert.That(cfg.TunNames, Is.EquivalentTo(new[] { "tunA", "tunB" }));
    }

    [Test]
    public void Debug_Role_WithOneTun_DefaultsSecond()
    {
        var cfg = CliParser.Parse(new[] { "-r", "debug", "-t", "tunA", "-u", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Not.Null);
        Assert.That(cfg!.Role, Is.EqualTo(Role.Debug));
        Assert.That(cfg!.TunNames, Is.EquivalentTo(new[] { "tunA", "tunB" }));
    }

    [Test]
    public void MissingRole_Fails() => Assert.That(ParseOrNull("-t", "tun0", "-u", "url"), Is.Null);

    [Test]
    public void MissingTun_Fails() => Assert.That(ParseOrNull("-r", "server", "-u", "url"), Is.Null);

    [Test]
    public void MissingUrl_Fails() => Assert.That(ParseOrNull("-r", "server", "-t", "tun0"), Is.Null);

    [Test]
    public void BufferSizeCustom_Parses()
    {
        var cfg = Parse("-r", "client", "--client-id", "c1", "-t", "t0", "-u", "url", "-b", "50000");
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(50_000));
    }

    [Test]
    public void BufferSizeBelowMinimum_Fails()
        => Assert.That(ParseOrNull("-r", "server", "-t", "t0", "-u", "url", "-b", "100"), Is.Null);

    [Test]
    public void VerboseFlag_SetsTrue()
    {
        var cfg = Parse("-r", "server", "-t", "t0", "-u", "url", "-v");
        Assert.That(cfg.Verbose, Is.True);
    }

    [Test]
    public void ShortFormArgs_Work()
    {
        var cfg = Parse("-r", "server", "-t", "t0", "-u", "direct|ep", "-v");
        Assert.That(cfg.Role, Is.EqualTo(Role.Server));
        Assert.That(cfg.Verbose, Is.True);
    }

    [Test]
    public void InvalidRole_Fails()
        => Assert.That(ParseOrNull("-r", "proxy", "-t", "t0", "-u", "url"), Is.Null);

    private static BridgeConfiguration? ParseOrNull(params string[] args)
        => CliParser.Parse(args, TextWriter.Null);
}
