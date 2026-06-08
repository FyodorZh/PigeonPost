# Stage 6: Pontifex Handlers & Transport Integration

## Goal

Implement Pontifex handler and acknowledger classes for server and client roles.
Verify correct Pontifex behavior through unit tests using the Direct (in-process) transport.

This stage proves our understanding of the Pontifex API: handshake lifecycle,
message sending/receiving, disconnect handling, and memory management.

## Prerequisites

- Stage 1 complete (project `PigeonPost.Bridge` builds with Pontifex references).
- Stage 5 complete (packet buffer is available).

## Technical Details

### Architecture

The Bridge sits between a TUN device and a Pontifex transport:

```
TUN Device ←→ Bridge ←→ Pontifex Handler ←→ Pontifex Transport ←→ Remote
```

The handler receives Pontifex callbacks and delegates to the bridge. The bridge
sends packets through the Pontifex endpoint.

### Handler Interfaces (from Pontifex)

**Client handler** (`IAckRawClientHandler`):
- `WriteAckData(UnionDataList ackData)` — called during handshake; populate with metadata.
- `OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)` — connected; save endpoint for sending.
- `OnReceived(UnionDataList receivedBuffer)` — data received from remote.
- `OnDisconnected(StopReason reason)` — remote disconnected.
- `OnStopped(StopReason reason)` — transport fully stopped.

**Server handler** (`IAckRawServerHandler`):
- `OnConnected(IAckRawClientEndpoint endPoint)` — client connected; save endpoint for sending.
- `OnReceived(UnionDataList receivedBuffer)` — data received from client.
- `GetAckResponse(UnionDataList ackData)` — populate server→client handshake response.
- `OnDisconnected(StopReason reason)` — client disconnected.

**Server acknowledger** (`IRawServerAcknowledger<THandler>`):
- `TryAck(UnionDataList ackData)` — validate client handshake data; return handler or null to reject.

### Message Format

For V1, each Pontifex message carries exactly one raw IP packet:

```csharp
// Sending:
var list = new UnionDataList();
list.PutFirst(new UnionData(new StaticReadOnlyByteArray(packet)));
endpoint.Send(list);

// Receiving (in OnReceived):
if (receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray data))
{
    byte[] packet = new byte[data.Length];
    // Copy data to packet... (exact API TBD during implementation)
    // data.CopyTo or Span access
    bridge.OnPacketReceived(packet);
}
```

**Key unknown**: the exact way to extract bytes from `IMultiRefReadOnlyByteArray`.
We will discover this during implementation by inspecting the Actuarius API.
Possible candidates:
- `data.Span` (if it exposes `ReadOnlySpan<byte>`)
- `data.ToArray()`
- `data.CopyTo(byte[] destination, int offset)`
- Iteration via indexer

Our unit tests in this stage will determine the correct approach.

### Bridge Interface (minimal for this stage)

```csharp
namespace PigeonPost.Bridge;

/// <summary>
/// Called by Pontifex handlers when events occur.
/// </summary>
public interface IBridge
{
    /// <summary>Called when a Pontifex endpoint becomes available for sending.</summary>
    void OnEndpointConnected(IAckRawBaseEndpoint endpoint);

    /// <summary>Called when the Pontifex endpoint is no longer available.</summary>
    void OnEndpointDisconnected();

    /// <summary>Called when a packet arrives from Pontifex, to be written to TUN.</summary>
    void OnPacketReceived(byte[] packet);

    /// <summary>Called to read the next buffered packet to send via Pontifex.</summary>
    bool TryGetNextPacket(out byte[] packet);

    /// <summary>Transport has stopped entirely.</summary>
    void OnTransportStopped(StopReason reason);
}
```

### Implementation

#### `BridgeServerAcknowledger`

File: `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs`

```csharp
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
        // For V1, accept all connections unconditionally.
        // V2: add authentication token via ackData.
        return new BridgeServerHandler(_bridge);
    }
}
```

#### `BridgeServerHandler`

File: `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs`

```csharp
using Pontifex;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;

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
        try
        {
            if (TryExtractPacket(receivedBuffer, out byte[]? packet))
                _bridge.OnPacketReceived(packet);
        }
        finally
        {
            // Release the received buffer if needed.
            // Using 'using var' pattern if IReleasableResource.
        }
    }

    public void GetAckResponse(UnionDataList ackData)
    {
        // No data needed for V1.
    }

    public void OnDisconnected(StopReason reason)
    {
        _bridge.OnEndpointDisconnected();
    }

    private static bool TryExtractPacket(UnionDataList list, out byte[] packet)
    {
        packet = null!;
        if (!list.TryPopFirst(out IMultiRefReadOnlyByteArray data))
            return false;
        // TODO: determine correct extraction method during implementation.
        // Option A: data.ToArray()
        // Option B: new byte[data.Length]; data.CopyTo(...)
        packet = ExtractBytes(data);
        return true;
    }
}
```

