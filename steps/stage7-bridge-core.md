# Stage 7: Bridge Core

## Goal

Implement the `Bridge` class that wires together TUN I/O, the packet buffer, and
Pontifex handlers. This is the central orchestration component.

## Prerequisites

- Stage 3 complete (`ITunDevice`, `TunDevice`).
- Stage 5 complete (`IPacketBuffer`, `PacketBuffer`).
- Stage 6 complete (`IBridge`, Pontifex handlers, `PontifexPacketConverter`).

## Architecture

```
                    ┌──────────────────────────────┐
                    │            Bridge             │
                    │                              │
 TUN Device ──────→ │  TUN Reader Thread           │
   (Read)          │    ├─ Reads from TUN          │
                    │    ├─ If connected: send via  │──→ Pontifex Endpoint
                    │    │  Pontifex               │
                    │    └─ If not connected:       │
                    │       buffer packet           │
                    │                              │
                    │  Packet Drain (on connect)    │
                    │    └─ Dequeue buffer → send   │──→ Pontifex Endpoint
                    │                              │
 Pontifex ─────────→│  OnPacketReceived            │
 (OnReceived)      │    └─ Write to TUN            │──→ TUN Device
                    │                              │
 Pontifex ─────────→│  OnEndpointDisconnected      │
 (OnDisconnected)  │    └─ Clear endpoint ref      │
                    │                              │
 Pontifex ─────────→│  OnTransportStopped          │
 (OnStopped)       │    └─ Signal reconnect loop   │
                    └──────────────────────────────┘
```

## Implementation

### `Bridge` class

File: `src/PigeonPost.Bridge/Bridge.cs`

```csharp
using System.Collections.Concurrent;
using Pontifex;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Utils;
using PigeonPost.Bridge.Utils;
using Scriba;

namespace PigeonPost.Bridge;

public sealed class Bridge : IBridge, IDisposable
{
    private readonly ITunDevice _tun;
    private readonly IPacketBuffer _buffer;
    private readonly ILogger _logger;
    private readonly bool _verbose;

    private volatile bool _running;
    private Thread? _tunReaderThread;
    private IAckRawBaseEndpoint? _endpoint;
    private readonly object _endpointLock = new();

    private long _packetsIn;
    private long _packetsOut;
    private long _bytesIn;
    private long _bytesOut;

    public event Action<StopReason>? OnStopped;

    public Bridge(ITunDevice tun, IPacketBuffer buffer, ILogger logger, bool verbose = false)
    {
        _tun = tun ?? throw new ArgumentNullException(nameof(tun));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _verbose = verbose;
    }

    /// <summary>
    /// Start the TUN reader thread. Call after TUN device is opened.
    /// The Pontifex connection may not be established yet — packets will be buffered.
    /// </summary>
    public void Start()
    {
        if (_running)
            throw new InvalidOperationException("Bridge already started.");

        _running = true;
        _tunReaderThread = new Thread(TunReaderLoop)
        {
            Name = "PigeonPost-TunReader",
            IsBackground = true
        };
        _tunReaderThread.Start();
    }

    /// <summary>
    /// Stop the TUN reader thread and clear state. Call before closing the TUN device.
    /// </summary>
    public void Stop(StopReason reason)
    {
        _running = false;

        // Wake up a blocking read by briefly pausing; or rely on Close() to unblock.
        _tunReaderThread?.Join(TimeSpan.FromSeconds(5));

        OnEndpointDisconnected();
        OnStopped?.Invoke(reason);
    }

    private void TunReaderLoop()
    {
        // Use a buffer larger than any IP packet.
        byte[] readBuffer = new byte[65536];

        while (_running)
        {
            try
            {
                int bytesRead = _tun.Read(readBuffer);
                if (bytesRead <= 0) continue;

                byte[] packet = new byte[bytesRead];
                Array.Copy(readBuffer, packet, bytesRead);

                Interlocked.Add(ref _bytesIn, bytesRead);
                Interlocked.Increment(ref _packetsIn);

                if (_verbose)
                    _logger.i($"TUN ← {bytesRead} bytes (#{_packetsIn})");

                // Try to send directly if connected
                if (TryGetEndpoint(out var endpoint))
                {
                    SendPacket(endpoint, packet);
                }
                else
                {
                    // Buffer for later
                    if (!_buffer.TryEnqueue(packet))
                    {
                        if (_verbose)
                            _logger.w($"Buffer full, dropped packet ({bytesRead} bytes). Dropped total: {_buffer.DroppedPackets}");
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.e($"TUN read error: {ex.Message}");
                // If TUN device fails, we should stop everything.
                Stop(new ExceptionFail(ex));
                break;
            }
            catch (ObjectDisposedException)
            {
                // TUN device closed during shutdown.
                break;
            }
        }
    }

    /// <summary>
    /// Called by Pontifex handler when a connection is established.
    /// Drains the packet buffer through the new endpoint.
    /// </summary>
    public void OnEndpointConnected(IAckRawBaseEndpoint endpoint)
    {
        lock (_endpointLock)
        {
            _endpoint = endpoint;
        }

        _logger.i("Pontifex endpoint connected.");

        // Drain buffer
        while (_running && _buffer.TryDequeue(out byte[]? packet))
        {
            if (TryGetEndpoint(out var ep))
                SendPacket(ep, packet);
            else
                break;
        }
    }

    public void OnEndpointDisconnected()
    {
        lock (_endpointLock)
        {
            _endpoint = null;
        }

        _logger.i("Pontifex endpoint disconnected.");
    }

    public void OnPacketReceived(byte[] packet)
    {
        Interlocked.Add(ref _bytesOut, packet.Length);
        Interlocked.Increment(ref _packetsOut);

        if (_verbose)
            _logger.i($"TUN → {packet.Length} bytes (out #{_packetsOut})");

        try
        {
            _tun.Write(packet);
        }
        catch (IOException ex)
        {
            _logger.e($"TUN write error: {ex.Message}");
            Stop(new ExceptionFail(ex));
        }
    }

    public bool TryGetNextPacket(out byte[] packet)
    {
        return _buffer.TryDequeue(out packet!);
    }

    public void OnTransportStopped(StopReason reason)
    {
        _logger.i($"Transport stopped: {reason.Type}");

        OnEndpointDisconnected();

        // The caller (App) watches for this to trigger reconnection.
        OnStopped?.Invoke(reason);
    }

    private bool TryGetEndpoint(out IAckRawBaseEndpoint endpoint)
    {
        lock (_endpointLock)
        {
            endpoint = _endpoint!;
            return _endpoint != null;
        }
    }

    private void SendPacket(IAckRawBaseEndpoint endpoint, byte[] packet)
    {
        var message = PontifexPacketConverter.CreateMessage(packet);
        var result = endpoint.Send(message);

        if (result != SendResult.Ok && _verbose)
            _logger.w($"Send failed: {result}");
    }

    public void Dispose()
    {
        _running = false;
        _tunReaderThread?.Join(TimeSpan.FromSeconds(3));
    }
}
```

