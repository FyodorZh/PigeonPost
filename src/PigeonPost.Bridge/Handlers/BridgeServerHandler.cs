using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using PigeonPost.Tun;

namespace PigeonPost.Bridge;

internal sealed class BridgeServerHandler : IAckRawServerHandler
{
    private readonly ServerHub _hub;
    private readonly IPv4? _hostIp;
    private readonly HandshakeRejectCode? _rejectCode;

    public BridgeServerHandler(ServerHub hub, IPv4? hostIp, HandshakeRejectCode? rejectCode)
    {
        _hub = hub;
        _hostIp = hostIp;
        _rejectCode = rejectCode;
    }

    public void OnConnected(IAckRawClientEndpoint endPoint)
    {
        if (_hostIp != null)
        {
            _hub.ActivateSessionEndpoint(_hostIp.Value, endPoint);
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

            if (_hostIp != null && packet != null)
            {
                _hub.OnPacketFromClient(_hostIp.Value, packet);
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
        else if (_hostIp != null)
        {
            var ack = HandshakeAck.Accepted();
            byte[] ackBytes = HandshakeCodec.EncodeAck(ack);
            ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(ackBytes)));
        }
    }

    public void OnDisconnected(StopReason reason)
    {
        if (_hostIp != null)
        {
            _hub.RemoveSession(_hostIp.Value);
        }
    }
}