#### `BridgeClientHandler`

File: `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`

```csharp
using Pontifex;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Utils;

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
        try
        {
            if (TryExtractPacket(receivedBuffer, out byte[]? packet))
                _bridge.OnPacketReceived(packet);
        }
        finally { /* release if needed */ }
    }

    public void WriteAckData(UnionDataList ackData)
    {
        // No data needed for V1.
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
```

#### Packet Extraction Helper

File: `src/PigeonPost.Bridge/Utils/PontifexPacketConverter.cs`

```csharp
using Actuarius.Memory;
using Pontifex.Utils;

namespace PigeonPost.Bridge.Utils;

internal static class PontifexPacketConverter
{
    public static UnionDataList CreateMessage(byte[] packet)
    {
        var list = new UnionDataList();
        list.PutFirst(new UnionData(new StaticReadOnlyByteArray(packet)));
        return list;
    }

    public static byte[] ExtractPacket(IMultiRefReadOnlyByteArray data)
    {
        // Implementation determined during testing.
        // Likely: data.ToArray() or similar.
        byte[] copy = new byte[data.Length];
        // Copy bytes — exact method TBD.
        return copy;
    }
}
```

## Tests (PigeonPost.Bridge.Tests)

These tests use **real Pontifex Direct transport** to verify the full handshake
and message exchange lifecycle. This is critical since we have limited Pontifex documentation.

### Test Fixture: `PontifexDirectTransportTests`

