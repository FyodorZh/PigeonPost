using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using PigeonPost.Bridge.Protocol;
using PigeonPost.Bridge.Utils;

namespace PigeonPost.Bridge.Handlers;

internal sealed class BridgeServerAcknowledger : IRawServerAcknowledger<BridgeServerHandler>
{
    private readonly Server.ServerHub _hub;

    public BridgeServerAcknowledger(Server.ServerHub hub)
    {
        _hub = hub;
    }

    public BridgeServerHandler? TryAck(UnionDataList ackData)
    {
        ClientHandshake? handshake = null;
        HandshakeRejectCode rejectCode = HandshakeRejectCode.InvalidHandshake;

        if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? data) && data != null)
        {
            byte[] handshakeBytes = PontifexPacketConverter.ExtractPacket(data);
            handshake = HandshakeCodec.DecodeRequest(handshakeBytes);
        }

        if (handshake == null)
            return new BridgeServerHandler(_hub, null, rejectCode);

        var result = _hub.TryRegisterSession(handshake, out _);

        switch (result)
        {
            case Server.SessionRegistrationResult.Accepted:
                return new BridgeServerHandler(_hub, handshake, null);
            case Server.SessionRegistrationResult.RejectedDuplicateId:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.DuplicateClientId);
            case Server.SessionRegistrationResult.RejectedDuplicateHostIp:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.DuplicateHostIp);
            case Server.SessionRegistrationResult.RejectedServerShuttingDown:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.ServerShuttingDown);
            default:
                return new BridgeServerHandler(_hub, null, HandshakeRejectCode.InvalidHandshake);
        }
    }
}
