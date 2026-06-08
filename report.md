# PigeonPost Implementation Report

## Project Overview

A .NET 10 console application that bridges two TUN virtual network devices over a Pontifex transport layer, creating a P2P bidirectional IP tunnel between two Linux machines.

## Architecture Implemented

```
PigeonPost.sln
├── src/
│   ├── PigeonPost.Tun/       — TUN device abstraction (P/Invoke, ITunDevice, TunDevice)
│   ├── PigeonPost.Bridge/    — Core bridging (packet buffer, Pontifex handlers, Bridge orchestrator)
│   └── PigeonPost/           — Console app (CLI parsing, App orchestration, signal handling)
└── tests/
    ├── PigeonPost.Tun.Tests/ — 15 tests (constants, struct layout, P/Invoke, TunDevice contract)
    ├── PigeonPost.Bridge.Tests/ — 24 tests (packet buffer, Pontifex transport, Bridge)
    └── PigeonPost.Tests/     — 22 tests (CLI parser, config, 8 integration/E2E)
```

## What Was Implemented

### Stage 1: Project Scaffolding
- 3-source + 3-test project solution with shared build properties (net10.0, nullable, treat warnings as errors)
- Local NuGet feed and nuget.org configured
- All projects build with 0 warnings, 0 errors

### Stage 2: TUN Native Interop (PigeonPost.Tun)
- `NativeMethods` — libc P/Invoke for `open`, `close`, `ioctl`, `read`, `write`
- `TunConstants` — `/dev/net/tun` path, TUNSETIFF (0x400454ca), IFF_TUN/IFF_NO_PI flags
- `ifreq` struct — 40-byte explicit layout with ifr_name (ByValTStr, 16) + ifr_flags (short)
- CS8981 suppression for lowercase struct name

### Stage 3: TUN Device Wrapper (PigeonPost.Tun)
- `ITunDevice` — public interface with Open/Read/Write/Close/ReadAsync/WriteAsync + IDisposable
- `TunDevice` — sealed implementation with blocking I/O, IOException on errors, async wrappers via Task.Run
- `FakeTunDevice` — test double for Bridge tests

### Stage 4: Configuration & CLI (PigeonPost)
- `Role` enum (Server, Client, Debug)
- `BridgeConfiguration` — immutable config model with validation
- `CliParser` — manual argument parser supporting long/short forms, validation, help text

### Stage 5: Packet Buffer (PigeonPost.Bridge)
- `IPacketBuffer` / `PacketBuffer` — thread-safe FIFO with byte-size capacity, drop-newest overflow
- Lock-based synchronization, concurrent stress test verified

### Stage 6: Pontifex Handlers (PigeonPost.Bridge)
- `IBridge` — handler-to-bridge callback contract
- `BridgeServerAcknowledger` — accepts all connections (V1)
- `BridgeServerHandler` / `BridgeClientHandler` — Pontifex callback implementations
- `PontifexPacketConverter` — UnionDataList ↔ byte[] conversion
- Verified with real Pontifex Direct transport (handshake, send/receive, disconnect, reconnection, 100-packet ordering)

**Key discovery**: `IMultiRefReadOnlyByteArray` extraction uses `data.CopyTo(dst, srcOff, dstOff, count)` with `data.Count` for length.

### Stage 7: Bridge Core (PigeonPost.Bridge)
- `Bridge` — central orchestrator with dedicated TUN reader thread
- Packets buffered when Pontifex not connected, sent directly when connected
- Buffer drained on new connection, thread-safe endpoint management
- `FakeEndpoint` — IAckRawBaseEndpoint test double

**Key discovery**: `ExceptionFail` is in `Pontifex.StopReasons`, constructor: `new ExceptionFail(source, exception, text)`.

### Stage 8: Console Host (PigeonPost)
- `App` — orchestration with server/client/debug mode support
- Client reconnection loop (1s delay, infinite retry)
- `CreateTransport` — Direct construction (public ctors) + TCP via TransportFactory
- `Program.cs` — CLI parsing, Scriba logging, SIGTERM/SIGINT handlers, clean exit

