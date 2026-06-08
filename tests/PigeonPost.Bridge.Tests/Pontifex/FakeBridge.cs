using System.Collections.Generic;
using Pontifex;
using Pontifex.Abstractions.Endpoints;

namespace PigeonPost.Bridge.Tests.Pontifex;

internal class FakeBridge : IBridge
{
    private readonly object _lock = new();

    public bool IsConnected { get; private set; }
    public IAckRawBaseEndpoint? Endpoint { get; private set; }
    public List<byte[]> ReceivedPackets { get; } = new();
    public bool Stopped { get; private set; }
    public StopReason? StopReason { get; private set; }

    public void OnEndpointConnected(IAckRawBaseEndpoint endpoint)
    {
        lock (_lock)
        {
            IsConnected = true;
            Endpoint = endpoint;
        }
    }

    public void OnEndpointDisconnected()
    {
        lock (_lock)
        {
            IsConnected = false;
            Endpoint = null;
        }
    }

    public void OnPacketReceived(byte[] packet)
    {
        lock (_lock)
        {
            ReceivedPackets.Add((byte[])packet.Clone());
        }
    }

    public bool TryGetNextPacket(out byte[] packet)
    {
        packet = null!;
        return false;
    }

    public void OnTransportStopped(StopReason reason)
    {
        lock (_lock)
        {
            Stopped = true;
            StopReason = reason;
            IsConnected = false;
            Endpoint = null;
        }
    }
}
