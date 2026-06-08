# Pontifex — Abstract Transport Library for .NET

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Core Concepts](#core-concepts)
4. [Packages & NuGet Dependencies](#packages--nuget-dependencies)
5. [Core Interfaces](#core-interfaces)
6. [Handlers (The User-Implemented Logic)](#handlers-the-user-implemented-logic)
7. [Endpoints (The Communication Channels)](#endpoints-the-communication-channels)
8. [Data Types](#data-types)
9. [Stop Reasons](#stop-reasons)
10. [Transport Implementations](#transport-implementations)
11. [TransportFactory & Addresses](#transportfactory--addresses)
12. [Lifecycle & State Machines](#lifecycle--state-machines)
13. [Memory Model](#memory-model)
14. [Logging](#logging)
15. [Built-in Utility Handlers](#built-in-utility-handlers)
16. [Controls (Debug & Monitoring)](#controls-debug--monitoring)
17. [Complete Sample: Direct Transport](#complete-sample-direct-transport)
18. [Complete Sample: TCP Transport](#complete-sample-tcp-transport)
19. [Complete Sample: Reconnectable Protocol](#complete-sample-reconnectable-protocol)
20. [No-Acknowledgment Transports](#no-acknowledgment-transports)
21. [Performance Considerations](#performance-considerations)
22. [TODO / Known Gaps](#todo--known-gaps)

---

## Overview

Pontifex is a layered, abstract transport library for .NET (netstandard2.1+). It provides
a uniform programming model for in-process and network communication. All transports
share the same handshake, messaging, and lifecycle conventions.

**Key design goals:**

- **Separation of concerns** — transports, protocols, and user logic are independent layers.
- **Pluggable transports** — in-process (Direct) and TCP come built in; you can add others.
- **Pluggable protocols** — the Reconnectable protocol wraps any reliable transport.
- **Resource pooling** — messages are pooled and reference-counted via `Actuarius.Memory`.
- **Structured logging** — uses the `Scriba` logging library.

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│                  User Code                       │
│  (IAckRawServerHandler / IAckRawClientHandler)   │
├──────────────────────────────────────────────────┤
│  Protocols (optional)                            │
│  Pontifex.Protocol.Reconnectable                 │
├──────────────────────────────────────────────────┤
│  Transports                                      │
│  Pontifex.Transport.Direct | Pontifex.Transport.Tcp│
├──────────────────────────────────────────────────┤
│  Core Abstractions                               │
│  Pontifex (ITransport, IEndPoint, StopReason…)   │
├──────────────────────────────────────────────────┤
│  Foundation Libraries                            │
│  Actuarius (memory, collections, FSM)            │
│  Scriba (logging), Operarius (scheduling)        │
└──────────────────────────────────────────────────┘
```

---

## Packages & NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Pontifex` | 0.1.2-dev.0 | Core abstractions, data types, base classes, FSM |
| `Pontifex.Transport.Direct` | 0.1.1-dev.0 | In-process (same-AppDomain) transport |
| `Pontifex.Transport.Tcp` | 0.1.1-dev.0 | TCP network transport |
| `Pontifex.Protocol.Reconnectable` | 0.1.1-dev.0 | Session-based reconnection protocol |
| `Pontifex.Api` | *available* | Higher-level API wrappers (not used in samples) |

All packages target `netstandard2.1`.

Transitive dependencies include:
- `Actuarius` — memory pooling (`IMemoryRental`, `IMultiRefByteArray`), collections, concurrent FSM
- `Scriba` — structured logging (`ILogger`, `StaticLogger`, `ConsoleConsumer`)
- `Operarius` — scheduling (`ILogicDriver`, `IPeriodicLogicDriverCtl`)

---

## Core Interfaces

### `ITransport` (base of all transports)

```csharp
namespace Pontifex.Abstractions;

public interface ITransport
{
    string Type { get; }           // e.g. "direct", "tcp", "reconnectable"
    bool IsValid { get; }          // false after an unrecoverable failure
    bool IsStarted { get; }
    ILogger Log { get; }
    IMemoryRental Memory { get; }

    bool Start(Action<StopReason> onStopped);
    bool Stop(StopReason? reason = null);
}
```

### Transport Interface Hierarchy

```
ITransport
├── IAckRawServer           ← server with ack, raw (byte array) messages
│   ├── IAckReliableRawServer
│   └── IAckUnreliableRawServer
├── IAckRawClient           ← client with ack, raw messages
│   ├── IAckReliableRawClient
│   └── IAckUnreliableRawClient
├── IAckRRServer            ← server with ack, request-response
│   ├── IAckReliableRRServer
│   └── IAckUnreliableRRServer
├── IAckRRClient            ← client with ack, request-response
│   ├── IAckReliableRRClient
│   └── IAckUnreliableRRClient
├── INoAckUnreliableRawServer   ← no ack, raw, unreliable
├── INoAckUnreliableRawClient   ← no ack, raw, unreliable
├── INoAckReliableRRServer      ← no ack, RR, reliable
├── INoAckUnreliableRRServer    ← no ack, RR, unreliable
├── INoAckRRClient              ← no ack, request-response client
│   ├── INoAckReliableRRClient
│   └── INoAckUnreliableRRClient
```

### Reliability Flags

Tagging interfaces on transport types that indicate delivery guarantees:

| Interface | Meaning |
|-----------|---------|
| `IReliable` | Guaranteed delivery — messages will be retried until sent |
| `IUnreliable` | Best-effort delivery — messages may be dropped |
| `IReliableOrUnreliable` | Transport can be configured for either mode |

---

## Handlers (The User-Implemented Logic)

Handlers are the **user-implemented** side of Pontifex. You implement these interfaces
to process incoming messages, handle connection events, and provide handshake data.

### Raw with Acknowledgment (the most common pattern)

#### Server Handler

```csharp
namespace Pontifex.Abstractions.Handlers.Server;

public interface IAckRawServerHandler : IRawBaseHandler, IHandler
{
    // Called once when a client connects. Save `endPoint` — you send through it.
    void OnConnected(IAckRawClientEndpoint endPoint);

    // Called **before** TryAck in the acknowledger returns. Populate `ackData`
    // with optional server→client response data (sent in handshake ack).
    void GetAckResponse(UnionDataList ackData);
}

// Base members inherited from IRawBaseHandler:
public interface IRawBaseHandler : IHandler
{
    void OnDisconnected(StopReason reason);
    void OnReceived(UnionDataList receivedBuffer);
}
```

#### Client Handler

```csharp
namespace Pontifex.Abstractions.Handlers.Client;

public interface IAckRawClientHandler : IRawBaseHandler, IHandler, IAckHandler
{
    // Called after a successful handshake. `endPoint` is your send channel.
    // `ackResponse` contains data the server put in GetAckResponse().
    void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse);

    // Called after OnDisconnected, when transport fully stops.
    void OnStopped(StopReason reason);
}

// Members inherited from IAckHandler:
public interface IAckHandler : IHandler
{
    // Called during handshake. Populate `ackData` with your handshake payload.
    void WriteAckData(UnionDataList ackData);
}
```

#### Server Acknowledger (connection gatekeeper)

```csharp
namespace Pontifex.Abstractions.Acknowledgers;

public interface IRawServerAcknowledger<out THandler> where THandler : IAckRawServerHandler
{
    // Called during handshake. Return a handler to accept, null to reject.
    // `ackData` contains what the client put in WriteAckData().
    THandler? TryAck(UnionDataList ackData);
}
```

### Request-Response Handlers (RR)

Used with `IAckRRServer`/`IAckRRClient` transports.

```csharp
// Server — marker interface
public interface IAckRRServerHandler : IHandler { }

// Client — also marker interface
public interface IAckRRClientHandler : IAckHandler, IHandler { }
```

**NoAck Reliable RR Server Handler:**

```csharp
public interface INoAckReliableRRServerHandler : IHandler
{
    INoAckReliableRRClientSession OpenSession(IEndPoint client);
}

public interface INoAckReliableRRClientSession
{
    event Action<string> OnClosed;

    void OnRequested(DeliveryId id, IMultiRefByteArray data, INoAckReliableRRCallback callback);
    void Close(string reason);
}
```

**NoAck Reliable RR Client Handler:**

```csharp
public interface INoAckReliableRRClientHandler : IHandler
{
    void Started(INoAckReliableRRServerEndpoint endpoint);
    void Stopped();
}
```

Callback interface for the client side of reliable RR:

```csharp
public interface INoAckReliableRRCallback
{
    void Response(DeliveryId id, IMultiRefByteArray data);
    void Failed(NoAckReliableRRFailReason reason);
}

public enum NoAckReliableRRFailReason
{
    Rejected,
    BufferOverflow,
    Timeout
}
```

**NoAck Unreliable RR Handlers:**

```csharp
public interface INoAckUnreliableRRServerHandler : IHandler
{
    void OnRequest(IEndPoint client, Message message);
}

public interface INoAckUnreliableRRClientHandler : IHandler
{
    void Started(INoAckUnreliableRRServerEndpoint endpoint);
    void Received(Message message);
    void Stopped();
}
```

---

## Endpoints (The Communication Channels)

After connection, you send messages through an **endpoint**. The handler receives
the endpoint in its `OnConnected` callback.

### `IAckRawBaseEndpoint`

```csharp
namespace Pontifex.Abstractions.Endpoints;

public interface IAckRawBaseEndpoint
{
    IEndPoint? RemoteEndPoint { get; }
    bool IsConnected { get; }
    int MessageMaxByteSize { get; }

    SendResult Send(UnionDataList bufferToSend);
    bool Disconnect(StopReason reason);
    void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
}
```

- `IAckRawServerEndpoint : IAckRawBaseEndpoint` — client side, sends to server
- `IAckRawClientEndpoint : IAckRawBaseEndpoint` — server side, sends to client

### `IEndPoint` (address)

```csharp
public interface IEndPoint : IEquatable<IEndPoint> { }
```

Built-in implementations in `Pontifex.Endpoints`:
- `StringEndPoint(string)` — for Direct transport
- `GuidEndPoint(Guid)` — used internally for client identity
- `LongEndPoint(long)`
- `VoidEndPoint.Instance` — null-object pattern

### NoAck Endpoints

```csharp
public interface INoAckReliableRRServerEndpoint : INoAckRRServerEndpoint
{
    SendResult Send(IMultiRefByteArray data, INoAckReliableRRCallback callback);
}

public interface INoAckUnreliableRRServerEndpoint : INoAckRRServerEndpoint { }

public interface INoAckRRServerEndpoint
{
    IEndPoint EndPoint { get; }
    int MessageMaxByteSize { get; }
}
```

---

## Data Types

### `UnionData`

A tagged union encapsulating any primitive or byte array. This is the fundamental
building block of Pontifex messages.

```csharp
namespace Pontifex.Utils;

public enum UnionDataType : byte
{
    Unknown, Bool, Byte, Char, Short, UShort,
    Int, UInt, Long, ULong, Float, Double, Decimal,
    Array, NullArray
}

public struct UnionData : IEquatable<UnionData>
{
    public UnionDataType Type { get; }
    public IMultiRefReadOnlyByteArray? Bytes { get; }

    // Constructors for every primitive type + byte arrays
    public UnionData(bool value);
    public UnionData(byte value);
    public UnionData(char value);
    public UnionData(short value);
    public UnionData(ushort value);
    public UnionData(int value);
    public UnionData(uint value);
    public UnionData(long value);
    public UnionData(ulong value);
    public UnionData(float value);
    public UnionData(double value);
    public UnionData(decimal value);
    public UnionData(IMultiRefReadOnlyByteArray? value); // null = NullArray

    // Implicit conversions — you can write: unionList.PutFirst((long)777);
    public static implicit operator UnionData(bool value);
    public static implicit operator UnionData(byte value);
    // … all primitive types …
}
```

### `UnionDataList`

An ordered list of `UnionData` elements — the message container. It is a pooled,
reference-counted resource.

```csharp
public class UnionDataList : MultiRefCollectableResource<UnionDataList>
{
    public void PutFirst(UnionData value);     // prepend
    public void PutLast(UnionData value);      // append
    public UnionData PopFirst();               // pop from front (throws if empty)
    public bool TryPopFirst(out UnionData);    // safe pop
    public UnionDataType PeekFirstType();      // inspect first element type
    public void Clear();

    public bool Serialize(IPool<...> pool, out IMultiRefByteArray serializedData);
    public bool Deserialize<TByteSource>(ref TByteSource source, IPool<...> pool);
}
```

**Extension methods** (`UnionDataList_Ext`):

```csharp
public static class UnionDataList_Ext
{
    // Typed PutFirst helpers
    public static void PutFirst(this UnionDataList, byte value);
    public static void PutFirst(this UnionDataList, short value);
    public static void PutFirst(this UnionDataList, int value);
    public static void PutFirst(this UnionDataList, IMultiRefReadOnlyByteArray value);
    public static void PutFirst(this UnionDataList, string value); // UTF-8 encoded

    // Typed TryPopFirst helpers
    public static bool TryPopFirst(this UnionDataList, out bool value);
    public static bool TryPopFirst(this UnionDataList, out byte value);
    public static bool TryPopFirst(this UnionDataList, out short value);
    public static bool TryPopFirst(this UnionDataList, out int value);
    public static bool TryPopFirst(this UnionDataList, out long value);
    public static bool TryPopFirst(this UnionDataList, out IMultiRefReadOnlyByteArray value);

    // Comparison & copy
    public static bool EqualByContent(this UnionDataList d1, UnionDataList d2);
    public static void CopyFrom(this UnionDataList self, UnionDataList d2);
}
```

### `Message` & `MessageId`

Used by the NoAck variants.

```csharp
public readonly struct MessageId(uint id) : IEquatable<MessageId>
{
    public static readonly MessageId Void;
    public readonly uint Id;
}

public struct Message(MessageId id, IMultiRefByteArray? data) : IReleasableResource
{
    public MessageId Id;
    public IMultiRefByteArray? Data;
    public void Release();
    public Message Acquire();
}
```

### `DeliveryId`

Used by the reliable RR protocol for tracking deliveries.

```csharp
public struct DeliveryId(ushort id) : IComparable<DeliveryId>, IEquatable<DeliveryId>
{
    public static readonly DeliveryId Zero;
    public DeliveryId Next { get; }  // wraps around 1..65534
    public ushort Id { get; }
}
```

### `SendResult`

```csharp
public enum SendResult : byte
{
    Ok = 0,
    MessageToBig = 1,
    InvalidMessage = 2,
    InvalidAddress = 3,
    NotConnected = 4,
    BufferOverflow = 5,
    Error = 6
}
```

---

## Stop Reasons

All stop/disconnect events include a `StopReason` object.

```csharp
namespace Pontifex;

public class StopReason
{
    public string Source { get; }  // e.g. "direct", "tcp", "user"
    public string Type { get; }   // e.g. "UserIntention", "TimeOut", "AckRejected"
}
```

| Class | Type | Meaning |
|-------|------|---------|
| `StopReason` | *(base)* | |
| `UserIntention` | `"UserIntention"` | User code called `Stop()` |
| `TimeOut` | `"TimeOut"` | Inactivity timeout |
| `AckRejected` | `"AckRejected"` | Server rejected the handshake |
| `Unknown` | `"Unknown"` | Unknown reason |
| `UnknownRemoteIntention` | `"UnknownRemoteIntention"` | Remote side disconnected |
| `GracefulRemoteIntention` | `"GracefulRemoteIntention"` | Graceful remote disconnect |
| `ExceptionFail` | `"ExceptionFail"` | Exception occurred |
| `TextFail` | `"TextFail"` | Text-based failure message |
| `ChainFail` | `"ChainFail"` | Wraps another stop reason |
| `Induced` | `"Induced"` | Wraps another stop reason (with Source) |
| `AnyFail` | *(base for failures)* | Abstract base for failures |
| `StopReason.Void` | *(static)* | Sentinal null-object |
| `StopReason.UserIntention` | *(static)* | Reusable singleton |

All subclasses support JSON serialization via `PrintTo(IJsonObject)` and `Print()`.

---

## Transport Implementations

### 1. Direct Transport (in-process)

**Package:** `Pontifex.Transport.Direct`

Zero-copy communication between threads in the same process. Uses a shared
in-memory transport manager (`DirectTransportManager`). Identify servers by
a string name.

#### `AckRawDirectServer`

```csharp
namespace Pontifex.Transports.Direct;

public class AckRawDirectServer : AckRawServer, IAckReliableRawServer
{
    public override int MessageMaxByteSize => 1_232_896; // ~1.2 MB

    public AckRawDirectServer(string serverName, ILogger logger, IMemoryRental memory);

    public bool Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger);
    // Start/Stop inherited from AbstractTransport
}
```

#### `AckRawDirectClient`

```csharp
public class AckRawDirectClient : AckRawClient, IAckReliableRawClient
{
    public override int MessageMaxByteSize => 1_232_896;

    public AckRawDirectClient(string serverName, ILogger logger, IMemoryRental memory);

    public bool Init(IAckRawClientHandler handler);
    // Start/Stop inherited from AbstractTransport
}
```

**Note:** The Direct server does **not** need to be explicitly stopped. It lives
as long as the `DirectTransportManager` singleton (the AppDomain).

### 2. TCP Transport (network)

**Package:** `Pontifex.Transport.Tcp`

Full TCP transport with connection management, keep-alive, and configurable timeouts.

#### `AckRawTcpServer`

```csharp
internal class AckRawTcpServer : AckRawServer, IAckReliableRawServer
{
    public override int MessageMaxByteSize { get; }  // default ~100 MB

    public AckRawTcpServer(
        IPAddress ipAddress,
        int port,
        int connectionsLimit,        // max concurrent connections (default 20000)
        TimeSpan disconnectTimeout,  // idle client timeout
        int? messageMaxSize,         // null = 104,755,200 bytes
        ILogger logger,
        IMemoryRental memoryRental
    );
}
```

#### `AckRawTcpClient`

```csharp
internal class AckRawTcpClient : AckRawClient, IAckReliableRawClient, IAckRawServerEndpoint
{
    public override int MessageMaxByteSize { get; }

    public AckRawTcpClient(
        IPAddress ipAddress,
        int port,
        TimeSpan disconnectTimeout,  // inactivity timeout
        int? messageMaxSize,
        ILogger logger,
        IMemoryRental memoryRental
    );

    // The client IS the endpoint — you can cast to IAckRawServerEndpoint after connect
}
```

**TCP defaults:**
- Default disconnect timeout: 180 seconds
- Default max message size: 104,755,200 bytes (~100 MB)
- Server connection limit: 20,000
- Keep-alive interval: 1 second
- Nagle disabled (`NoDelay = true`)

### 3. Reconnectable Protocol

**Package:** `Pontifex.Protocol.Reconnectable`

Wraps any `IAckReliableRawServer`/`IAckReliableRawClient` transport and adds
session-based reconnection.

#### `AckRawReconnectableServer`

```csharp
public class AckRawReconnectableServer : AckRawServer, IAckReliableRawServer,
    IRawServerAcknowledger<IAckRawServerHandler>
{
    public AckRawReconnectableServer(
        IAckReliableRawServer coreTransport,  // underlying server transport
        TimeSpan disconnectTimeout,            // session timeout
        ILogger logger,
        IMemoryRental memoryRental
    );
}
```

The reconnectable server manages session IDs. Clients that reconnect with a valid
session ID resume their session. New clients go through the normal acknowledgment.

#### `AckRawReconnectableClient`

```csharp
public class AckRawReconnectableClient : AckRawClient, IAckReliableRawClient
{
    public AckRawReconnectableClient(
        Func<IAckReliableRawClient?> underlyingTransportProducer,  // factory for reconnection
        TimeSpan disconnectTimeout,
        ILogger logger,
        IMemoryRental memoryRental
    );
}
```

The reconnectable client automatically reconnects when the underlying transport
drops. It sends session IDs in the handshake so the server can resume.

---

## TransportFactory & Addresses

Pontifex includes a factory system for constructing transports from string addresses.

### Factory Interface

```csharp
namespace Pontifex;

public interface ITransportFactory
{
    ITransport? Construct(string address, ILogger logger, IMemoryRental memoryRental);
}

public interface ITransportFactoryCtl : ITransportFactory
{
    bool Register(ITransportProducer producer);
}

public interface ITransportProducer
{
    string Name { get; }  // transport type: "direct", "tcp", "reconnectable"
    ITransport? Produce(string @params, ITransportFactory factory,
                        ILogger logger, IMemoryRental memoryRental);
}
```

### Address Format

The factory parses addresses as `type|params` (pipe-delimited).

**Direct transport:**
```
direct|server_name
```

**TCP transport:**
```
tcp|ip:port/timeout_in_seconds
```
Example: `tcp|127.0.0.1:8080/30` — 30-second disconnect timeout.

**Reconnectable protocol:**
```
reconnectable|timeout_in_seconds:underlying_address
```
Example: `reconnectable|300:tcp|127.0.0.1:8080/180`

### Using the Factory

```csharp
var factory = new TransportFactory();
var registrator = new TransportFactoryRegistrator(factory);

// Register producers
registrator.Register<AckRawDirectClientProducer>();
registrator.Register<AckRawTcpClientProducer>();

// Construct from address
var transport = factory.Construct("tcp|127.0.0.1:9000/30", logger, MemoryRental.Shared);
```

**Built-in producers:**

| Producer | Package | Name |
|----------|---------|------|
| `AckRawDirectClientProducer` | Pontifex.Transport.Direct | `"direct"` |
| `AckRawDirectServerProducer` | Pontifex.Transport.Direct | `"direct"` |
| `AckRawTcpClientProducer` | Pontifex.Transport.Tcp | `"tcp"` |
| `AckRawTcpServerProducer` | Pontifex.Transport.Tcp | `"tcp"` |
| `AckRawReconnectableClientProducer` | Pontifex.Protocol.Reconnectable | `"reconnectable"` |
| `AckRawReconnectableServerProducer` | Pontifex.Protocol.Reconnectable | `"reconnectable"` |

**Note:** When you reference a transport package, producers typically self-register
via static constructors or the `TransportFactoryRegistrator`. Check the specific
package for auto-registration behavior.

---

## Lifecycle & State Machines

### Server Lifecycle

```
Construct ──→ Init(acknowledger) ──→ Start(onStopped) ──→ Listening
                                    ↓
                          TryAck() per client
                                    ↓
                          GetAckResponse(ackData)
                                    ↓
                          OnConnected(endPoint)
                                    ↓
                    ┌─── OnReceived(buffer) ──→ endPoint.Send(response)
                    │
                    └─── OnDisconnected(reason)
                                    └──→ OnStopped(reason) callback
```

### Client Lifecycle

```
Construct ──→ Init(handler) ──→ Start(onStopped)
                                    ↓
                          WriteAckData(ackData)
                                    ↓
                          OnConnected(endPoint, ackResponse)
                                    ↓
                    ┌─── OnReceived(buffer) ──→ endPoint.Send(data)
                    │
           ┌───────┤
           │       └─── OnDisconnected(reason)
           │                    ↓
           │            OnStopped(reason)
           │                    ↓
           └──── onStopped(reason) callback
```

### State Machine Internals

The `AbstractTransport` base class manages the `IsValid` and `IsStarted` flags.
Subclasses override:

```csharp
protected abstract bool TryStart();              // starts the underlying transport
protected abstract void OnStarted();              // called after successful start
protected abstract void OnStopped(StopReason);    // cleanup after stop
```

`AckRawClient` adds a five-state FSM:
`Constructed → Initialized → Connecting → Connected → Disconnected`

---

## Memory Model

Pontifex uses `Actuarius.Memory` for pooled, reference-counted memory management.

### Key Types

- **`IMemoryRental`** — the memory rental service. `MemoryRental.Shared` is the
  static singleton instance.
- **`IMultiRefByteArray`** — a reference-counted byte array. Pooled.
- **`IMultiRefReadOnlyByteArray`** — read-only version.
- **`IPool<T, TParam>`** — pool interface for acquiring/releasing objects.
- **`MultiRefCollectableResource<T>`** — base class for pooled, reference-counted objects
  (like `UnionDataList`).

### Rules

1. **Messages are pooled.** Call `memory.CollectablePool.Acquire<UnionDataList>()`
   to get a new message, or just use `new UnionDataList()` for short-lived ones.
2. **Reference counting.** Use `IMultiRefResource_Ext.Acquire<T>(obj)` to add a
   reference. Use `((IReleasableResource)obj).Release()` or the `Dispose` pattern
   (`ReleasableResourceDisposer<T>`) to release.
3. **Buffer pool.** `MemoryRental.Shared.Pool` gives `IMultiRefByteArray` instances
   for serialization. Use `pool.Acquire(size)` and `pool.Release()`.
4. **In practice** for simple use cases, `new UnionDataList()` with `PutFirst`/`PutLast`
   works fine — the library manages the lifecycle.

Example of safe disposal:

```csharp
using var disposeGuard = IReleasableResource_Ext.AsDisposable(receivedBuffer);
// … process receivedBuffer …
// Auto-disposed at end of scope
```

---

## Logging

Pontifex uses the `Scriba` library.

```csharp
using Scriba;
using Scriba.Consumers;

// Setup
StaticLogger.Instance.AddConsumer(new ConsoleConsumer());

// Every transport constructor takes an ILogger
var logger = StaticLogger.Instance;
```

Log levels available on `ILogger`:
- `i(string, params object[])` — info
- `w(string, params object[])` — warning
- `e(string, params object[])` — error
- `wtf(Exception)` / `wtf(string, params object[])` — what a terrible failure

---

## Built-in Utility Handlers

### `SynchronizedAckRawClientHandler`

`Pontifex.Handlers.SynchronizedAckRawClientHandler` — wraps a client handler to
synchronize callbacks (OnConnected, OnReceived, OnDisconnected, OnStopped) onto
a single thread via `Service()`.

```csharp
public class SynchronizedAckRawClientHandler : IAckRawClientHandler
{
    public SynchronizedAckRawClientHandler(
        IAckRawClientHandler handler,
        Action onBufferOverflow   // called when internal queue is full (>500 items)
    );

    public void Service();  // call this on your main/update thread each tick
}
```

### Extension Methods on Handlers

```csharp
// Wraps with invariant checker (state machine validation) — used internally by Init()
IAckRawClientHandler handler.Test(Action<string> onFail);

// Wraps with exception safety
IAckRawServerHandler handler.GetSafe(Action<Exception> onFail);
IAckRawServerHandler handler.Test(Action<string> onFail);
```

---

## Controls (Debug & Monitoring)

Endpoints expose controls via `GetControls()`:

```csharp
public interface IControl
{
    string Name { get; }
}

// Example controls:
public interface IAckRawClientControl : IControl
{
    void Stop();  // manually stop the transport
}

public interface ICCUController : IControl
{
    int CCU { get; }  // concurrent users
}

public interface IDeliveryController : IControl
{
    int DeliveredPS { get; }
    int AttemptsPS { get; }
}

public interface IPingCollector : IControl
{
    // ping data
}

public interface ITrafficCollector : IControl
{
    // traffic data
}
```

---

## Complete Sample: Direct Transport

This is the sample from `Program.cs` — a full in-process client↔server exchange.

```csharp
using System.Text;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using Scriba;
using Scriba.Consumers;

// ---- Server Handler ----
public class ServerSideHandler : IAckRawServerHandler
{
    private IAckRawClientEndpoint? _endpoint;

    public void OnConnected(IAckRawClientEndpoint endPoint)
    {
        _endpoint = endPoint;
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        Log.i("Server received: " + receivedBuffer.ToString());

        var response = new UnionDataList();
        response.PutFirst(new UnionData(
            new StaticReadOnlyByteArray("Hello from server!"u8.ToArray())));
        _endpoint!.Send(response);
    }

    public void GetAckResponse(UnionDataList ackData)
    {
        ackData.PutFirst(new UnionData((long)888)); // optional handshake response
    }

    public void OnDisconnected(StopReason reason) { }
}

// ---- Server Acknowledger (connection gatekeeper) ----
public class ServerSideClientAcceptor : IRawServerAcknowledger<ServerSideHandler>
{
    public ServerSideHandler? TryAck(UnionDataList ackData)
    {
        // Check for secret token
        if (ackData.TryPopFirst(out long token) && token == 777)
            return new ServerSideHandler();
        return null; // reject
    }
}

// ---- Client Handler ----
public class ClientHandler : IAckRawClientHandler
{
    public IAckRawServerEndpoint? Endpoint { get; private set; }

    public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
    {
        Endpoint = endPoint;
        // ackResponse contains what server put in GetAckResponse()
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        Log.i("Client received: " + receivedBuffer.ToString());
    }

    public void WriteAckData(UnionDataList ackData)
    {
        ackData.PutFirst(new UnionData((long)777)); // secret token
    }

    public void OnDisconnected(StopReason reason) { }
    public void OnStopped(StopReason reason) { }
}

// ---- Main ----
static void Main(string[] args)
{
    StaticLogger.Instance.AddConsumer(new ConsoleConsumer());

    // Create server
    var server = new Pontifex.Transports.Direct.AckRawDirectServer(
        "server_name", StaticLogger.Instance, MemoryRental.Shared);
    server.Init(new ServerSideClientAcceptor());
    server.Start(reason => Log.i("Server stopped: " + reason));

    // Create client
    var client = new Pontifex.Transports.Direct.AckRawDirectClient(
        "server_name", StaticLogger.Instance, MemoryRental.Shared);
    var clientHandler = new ClientHandler();
    client.Init(clientHandler);
    client.Start(reason => Log.i("Client stopped: " + reason));

    // Send data from client to server
    var ep = clientHandler.Endpoint;
    if (ep != null)
    {
        var data = new UnionDataList();
        data.PutFirst(new UnionData(
            new StaticReadOnlyByteArray("Hello from client!"u8.ToArray())));
        ep.Send(data);
    }

    client.Stop();
    // Direct server doesn't need to be stopped

    // Expected output:
    // INFO: "Server received: [Array:[72,101,108,108,111,32,...]]"
    // INFO: "Client received: [Array:[72,101,108,108,111,32,...]]"
}
```

---

## Complete Sample: TCP Transport

The TCP types are `internal`, so you normally use the `TransportFactory`.

```csharp
using System.Net;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Transports.Tcp;
using Pontifex.Utils;
using Scriba;
using Scriba.Consumers;

// ---- Same handler/acknowledger classes as Direct example above ----

static void Main(string[] args)
{
    StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
    var factory = new TransportFactory();
    var reg = new TransportFactoryRegistrator(factory);
    reg.Register<AckRawTcpServerProducer>();
    reg.Register<AckRawTcpClientProducer>();

    // ---- Start TCP Server ----
    var serverTransport = factory.Construct(
        "tcp|127.0.0.1:9000/30",  // 30s disconnect timeout
        StaticLogger.Instance,
        MemoryRental.Shared);

    if (serverTransport is IAckRawServer ackServer)
    {
        ackServer.Init(new ServerSideClientAcceptor());
        ackServer.Start(reason => Log.i("TCP Server stopped: " + reason));
    }

    // ---- Start TCP Client ----
    var clientTransport = factory.Construct(
        "tcp|127.0.0.1:9000/30",
        StaticLogger.Instance,
        MemoryRental.Shared);

    if (clientTransport is IAckRawClient ackClient)
    {
        var handler = new ClientHandler();
        ackClient.Init(handler);
        ackClient.Start(reason => Log.i("TCP Client stopped: " + reason));

        // Wait for connection, then send
        Thread.Sleep(200);
        if (handler.Endpoint != null)
        {
            var data = new UnionDataList();
            data.PutFirst("Hello via TCP!"u8.ToArray());
            handler.Endpoint.Send(data);
        }
    }

    Console.ReadLine();
    clientTransport?.Stop();
    serverTransport?.Stop();
}
```

### Advanced: Using TCP Transport Directly (reflection-free)

If you need direct access to TCP types (they're `internal` but you can reference
the assembly), or you want to cast for debug controls:

```csharp
// TCP client exposes debug control to gracefully disconnect
var controls = new List<IControl>();
endpoint.GetControls(controls);
foreach (var c in controls)
{
    if (c is IAckRawTcpClientDebugControl debug)
    {
        debug.GracefulDisconnect();
    }
}
```

---

## Complete Sample: Reconnectable Protocol

Wrap a TCP transport with automatic reconnection.

```csharp
using Pontifex.Protocols.Reconnectable;
using Pontifex.Protocols.Reconnectable.AckReliableRaw;

// -- Server --
var reg = new TransportFactoryRegistrator(factory);
reg.Register<AckRawTcpServerProducer>();
reg.Register<AckRawReconnectableServerProducer>();

var server = factory.Construct(
    "reconnectable|300:tcp|127.0.0.1:9000/30",  // 300s session timeout
    StaticLogger.Instance,
    MemoryRental.Shared);

((IAckRawServer)server).Init(new MyAcknowledger());
server.Start(r => Log.i("Server stopped: " + r));

// -- Client --
reg.Register<AckRawTcpClientProducer>();
reg.Register<AckRawReconnectableClientProducer>();

var client = factory.Construct(
    "reconnectable|300:tcp|127.0.0.1:9000/30",
    StaticLogger.Instance,
    MemoryRental.Shared);

((IAckRawClient)client).Init(new MyClientHandler());
client.Start(r => Log.i("Client stopped: " + r));

// Client will automatically reconnect if the TCP connection drops.
// Sessions are maintained by SessionId on the server side.
```

### Manual Reconnectable Client Construction

```csharp
var client = new AckRawReconnectableClient(
    underlyingTransportProducer: () =>
    {
        // Return a fresh client transport each time
        return (IAckReliableRawClient?)factory.Construct(
            "tcp|127.0.0.1:9000/30", logger, MemoryRental.Shared);
    },
    disconnectTimeout: TimeSpan.FromMinutes(5),
    logger: StaticLogger.Instance,
    memoryRental: MemoryRental.Shared
);
```

---

## No-Acknowledgment Transports

For simpler use cases without the connection handshake.

### NoAck Unreliable Raw

Fire-and-forget, no delivery guarantees, no handshake.

```csharp
// Handler interfaces (in Pontifex.Abstractions namespace):
public interface INoAckUnreliableRawServerHandler : IHandler
{
    void OnStarted(INoAckUnreliableRawClientEndpoint endpoint);
    void OnStopped();
    void OnReceived(IEndPoint sender, Message message);
}

public interface INoAckUnreliableRawClientHandler : IHandler
{
    void OnStarted(INoAckUnreliableRawServerEndpoint endpoint);
    void OnReceived(Message message);
    void OnStopped();
}
```

### NoAck Reliable RR

Request-Response with guaranteed delivery, tracked by `DeliveryId`.

```csharp
// Server:
public interface INoAckReliableRRServerHandler : IHandler
{
    INoAckReliableRRClientSession OpenSession(IEndPoint client);
}

public interface INoAckReliableRRClientSession
{
    event Action<string> OnClosed;
    void OnRequested(DeliveryId id, IMultiRefByteArray data, INoAckReliableRRCallback callback);
    void Close(string reason);
}

// Callback on the server side:
public interface INoAckReliableRRCallback  // Pontifex.Abstractions.Endpoints.Server
{
    int MessageMaxByteSize { get; }
    SendResult Response(UnionDataList data);
}

// Client:
public interface INoAckReliableRRClientHandler : IHandler
{
    void Started(INoAckReliableRRServerEndpoint endpoint);
    void Stopped();
}

// Send data + get response:
public interface INoAckReliableRRServerEndpoint : INoAckRRServerEndpoint
{
    SendResult Send(IMultiRefByteArray data, INoAckReliableRRCallback callback);
}

// Callback on the client side:
public interface INoAckReliableRRCallback  // Pontifex.Abstractions.Handlers.Client
{
    void Response(DeliveryId id, IMultiRefByteArray data);
    void Failed(NoAckReliableRRFailReason reason);
}
```

---

## Performance Considerations

1. **Pool messages.** Use `MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>()`
   instead of `new UnionDataList()` for long-running systems to reduce GC pressure.

2. **`UnionDataList` is reference-counted.** Always dispose received buffers properly.
   The framework handles this internally, but if you manually create lists, ensure cleanup.

3. **Direct transport is zero-copy.** In-process communication passes references,
   not serialized bytes. Ideal for local component-to-component messaging.

4. **TCP transport serializes/deserializes.** Every `UnionDataList` is serialized
   to bytes for network transmission. The max message size defaults to ~100 MB.

5. **`SynchronizedAckRawClientHandler`** decouples transport callbacks from
   the actual processing thread. Call `Service()` on your main logic thread.

6. **Reconnectable protocol** adds overhead — session tracking, keep-alive, and
   reconnection logic run at 20ms intervals. Fine for most use cases but be aware
   if you have thousands of sessions.

7. **Thread safety.** The base classes use `lock(_locker)` for state transitions.
   Handler callbacks may be called from different threads depending on the transport.

---

## TODO / Known Gaps

- The **NoAck Unreliable Raw** interfaces are defined but were not implemented
  by any transport in the audited packages (0.1.x-dev).
- The **RR (Request-Response)** interfaces are mostly marker interfaces at this
  development stage — implementations are still in progress.
- TCP transport constructors are `internal` — only usable via `TransportFactory`.
- `Pontifex.Transport.Tcp` is referenced in the sample project but never directly
  instantiated — only its producer is registered.
- There is no built-in **UDP** transport.
- There is no built-in **WebSocket** transport.
- There is no built-in **TLS/SSL** support in the TCP transport.
- The `Pontifex.Api` package exists but is not used in any sample.
