using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using PigeonPost.Bridge.Utils;

namespace PigeonPost.Bridge.Handlers;

internal sealed class BridgeServerHandler : IAckRawServerHandler
{
    private readonly IBridge _bridge;

    public BridgeServerHandler(IBridge bridge)
    {
        _bridge = bridge;
    }

    public void OnConnected(IAckRawClientEndpoint endPoint)
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

    public void GetAckResponse(UnionDataList ackData)
    {
    }

    public void OnDisconnected(StopReason reason)
    {
        _bridge.OnEndpointDisconnected();
    }
}
