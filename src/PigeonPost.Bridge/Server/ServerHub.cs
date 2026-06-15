using System;
using System.Collections.Generic;
using Pontifex;
using PigeonPost.Bridge.Protocol;
using Scriba;

namespace PigeonPost.Bridge.Server;

public sealed class ServerHub : IServerHub
{
    private readonly object _lock = new();
    private readonly Dictionary<ClientId, ClientSession> _sessionsByClientId = new();
    private readonly Dictionary<uint, ClientId> _clientIdByHostIp = new();
    private readonly ILogger _logger;

    public int ActiveSessionCount
    {
        get { lock (_lock) return _sessionsByClientId.Count; }
    }

    public long DroppedNoRoute { get; private set; }
    public long DroppedInvalidSource { get; private set; }
    public long DroppedMalformedIpv4 { get; private set; }
    public long DroppedNonIpv4 { get; private set; }

    public ServerHub(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SessionRegistrationResult TryRegisterSession(ClientHandshake handshake, out ClientSession? session)
    {
        throw new NotImplementedException();
    }

    public void RemoveSession(ClientId clientId)
    {
        throw new NotImplementedException();
    }

    public void OnPacketFromTun(byte[] packet)
    {
        throw new NotImplementedException();
    }

    public void OnPacketFromClient(ClientId clientId, byte[] packet)
    {
        throw new NotImplementedException();
    }

    public void StopAll(StopReason reason)
    {
        throw new NotImplementedException();
    }
}
