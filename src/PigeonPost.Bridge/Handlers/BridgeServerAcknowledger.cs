using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Utils;

namespace PigeonPost.Bridge;

internal sealed class BridgeServerAcknowledger : IRawServerAcknowledger<BridgeServerHandler>
{
    private readonly ServerHub _hub;

    public BridgeServerAcknowledger(ServerHub hub)
    {
        _hub = hub;
    }

    public BridgeServerHandler? TryAck(UnionDataList ackData)
    {
        ClientHandshake? handshake = null;
        HandshakeRejectCode rejectCode = HandshakeRejectCode.InvalidHandshake;

        using var disposer = ackData.AsDisposable();

        if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            byte[]? handshakeBytes = data.ToArray();
            data.Release();
            if (handshakeBytes == null)
            {
                return new BridgeServerHandler(_hub, null, rejectCode);
            }

            handshake = HandshakeCodec.DecodeRequest(handshakeBytes);
        }

        if (handshake == null)
            return new BridgeServerHandler(_hub, null, rejectCode);

        var result = _hub.TryRegisterSession(handshake, out _);

        switch (result)
        {
            case SessionRegistrationResult.Accepted:
                return new BridgeServerHandler(_hub, handshake.AdvertisedHostIpv4, null);
            case SessionRegistrationResult.RejectedDuplicateHostIp:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.DuplicateHostIp);
            case SessionRegistrationResult.RejectedServerShuttingDown:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.ServerShuttingDown);
            default:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.InvalidHandshake);
        }
    }
}