```csharp
using System.Text;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Transports.Direct;
using Pontifex.Utils;
using PigeonPost.Bridge.Handlers;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost.Bridge.Tests.Pontifex;

[TestFixture]
public class PontifexDirectTransportTests
{
    private const string ServerName = "test_server";

    [OneTimeSetUp]
    public void SetupLogging()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    }

    [Test]
    public void ServerAndClient_Handshake_Succeeds()
    {
        // Create server
        var server = new AckRawDirectServer(ServerName, StaticLogger.Instance, MemoryRental.Shared);
        var bridge = new FakeBridge();
        var acknowledger = new BridgeServerAcknowledger(bridge);
        server.Init(acknowledger);

        var serverStopped = new ManualResetEventSlim(false);
        server.Start(_ => serverStopped.Set());

        // Create client
        var client = new AckRawDirectClient(ServerName, StaticLogger.Instance, MemoryRental.Shared);
        var clientBridge = new FakeBridge();
        var handler = new BridgeClientHandler(clientBridge);
        client.Init(handler);

        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        // Wait for connection
        Thread.Sleep(200);

        Assert.That(bridge.IsConnected, Is.True, "Server bridge should be connected");
        Assert.That(clientBridge.IsConnected, Is.True, "Client bridge should be connected");

        // Cleanup
        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));

        // Direct server doesn't need stopping, but dispose anyway
    }

    [Test]
    public void SendPacket_ServerToClient_Delivered()
    {
        var serverBridge = new FakeBridge();
        var server = CreateServer(serverBridge);
        var serverStopped = new ManualResetEventSlim(false);
        server.Start(_ => serverStopped.Set());

        var clientBridge = new FakeBridge();
        var client = CreateClient(clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        // Server sends to client
        byte[] testPacket = Encoding.UTF8.GetBytes("test-packet-data");
        var msg = PontifexPacketConverter.CreateMessage(testPacket);
        serverBridge.Endpoint!.Send(msg);

        Thread.Sleep(200);

        Assert.That(clientBridge.ReceivedPackets, Has.Count.EqualTo(1));
        Assert.That(clientBridge.ReceivedPackets[0], Is.EqualTo(testPacket));

        // Cleanup
        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void SendPacket_ClientToServer_Delivered()
    {
        var serverBridge = new FakeBridge();
        var server = CreateServer(serverBridge);
        var serverStopped = new ManualResetEventSlim(false);
        server.Start(_ => serverStopped.Set());

        var clientBridge = new FakeBridge();
        var client = CreateClient(clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);

        // Client sends to server
        byte[] testPacket = new byte[] { 0x45, 0x00, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x40, 0x00 };
        var msg = PontifexPacketConverter.CreateMessage(testPacket);
        clientBridge.Endpoint!.Send(msg);

        Thread.Sleep(200);

        Assert.That(serverBridge.ReceivedPackets, Has.Count.EqualTo(1));
        Assert.That(serverBridge.ReceivedPackets[0], Is.EqualTo(testPacket));

        // Cleanup
        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void ClientDisconnect_ServerHandler_OnDisconnectedCalled()
    {
        var serverBridge = new FakeBridge();
        var server = CreateServer(serverBridge);
        var serverStopped = new ManualResetEventSlim(false);
        server.Start(_ => serverStopped.Set());

        var clientBridge = new FakeBridge();
        var client = CreateClient(clientBridge);
        var clientStopped = new ManualResetEventSlim(false);
        client.Start(_ => clientStopped.Set());

        Thread.Sleep(200);
        Assert.That(serverBridge.IsConnected, Is.True);

        // Disconnect client
        client.Stop();
        clientStopped.Wait(TimeSpan.FromSeconds(5));

        Thread.Sleep(200);
        Assert.That(serverBridge.IsConnected, Is.False, "Server should detect client disconnect");
    }

    [Test]
    public void ClientReconnects_NewHandlerCreated()
    {
        var serverBridge = new FakeBridge();
        var server = CreateServer(serverBridge);
        var serverStopped = new ManualResetEventSlim(false);
        server.Start(_ => serverStopped.Set());

        // First client
        {
            var cb = new FakeBridge();
            var client = CreateClient(cb);
            var cs = new ManualResetEventSlim(false);
            client.Start(_ => cs.Set());
            Thread.Sleep(200);
            Assert.That(serverBridge.IsConnected, Is.True);
            client.Stop();
            cs.Wait(TimeSpan.FromSeconds(5));
        }

        Thread.Sleep(200);

        // Second client — should reconnect
        {
            var cb2 = new FakeBridge();
            var client2 = CreateClient(cb2);
            var cs2 = new ManualResetEventSlim(false);
            client2.Start(_ => cs2.Set());
            Thread.Sleep(200);
            Assert.That(serverBridge.IsConnected, Is.True, "Server should accept reconnection");
            client2.Stop();
            cs2.Wait(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public void SendManyPackets_AllDelivered_InOrder()
    {
        var serverBridge = new FakeBridge();
        var server = CreateServer(serverBridge);
        var ss = new ManualResetEventSlim(false);
        server.Start(_ => ss.Set());

        var clientBridge = new FakeBridge();
        var client = CreateClient(clientBridge);
        var cs = new ManualResetEventSlim(false);
        client.Start(_ => cs.Set());

        Thread.Sleep(200);

        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            byte[] pkt = BitConverter.GetBytes(i);
            clientBridge.Endpoint!.Send(PontifexPacketConverter.CreateMessage(pkt));
        }

        Thread.Sleep(500);

        Assert.That(serverBridge.ReceivedPackets, Has.Count.EqualTo(count));
        for (int i = 0; i < count; i++)
        {
            Assert.That(BitConverter.ToInt32(serverBridge.ReceivedPackets[i], 0), Is.EqualTo(i));
        }

        client.Stop();
        cs.Wait(TimeSpan.FromSeconds(5));
    }

    // Helper methods
    private static AckRawDirectServer CreateServer(FakeBridge serverBridge)
    {
        var server = new AckRawDirectServer(ServerName, StaticLogger.Instance, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(serverBridge));
        return server;
    }

    private static AckRawDirectClient CreateClient(FakeBridge clientBridge)
    {
        var client = new AckRawDirectClient(ServerName, StaticLogger.Instance, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(clientBridge));
        return client;
    }
}
```

### Test Fake: `FakeBridge`

```csharp
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
            ReceivedPackets.Add(packet.ToArray()); // defensive copy
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
```

## Success Criteria

1. Server and client complete handshake via Direct transport.
2. Packets sent from client to server arrive correctly.
3. Packets sent from server to client arrive correctly.
4. Order is preserved under 100+ packets.
5. Client disconnect is detected by server.
6. Server accepts reconnection from new client.
7. `byte[]` extraction from `IMultiRefReadOnlyByteArray` is verified and documented.
8. Memory management: no leaks, received buffers are properly disposed.

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost.Bridge/IBridge.cs` | Create |
| `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs` | Create |
| `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs` | Create |
| `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs` | Create |
| `src/PigeonPost.Bridge/Utils/PontifexPacketConverter.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/Pontifex/PontifexDirectTransportTests.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/Pontifex/FakeBridge.cs` | Create |
