using Pontifex;
using Pontifex.Abstractions.Endpoints;

namespace PigeonPost.Bridge;

public interface IBridge
{
    void OnEndpointConnected(IAckRawBaseEndpoint endpoint);
    void OnEndpointDisconnected();
    void OnPacketReceived(byte[] packet);
    bool TryGetNextPacket(out byte[] packet);
    void OnTransportStopped(StopReason reason);
}