**Key discovery**: TCP server/client producers use same `"tcp"` scheme key; only role-appropriate producer registered.

### Stage 9: Integration Tests (PigeonPost.Tests)
- `PacketBuilder` — ICMP/IP packet builder with correct checksums
- `TunDeviceIntegrationTests` — open/close/reopen, peer-to-peer read/write
- `DebugModeEndToEndTests` — full bridge with real TUNs + Direct transport
- `ReconnectionTests` — client reconnection after server restart (test plan)
- All integration tests tagged `[Category("Integration")]`, skip on non-Linux

## Test Summary

| Test Project | Unit Tests | Integration Tests | Total |
|---|---|---|---|
| PigeonPost.Tun.Tests | 15 | 2 (skipped on macOS) | 15 |
| PigeonPost.Bridge.Tests | 24 | 0 | 24 |
| PigeonPost.Tests | 14 | 8 (skipped on macOS) | 22 |
| **Total** | **53** | **10 (skipped on macOS)** | **61** |

Build: **0 warnings, 0 errors** across all 7 projects.

## Known Issues & Points of Improvement

### Current Issues
1. **TCP TransportFactory producer conflict**: Server and client TCP producers both register under `"tcp"` scheme. `CreateTransport` works around this by registering one at a time based on role — but a single factory can't create both server and client. This is fine for current architecture (separate processes) but would fail if both were needed in one process.
2. **Direct server cannot be stopped**: `AckRawDirectServer.Stop()` throws `NotImplementedException`. The server lives as long as the DirectTransportManager/AppDomain. This is known Pontifex behavior but limits clean shutdown in debug mode.
3. **Non-Linux guard**: Integration tests skip with `Assert.Ignore` on non-Linux, which prevents running full E2E validation on development machines.

### Points of Improvement
1. **Pontifex Protocol.Reconnectable package**: Not used in V1. The client implements manual reconnection (loop with create/start/wait). The reconnectable protocol would provide session-based reconnection with session IDs, reducing overhead.
2. **UDP support**: V1 uses `IAckReliableRaw` for all traffic, which is inefficient for UDP packets. V2 should add UDP-aware transport selection.
3. **Ring buffer**: Current `PacketBuffer` uses `Queue<byte[]>`. A pre-allocated ring buffer with pinned byte arrays would reduce GC pressure at high throughput.
4. **True async I/O**: `ReadAsync`/`WriteAsync` use `Task.Run` wrapping blocking calls. Linux-native async (epoll/io_uring) would be more efficient.
5. **Configuration**: CLI arguments only. Support for config files would be useful for production deployment.
6. **TCP NAT traversal**: No support for NAT traversal (STUN/TURN). V1 assumes direct connectivity.
7. **Monitoring**: The `Bridge` tracks packet/byte counters via `Interlocked` but there's no API exposed for external monitoring. Adding diagnostics endpoint would help.
8. **Pontifex memory pooling**: V1 uses `new UnionDataList()` for simplicity. Using pooled lists (`MemoryRental.Shared.CollectablePool.Acquire`) would reduce allocations.

### Pontifex API Discoveries
| Expectation | Reality |
|---|---|
| `ExceptionFail` in namespace `Pontifex` | In `Pontifex.StopReasons`, ctor `(string, Exception, string)` |
| `IAckRawClientEndpoint` in Client namespace | In `Pontifex.Abstractions.Endpoints.Server` |
| `IAckRawServerEndpoint` in Server namespace | In `Pontifex.Abstractions.Endpoints.Client` |
| `AckRawDirectServer.Stop()` works | Throws `NotImplementedException` |
| `IMultiRefReadOnlyByteArray` has `Length` | Has `Count`, `CopyTo(dst, srcOff, dstOff, count)` with 4 params |
| TCP types accessible directly | Are `internal`, only usable via `TransportFactory` |