### Key Design Decisions

1. **Endpoint is nullable**: set on connect, cleared on disconnect. The TUN reader checks before sending.
2. **Buffer drain on connect**: when a new endpoint connects, all buffered packets are sent through it before new packets.
3. **Thread safety**: `_endpoint` is accessed under `_endpointLock`. The TUN reader thread and Pontifex callbacks may be on different threads.
4. **Verbose logging**: packet sizes in/out are logged only when `--verbose` is set.
5. **Error handling**: TUN read/write errors stop the bridge with an `ExceptionFail` reason.

### `ExceptionFail` helper

Since `ExceptionFail` is a Pontifex type, we need a simple constructor or factory.
From the Pontifex docs:
```csharp
public class ExceptionFail : AnyFail
{
    // Source: exception type name, Type: "ExceptionFail"
}
```

We'll create a helper:

```csharp
// In PigeonPost.Bridge.Utils or inline
internal static StopReason ExceptionFail(Exception ex) =>
    new Pontifex.ExceptionFail(ex); // check actual constructor overload
```

We'll verify the exact constructor during implementation.

## Tests (PigeonPost.Bridge.Tests)

### `BridgeTests` — using FakeTunDevice and FakeBridge pattern

```csharp
[TestFixture]
public class BridgeTests
{
    private FakeTunDevice _tun = null!;
    private PacketBuffer _buffer = null!;
    private Bridge _bridge = null!;
    private StaticLogger _logger = null!;

    [SetUp]
    public void Setup()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
        _tun = new FakeTunDevice();
        _buffer = new PacketBuffer(1_000_000);
        _bridge = new Bridge(_tun, _buffer, StaticLogger.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _bridge.Stop(StopReason.UserIntention);
        _bridge.Dispose();
    }

    [Test]
    public void Start_OpensTunReader()
    {
        _bridge.Start();
        Thread.Sleep(100);
        Assert.That(true); // no crash
    }

    [Test]
    public void PacketsReadFromTun_Buffered_WhenNotConnected()
    {
        _tun.EnqueueIncoming(CreateIpPacket(64));
        _tun.EnqueueIncoming(CreateIpPacket(128));

        _bridge.Start();
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(2));
    }

    [Test]
    public void PacketsReadFromTun_SentDirectly_WhenConnected()
    {
        var endpoint = new FakeEndpoint();
        _bridge.OnEndpointConnected(endpoint);

        _tun.EnqueueIncoming(CreateIpPacket(64));
        _bridge.Start();
        Thread.Sleep(200);

        // No buffering — sent directly
        Assert.That(_buffer.Count, Is.EqualTo(0));
        Assert.That(endpoint.SentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public void BufferedPackets_Drained_OnConnect()
    {
        _tun.EnqueueIncoming(CreateIpPacket(64));
        _tun.EnqueueIncoming(CreateIpPacket(128));

        _bridge.Start();
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(2));

        // Now connect — should drain
        var endpoint = new FakeEndpoint();
        _bridge.OnEndpointConnected(endpoint);
        Thread.Sleep(200);

        Assert.That(_buffer.Count, Is.EqualTo(0));
        Assert.That(endpoint.SentMessages, Has.Count.EqualTo(2));
    }

    [Test]
    public void IncomingPacket_WrittenToTun()
    {
        _bridge.Start();
        var packet = CreateIpPacket(100);

        _bridge.OnPacketReceived(packet);

        Assert.That(_tun.WrittenPackets, Has.Count.EqualTo(1));
        Assert.That(_tun.WrittenPackets[0], Is.EqualTo(packet));
    }

    [Test]
    public void OnTransportStopped_FiresEvent()
    {
        var fired = new ManualResetEventSlim(false);
        StopReason? reason = null;
        _bridge.OnStopped += r => { reason = r; fired.Set(); };

        _bridge.OnTransportStopped(StopReason.UserIntention);

        Assert.That(fired.Wait(TimeSpan.FromSeconds(1)), Is.True);
        Assert.That(reason, Is.SameAs(StopReason.UserIntention));
    }

    private static byte[] CreateIpPacket(int size)
    {
        var data = new byte[size];
        data[0] = 0x45; // IPv4, 20-byte header
        new Random().NextBytes(data);
        return data;
    }
}
```

