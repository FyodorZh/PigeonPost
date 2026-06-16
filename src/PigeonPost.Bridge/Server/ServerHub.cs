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
    private readonly Dictionary<ClientId, ClientSession> _sessionsByClientId = new();
    private readonly Dictionary<uint, ClientId> _clientIdByHostIp = new();
    private readonly ILogger _logger;
    private readonly ITunDevice? _tun;

    private volatile bool _accepting = true;

    public int ActiveSessionCount
    {
        get { lock (_lock) return _sessionsByClientId.Count; }
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
            if (_sessionsByClientId.ContainsKey(handshake.ClientId))
                return SessionRegistrationResult.RejectedDuplicateId;

            if (_clientIdByHostIp.ContainsKey(handshake.AdvertisedHostIpv4))
                return SessionRegistrationResult.RejectedDuplicateHostIp;

            var endpoint = new PendingEndpoint();
            session = new ClientSession(handshake.ClientId, handshake.AdvertisedHostIpv4, endpoint);

            _sessionsByClientId[handshake.ClientId] = session;
            _clientIdByHostIp[handshake.AdvertisedHostIpv4] = handshake.ClientId;

            _logger.i($"Session registered: clientId={handshake.ClientId.Value}, host={FormatIp(handshake.AdvertisedHostIpv4)}");

            return SessionRegistrationResult.Accepted;
        }
    }

    public virtual void ActivateSessionEndpoint(ClientId clientId, IAckRawBaseEndpoint endpoint)
    {
        lock (_lock)
        {
            if (_sessionsByClientId.TryGetValue(clientId, out var session)
                && session.Endpoint is PendingEndpoint pending)
            {
                pending.RealEndpoint = endpoint;
            }
        }
    }

    public void RemoveSession(ClientId clientId)
    {
        ClientSession? session;
        lock (_lock)
        {
            if (!_sessionsByClientId.Remove(clientId, out session))
                return;

            _clientIdByHostIp.Remove(session.AdvertisedHostIpv4);
        }

        _logger.i($"Session removed: clientId={session.ClientId.Value}, host={FormatIp(session.AdvertisedHostIpv4)}");
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
        string? targetClientId;
        lock (_lock)
        {
            if (!_clientIdByHostIp.TryGetValue(info.DestinationAddress, out var clientId)
                || !_sessionsByClientId.TryGetValue(clientId, out var clientSession))
            {
                targetClientId = null;
                targetEndpoint = null;
            }
            else
            {
                targetClientId = clientId.Value;
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
            _logger.w($"Send failed for clientId={targetClientId}: {result}");
        }
    }

    public virtual void OnPacketFromClient(ClientId clientId, byte[] packet)
    {
        var info = Ipv4PacketParser.TryParse(packet);
        if (info == null)
        {
            DroppedMalformedIpv4++;
            return;
        }

        lock (_lock)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out var session))
                return;

            if (info.SourceAddress != session.AdvertisedHostIpv4)
            {
                DroppedInvalidSource++;
                _logger.w($"Invalid source IP from clientId={clientId.Value}: expected={FormatIp(session.AdvertisedHostIpv4)}, got={FormatIp(info.SourceAddress)}");
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
            foreach (var session in _sessionsByClientId.Values)
                endpoints.Add(session.Endpoint);

            _sessionsByClientId.Clear();
            _clientIdByHostIp.Clear();
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
            foreach (var session in _sessionsByClientId.Values)
                return session.Endpoint;
            return null;
        }
    }

    private static string FormatIp(uint ip)
    {
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
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
