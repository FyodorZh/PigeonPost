using Pontifex;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

public interface IServerHub
{
    SessionRegistrationResult TryRegisterSession(ClientHandshake handshake, out ClientSession? session);
    void RemoveSession(IPv4 hostIp);
    void OnPacketFromTun(byte[] packet);
    void OnPacketFromClient(IPv4 hostIp, byte[] packet);
    void StopAll(StopReason reason);
    int ActiveSessionCount { get; }
    long DroppedNoRoute { get; }
    long DroppedInvalidSource { get; }
    long DroppedMalformedIpv4 { get; }
    long DroppedNonIpv4 { get; }
    long DroppedIsolationPolicy { get; }
}