### `FakeTunDevice`

```csharp
internal class FakeTunDevice : ITunDevice
{
    private readonly Queue<byte[]> _incoming = new();
    private readonly object _lock = new();

    public string Name => "fake_tun";
    public bool IsOpen { get; private set; } = true;
    public List<byte[]> WrittenPackets { get; } = new();

    public void EnqueueIncoming(byte[] packet)
    {
        lock (_lock) _incoming.Enqueue(packet);
    }

    public int Read(byte[] buffer)
    {
        lock (_lock)
        {
            if (_incoming.Count == 0)
            {
                Thread.Sleep(50); // simulate blocking read
                return 0;
            }

            var packet = _incoming.Dequeue();
            Array.Copy(packet, buffer, packet.Length);
            return packet.Length;
        }
    }

    public void Write(byte[] buffer)
    {
        WrittenPackets.Add(buffer.ToArray());
    }

    public void Open(string name) { }
    public void Close() { }
    public void Dispose() { }
    public ValueTask<int> ReadAsync(byte[] buffer, CancellationToken ct) => new(Task.Run(() => Read(buffer), ct));
    public ValueTask WriteAsync(byte[] buffer, CancellationToken ct) => new(Task.Run(() => Write(buffer), ct));
}
```

### `FakeEndpoint`

```csharp
using Pontifex;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Utils;

internal class FakeEndpoint : IAckRawBaseEndpoint
{
    public IEndPoint? RemoteEndPoint => null;
    public bool IsConnected => true;
    public int MessageMaxByteSize => 1_048_576;
    public List<UnionDataList> SentMessages { get; } = new();

    public SendResult Send(UnionDataList bufferToSend)
    {
        SentMessages.Add(bufferToSend);
        return SendResult.Ok;
    }

    public bool Disconnect(StopReason reason) => true;
    public void GetControls(List<Pontifex.IControl> dst, Predicate<Pontifex.IControl>? predicate = null) { }
}
```

## Success Criteria

1. TUN reader thread starts and reads packets.
2. Packets are buffered when no endpoint is connected.
3. Packets are sent directly (bypassing buffer) when endpoint is connected.
4. Buffer is drained on new connection.
5. Incoming Pontifex packets are written to TUN.
6. `OnTransportStopped` fires the event.
7. Bridge stops cleanly when TUN read fails.
8. Thread-safety: no race conditions in concurrent connect/disconnect + read scenarios (verified by stress test).

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost.Bridge/Bridge.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/BridgeTests.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/FakeTunDevice.cs` | Create |
| `tests/PigeonPost.Bridge.Tests/FakeEndpoint.cs` | Create |
