using Pontifex;
using PigeonPost.Bridge.Protocol;

namespace PigeonPost.Bridge.Server;

public interface IServerHub
{
    SessionRegistrationResult TryRegisterSession(ClientHandshake handshake, out ClientSession? session);
    void RemoveSession(ClientId clientId);
    void OnPacketFromTun(byte[] packet);
    void OnPacketFromClient(ClientId clientId, byte[] packet);
    void StopAll(StopReason reason);
    int ActiveSessionCount { get; }
    long DroppedNoRoute { get; }
    long DroppedInvalidSource { get; }
    long DroppedMalformedIpv4 { get; }
    long DroppedNonIpv4 { get; }
}
