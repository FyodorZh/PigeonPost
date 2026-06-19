using NUnit.Framework;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class ServerHubIsolationTests
{
    private ServerHub _hub = null!;

    private const uint LinuxClientIp = 0x0A000A05;
    private const uint EndpointClientIp = 0x0A000A64;
    private const uint ServerTunIp = 0x0A000A01;
    private const uint InternetIp = 0x08080808;

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [SetUp]
    public void Setup()
    {
        _hub = new ServerHub(StaticLogger.Instance);
    }

    [Test]
    public void EndpointToLinuxPeer_IsDropped()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: LinuxClientIp);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(1));
        Assert.That(_hub.DroppedInvalidSource, Is.EqualTo(0));
    }

    [Test]
    public void EndpointToEndpointPeer_IsDropped()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: 0x0A000AC8);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(1));
    }

    [Test]
    public void EndpointToInternet_IsAllowed()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: InternetIp);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(0));
    }

    [Test]
    public void EndpointToServerTunIp_IsAllowed()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: ServerTunIp);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(0));
    }

    [Test]
    public void LinuxToLinuxPeer_IsAllowed()
    {
        RegisterAndActivate(LinuxClientIp);
        var packet = BuildIpv4Packet(source: LinuxClientIp, dest: 0x0A000A0A);

        _hub.OnPacketFromClient(new IPv4(LinuxClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(0));
    }

    [Test]
    public void LinuxToEndpointPeer_IsDropped()
    {
        RegisterAndActivate(LinuxClientIp);
        var packet = BuildIpv4Packet(source: LinuxClientIp, dest: EndpointClientIp);

        _hub.OnPacketFromClient(new IPv4(LinuxClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(1));
    }

    [Test]
    public void LinuxToInternet_IsAllowed()
    {
        RegisterAndActivate(LinuxClientIp);
        var packet = BuildIpv4Packet(source: LinuxClientIp, dest: InternetIp);

        _hub.OnPacketFromClient(new IPv4(LinuxClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(0));
    }

    [Test]
    public void LinuxToServerTunIp_IsAllowed()
    {
        RegisterAndActivate(LinuxClientIp);
        var packet = BuildIpv4Packet(source: LinuxClientIp, dest: ServerTunIp);

        _hub.OnPacketFromClient(new IPv4(LinuxClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(0));
    }

    [Test]
    public void EndpointToNetworkAddress_IsDropped()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: 0x0A000A00);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(1));
    }

    [Test]
    public void EndpointToSubnetBroadcast_IsDropped()
    {
        RegisterAndActivate(EndpointClientIp);
        var packet = BuildIpv4Packet(source: EndpointClientIp, dest: 0x0A000AFF);

        _hub.OnPacketFromClient(new IPv4(EndpointClientIp), packet);

        Assert.That(_hub.DroppedIsolationPolicy, Is.EqualTo(1));
    }

    private void RegisterAndActivate(uint hostIp)
    {
        var handshake = new ClientHandshake(new IPv4(hostIp));
        _hub.TryRegisterSession(handshake, out _);
    }

    private static byte[] BuildIpv4Packet(uint source, uint dest)
    {
        var packet = new byte[20];
        packet[0] = 0x45;
        packet[2] = 0x00;
        packet[3] = 20;
        packet[12] = (byte)(source >> 24);
        packet[13] = (byte)(source >> 16);
        packet[14] = (byte)(source >> 8);
        packet[15] = (byte)(source & 0xFF);
        packet[16] = (byte)(dest >> 24);
        packet[17] = (byte)(dest >> 16);
        packet[18] = (byte)(dest >> 8);
        packet[19] = (byte)(dest & 0xFF);
        return packet;
    }
}
