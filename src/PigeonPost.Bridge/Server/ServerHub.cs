using System;
using System.Collections.Generic;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Utils;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Bridge;

public class ServerHub : IServerHub, IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<IPv4, ClientSession> _sessions = new();
    private readonly ILogger _logger;
    private readonly ITunDevice? _tun;

    private volatile bool _accepting = true;

    public int ActiveSessionCount
    {
        get { lock (_lock) return _sessions.Count; }
    }

    public long DroppedNoRoute { get; private set; }
    public long DroppedInvalidSource { get; private set; }
    public long DroppedMalformedIpv4 { get; private set; }
    public long DroppedNonIpv4 { get; private set; }

    public ServerHub(ILogger logger, ITunDevice? tun = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tun = tun;
    }

    public void StopAccepting()
    {
        _accepting = false;
    }

    public SessionRegistrationResult TryRegisterSession(ClientHandshake handshake, out ClientSession? session)
    {
        session = null;

        if (!_accepting)
            return SessionRegistrationResult.RejectedServerShuttingDown;

        lock (_lock)
        {
            if (_sessions.ContainsKey(handshake.AdvertisedHostIpv4))
                return SessionRegistrationResult.RejectedDuplicateHostIp;

            var endpoint = new PendingEndpoint();
            session = new ClientSession(handshake.AdvertisedHostIpv4, endpoint);

            _sessions[handshake.AdvertisedHostIpv4] = session;

            _logger.i($"Session registered: host={handshake.AdvertisedHostIpv4}");

            return SessionRegistrationResult.Accepted;
        }
    }

    public virtual void ActivateSessionEndpoint(IPv4 hostIp, IAckRawBaseEndpoint endpoint)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(hostIp, out var session)
                && session.Endpoint is PendingEndpoint pending)
            {
                pending.RealEndpoint = endpoint;
            }
        }
    }

    public void RemoveSession(IPv4 hostIp)
    {
        lock (_lock)
        {
            if (!_sessions.Remove(hostIp, out var session))
                return;
        }

        _logger.i($"Session removed: host={hostIp}");
    }

    public void OnPacketFromTun(byte[] packet)
    {
        var info = Ipv4PacketParser.TryParse(packet);
        if (info == null)
        {
            DroppedMalformedIpv4++;
            return;
        }

        IAckRawBaseEndpoint? targetEndpoint;
        lock (_lock)
        {
            if (!_sessions.TryGetValue((IPv4)info.DestinationAddress, out var clientSession))
            {
                targetEndpoint = null;
            }
            else
            {
                targetEndpoint = clientSession.Endpoint;
            }
        }

        if (targetEndpoint == null)
        {
            DroppedNoRoute++;
            return;
        }

        var message = PontifexPacketConverter.CreateMessage(packet);
        var result = targetEndpoint.Send(message);

        if (result != SendResult.Ok)
        {
            DroppedNoRoute++;
            _logger.w($"Send failed for host={(IPv4)info.DestinationAddress}: {result}");
        }
    }

    public virtual void OnPacketFromClient(IPv4 hostIp, byte[] packet)
    {
        var info = Ipv4PacketParser.TryParse(packet);
        if (info == null)
        {
            DroppedMalformedIpv4++;
            return;
        }

        lock (_lock)
        {
            if (!_sessions.TryGetValue(hostIp, out var session))
                return;

            if (info.SourceAddress != session.AdvertisedHostIpv4.Value)
            {
                DroppedInvalidSource++;
                _logger.w($"Invalid source IP from host={hostIp}: expected={session.AdvertisedHostIpv4}, got={(IPv4)info.SourceAddress}");
                return;
            }
        }

        if (_tun != null)
        {
            try
            {
                _tun.Write(packet);
            }
            catch (Exception ex)
            {
                _logger.e($"TUN write error: {ex.Message}");
            }
        }
    }

    public void StopAll(StopReason reason)
    {
        List<IAckRawBaseEndpoint> endpoints;
        lock (_lock)
        {
            endpoints = new List<IAckRawBaseEndpoint>();
            foreach (var session in _sessions.Values)
                endpoints.Add(session.Endpoint);

            _sessions.Clear();
        }

        foreach (var ep in endpoints)
        {
            try { ep.Disconnect(reason); } catch { }
        }

        _logger.i($"Stopped all sessions ({endpoints.Count} total).");
    }

    public void Dispose()
    {
        StopAll(StopReason.UserIntention);
    }

    internal IAckRawBaseEndpoint? GetEndpointForTesting()
    {
        lock (_lock)
        {
            foreach (var session in _sessions.Values)
                return session.Endpoint;
            return null;
        }
    }

    private sealed class PendingEndpoint : IAckRawBaseEndpoint
    {
        public IAckRawBaseEndpoint? RealEndpoint { get; set; }
        public IEndPoint? RemoteEndPoint => RealEndpoint?.RemoteEndPoint;
        public bool IsConnected => RealEndpoint?.IsConnected ?? true;
        public int MessageMaxByteSize => RealEndpoint?.MessageMaxByteSize ?? 1_048_576;

        public SendResult Send(UnionDataList bufferToSend)
        {
            var ep = RealEndpoint;
            if (ep != null)
                return ep.Send(bufferToSend);
            return SendResult.Ok;
        }

        public bool Disconnect(StopReason reason) => RealEndpoint?.Disconnect(reason) ?? true;

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null) { }
    }
}
