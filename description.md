# PigeonPost — Technical Description

## Purpose

A .NET 10 console application that bridges two TUN virtual network devices over
a Pontifex transport layer, effectively creating a P2P bidirectional IP tunnel
between two Linux machines.

```
(real NIC on A) ←routing→ (TUN on A) ←→ (PigeonPost on A) ←Pontifex→ (PigeonPost on B) ←→ (TUN on B) ←routing→ (real NIC on B)
```

## Roles

Each PigeonPost instance runs in one of three roles:

| Role   | Description |
|--------|-------------|
| Server | Listens for a single client via Pontifex, bridges to one TUN device. |
| Client | Connects to a server via Pontifex, bridges to one TUN device. Reconnects forever on disconnect. |
| Debug  | Single-process mode: runs both server and client with two TUN devices on the same machine, using Direct (in-process) Pontifex transport. |

## Transport

Pontifex is the abstraction layer for the data transfer protocol. For V1:

- **Transport mode**: always `IAckReliableRaw` (guaranteed delivery). Known inefficiency for UDP packets — will be addressed in V2.
- **Debug mode**: `Pontifex.Transport.Direct` (in-process, zero-copy).
- **Real mode**: configurable via a Pontifex URL (e.g. `tcp|10.0.0.1:9000/30`). The URL is required even in debug mode (e.g. `direct|ep_name`).
- **Reconnection**: the Client role loops forever, creating a fresh Pontifex transport and reconnecting after every disconnect. The Server role stays running and accepts new connections.
- **Connection model**: strictly 1-to-1 per app instance (one server ↔ one client).

## TUN Device Handling

- **Packet format**: Raw IP (Linux TUN with `IFF_NO_PI` flag, no Ethernet header, no PI header).
- **Device names**: provided by command-line arguments. The app opens existing TUN devices; it does **not** create or configure them. IP addresses and routes are set up externally.
- **P/Invoke**: uses `open("/dev/net/tun", O_RDWR)` + `ioctl(TUNSETIFF, ...)` for opening, `read()`/`write()` for I/O.
- **API design**: the TUN wrapper exposes both synchronous (`Read`/`Write`) and asynchronous (`ReadAsync`/`WriteAsync`) methods. Internally, dedicated blocking threads are used.

## Packet Buffering

- The TUN reader thread starts **immediately** when the app launches, before the Pontifex connection is established.
- Outgoing packets (TUN → Pontifex) are buffered while Pontifex is not connected.
- **Buffer size**: configurable, default 10 MB.
- **Overflow policy**: drop **newest** packets when the buffer is full.
- Incoming packets (Pontifex → TUN) are written directly to the TUN device without buffering.

## Configuration (CLI)

| Argument            | Description |
|---------------------|-------------|
| `--role`            | `server`, `client`, or `debug` |
| `--tun`             | TUN device name (in debug mode, provide this twice for two devices) |
| `--url`             | Pontifex transport URL, required in all modes (e.g. `tcp|127.0.0.1:9000/30`, `direct|ep_name`) |
| `--buffer-size`     | Outgoing packet buffer size in bytes, default 10_485_760 (10 MB) |
| `--verbose`         | Log all traffic (packet sizes in/out) |

## Logging

Uses `Scriba` (same logging library as Pontifex) with a `ConsoleConsumer`.
Verbose mode logs packet sizes for every sent/received packet.

## Graceful Shutdown

Handles `SIGTERM` (and `SIGINT`):
1. Stop accepting new Pontifex connections.
2. Drain remaining buffered packets.
3. Close Pontifex transports.
4. Close TUN file descriptors.
5. Exit cleanly.

## Project Decomposition

| Project              | Type         | Purpose |
|----------------------|--------------|---------|
| `PigeonPost.Tun`     | Class library | TUN device abstraction: open/close/read/write via P/Invoke, sync + async API. |
| `PigeonPost.Bridge`  | Class library | Core bridging: packet buffering, Pontifex handler implementations, send/receive orchestration. |
| `PigeonPost`         | Console app   | Entry point, CLI parsing, app orchestration, signal handling. |

## Target & Dependencies

- **Target framework**: .NET 10 (`net10.0`)
- **NuGet packages**: `Pontifex`, `Pontifex.Transport.Direct`, `Pontifex.Transport.Tcp`, `Scriba`, `Scriba.JsonFactory`
- **Local feed**: `/Users/fyodor/Development/nugets`
- **Runtime**: Linux (Debian/Ubuntu) only

## Pontifex Usage Details (for V1)

1. **Messages**: one raw IP packet per `UnionDataList` message.
2. **Packet serialization**: TUN-read bytes are wrapped into `UnionData(new StaticReadOnlyByteArray(packet))` and sent via endpoint `Send()`.
3. **Packet deserialization**: in `OnReceived`, use `TryPopFirst(out IMultiRefReadOnlyByteArray data)` to extract the byte array, then write to TUN.
4. **Server side**: uses `IRawServerAcknowledger` to accept the client; handler receives `IAckRawClientEndpoint` for sending back.
5. **Client side**: uses `IAckRawClientHandler`; receives `IAckRawServerEndpoint` for sending; reconnects on `OnStopped`.
6. **Direct transport** (debug mode): `AckRawDirectServer` + `AckRawDirectClient`. Server does not need explicit stopping.
7. **TCP transport** (real mode): constructed via `TransportFactory` with URL format `tcp|ip:port/timeout_seconds`.

## Threading Model

- Dedicated blocking threads for TUN I/O (one thread per TUN reader).
- Pontifex callbacks arrive on transport-internal threads.
- All shared mutable state is protected by locks.
- No async/await in the core data path.
