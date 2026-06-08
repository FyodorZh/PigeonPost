using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;

namespace PigeonPost.Bridge.Handlers;

internal sealed class BridgeServerAcknowledger : IRawServerAcknowledger<BridgeServerHandler>
{
    private readonly IBridge _bridge;

    public BridgeServerAcknowledger(IBridge bridge)
    {
        _bridge = bridge;
    }

    public BridgeServerHandler? TryAck(UnionDataList ackData)
    {
        return new BridgeServerHandler(_bridge);
    }
}
