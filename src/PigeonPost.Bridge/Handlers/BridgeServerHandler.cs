using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;

namespace PigeonPost.Bridge;

internal sealed class BridgeServerHandler : IAckRawServerHandler
{
    private readonly ServerHub _hub;
    private readonly ClientHandshake? _handshake;
    private readonly HandshakeRejectCode? _rejectCode;

    public BridgeServerHandler(ServerHub hub, ClientHandshake? handshake, HandshakeRejectCode? rejectCode)
    {
        _hub = hub;
        _handshake = handshake;
        _rejectCode = rejectCode;
    }

    public void OnConnected(IAckRawClientEndpoint endPoint)
    {
        if (_handshake != null)
        {
            _hub.ActivateSessionEndpoint(_handshake.ClientId, endPoint);
        }
        else if (_rejectCode != null)
        {
            endPoint.Disconnect(StopReason.UserIntention);
        }
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        using var guard = receivedBuffer.AsDisposable();

        if (receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            using var dataGuard = data.AsDisposable();
            byte[]? packet = data.ToArray();

            if (_handshake != null && packet != null)
            {
                _hub.OnPacketFromClient(_handshake.ClientId, packet);
            }
        }
    }

    public void GetAckResponse(UnionDataList ackData)
    {
        if (_rejectCode != null)
        {
            var ack = HandshakeAck.Rejected(_rejectCode.Value);
            byte[] ackBytes = HandshakeCodec.EncodeAck(ack);
            ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(ackBytes)));
        }
        else if (_handshake != null)
        {
            var ack = HandshakeAck.Accepted();
            byte[] ackBytes = HandshakeCodec.EncodeAck(ack);
            ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(ackBytes)));
        }
    }

    public void OnDisconnected(StopReason reason)
    {
        if (_handshake != null)
        {
            _hub.RemoveSession(_handshake.ClientId);
        }
    }
}
