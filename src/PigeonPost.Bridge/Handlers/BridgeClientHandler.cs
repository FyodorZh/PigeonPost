using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Utils;

namespace PigeonPost.Bridge;

internal sealed class BridgeClientHandler : IAckRawClientHandler
{
    private readonly IBridge _bridge;
    private readonly ClientHandshake? _handshake;

    public BridgeClientHandler(IBridge bridge, ClientHandshake? handshake = null)
    {
        _bridge = bridge;
        _handshake = handshake;
    }

    public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
    {
        if (ackResponse.TryPopFirst(out IMultiRefReadOnlyByteArray? ackData) && ackData != null)
        {
            byte[] ackBytes = PontifexPacketConverter.ExtractPacket(ackData);
            var ack = HandshakeCodec.DecodeAck(ackBytes);
            if (ack != null && ack.Status == HandshakeAckStatus.Rejected)
            {
                _bridge.OnTransportStopped(new Pontifex.StopReasons.ExceptionFail(
                    "handshake", new System.InvalidOperationException($"Handshake rejected: {ack.RejectCode}"),
                    $"Handshake rejected: {ack.RejectCode}"));
                return;
            }
        }

        _bridge.OnEndpointConnected(endPoint);
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        using var guard = receivedBuffer.AsDisposable();

        if (receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            byte[] packet = PontifexPacketConverter.ExtractPacket(data);
            _bridge.OnPacketReceived(packet);
        }
    }

    public void WriteAckData(UnionDataList ackData)
    {
        if (_handshake != null)
        {
            byte[] handshakeBytes = HandshakeCodec.EncodeRequest(_handshake);
            ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(handshakeBytes)));
        }
    }

    public void OnDisconnected(StopReason reason)
    {
        _bridge.OnEndpointDisconnected();
    }

    public void OnStopped(StopReason reason)
    {
        _bridge.OnTransportStopped(reason);
    }
}
