using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Utils;
using PigeonPost.Bridge.Utils;

namespace PigeonPost.Bridge.Handlers;

internal sealed class BridgeClientHandler : IAckRawClientHandler
{
    private readonly IBridge _bridge;

    public BridgeClientHandler(IBridge bridge)
    {
        _bridge = bridge;
    }

    public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
    {
        _bridge.OnEndpointConnected(endPoint);
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        using var guard = IReleasableResource_Ext.AsDisposable(receivedBuffer);

        if (receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data) && data != null)
        {
            byte[] packet = PontifexPacketConverter.ExtractPacket(data);
            _bridge.OnPacketReceived(packet);
        }
    }

    public void WriteAckData(UnionDataList ackData)
    {
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
