using System;
using System.Collections.Generic;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Utils;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests.Server;

[TestFixture]
public class ServerHubRealRoutingTests
{
    private ServerHub _hub = null!;

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
    public void Packet_DestinedToClientA_SentOnlyToClientA()
    {
        var epA = new TrackingEndpoint();
        var epB = new TrackingEndpoint();

        RegisterAndActivate("client-a", 0xC0A80101, epA);
        RegisterAndActivate("client-b", 0xC0A80102, epB);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(0));
        Assert.That(epA.SentPackets, Has.Count.EqualTo(1));
        Assert.That(epB.SentPackets, Has.Count.EqualTo(0));
    }

    [Test]
    public void Packet_NoMatchingHostRoute_Dropped()
    {
        var epA = new TrackingEndpoint();
        RegisterAndActivate("client-a", 0xC0A80101, epA);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A801FF, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(1));
        Assert.That(epA.SentPackets, Has.Count.EqualTo(0));
    }

    [Test]
    public void Packet_NoBroadcastBehavior()
    {
        var epA = new TrackingEndpoint();
        var epB = new TrackingEndpoint();

        RegisterAndActivate("client-a", 0xC0A80101, epA);
        RegisterAndActivate("client-b", 0xC0A80102, epB);

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(0));
        Assert.That(epA.SentPackets, Has.Count.EqualTo(1));
        Assert.That(epB.SentPackets, Has.Count.EqualTo(0));
    }

    [Test]
    public void DisconnectedClient_RouteDropped()
    {
        var epA = new TrackingEndpoint();
        RegisterAndActivate("client-a", 0xC0A80101, epA);
        _hub.RemoveSession(new ClientId("client-a"));

        byte[] packet = BuildIpv4Packet(dest: 0xC0A80101, source: 0x0A000001);

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedNoRoute, Is.EqualTo(1));
        Assert.That(epA.SentPackets, Has.Count.EqualTo(0));
    }

    [Test]
    public void MalformedIpv4Packet_Dropped()
    {
        RegisterAndActivate("client-a", 0xC0A80101, new TrackingEndpoint());

        byte[] packet = new byte[] { 0x45 };

        _hub.OnPacketFromTun(packet);

        Assert.That(_hub.DroppedMalformedIpv4, Is.EqualTo(1));
    }

    private void RegisterAndActivate(string clientId, uint hostIp, TrackingEndpoint endpoint)
    {
        var handshake = new ClientHandshake(new ClientId(clientId), (IPv4)hostIp);
        var result = _hub.TryRegisterSession(handshake, out _);
        Assert.That(result, Is.EqualTo(SessionRegistrationResult.Accepted));
        _hub.ActivateSessionEndpoint(new ClientId(clientId), endpoint);
    }

    private static byte[] BuildIpv4Packet(uint dest, uint source)
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

    private sealed class TrackingEndpoint : IAckRawBaseEndpoint
    {
        public IEndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;
        public int MessageMaxByteSize => 1_048_576;
        public List<byte[]> SentPackets { get; } = new();

        public SendResult Send(UnionDataList bufferToSend)
        {
            using var disposer = bufferToSend.AsDisposable();
            if (bufferToSend.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
            {
                byte[]? copy = data.ToArray();
                data.Release();
                if (copy != null)
                {
                    SentPackets.Add(copy);
                }
            }
            return SendResult.Ok;
        }

        public bool Disconnect(StopReason reason) => true;
        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null) { }
    }
}
