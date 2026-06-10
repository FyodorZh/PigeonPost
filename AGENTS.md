# PigeonPost — AGENTS.md

## Overview

PigeonPost is a .NET 10 console application that bridges TUN virtual network devices
over a transport layer, creating a P2P bidirectional IP tunnel between two Linux
machines. It runs on .NET 10 (`net10.0`), Linux only (Debian/Ubuntu), with no
implicit usings and nullable enabled project-wide.

```
(real NIC on A) ←routing→ (TUN on A) ←→ (PigeonPost on A) ←transport→ (PigeonPost on B) ←→ (TUN on B) ←routing→ (real NIC on B)
```

## Project Structure

```
PigeonPost.sln
├── src/
│   ├── PigeonPost/             Console app — entry point, CLI parsing, orchestration
│   ├── PigeonPost.Tun/         Class library — Linux TUN device I/O via P/Invoke
│   └── PigeonPost.Bridge/      Class library — packet buffering, Pontifex handlers
├── tests/
│   ├── PigeonPost.Tests/       NUnit — CLI parsing, debug mode E2E, reconnection
│   ├── PigeonPost.Bridge.Tests/ NUnit — packet buffer, bridge, direct transport
│   └── PigeonPost.Tun.Tests/   NUnit — TUN device contracts, constants, P/Invoke
├── deploy/
│   ├── docker/                   Shared Dockerfile + docker-entrypoint.sh
│   ├── client/docker/            docker-compose.yml + deploy-docker.sh + deploy-plain.sh + setup.sh
│   ├── server/docker/            docker-compose.yml + deploy-docker.sh + deploy-plain.sh + setup.sh
│   └── test/docker/              docker-compose.yml + iperf harness
├── Directory.Build.props        Global: net10.0, no implicit usings, nullable, warn-as-error
├── nuget.config                 Sources: local ./nugets + nuget.org
└── pontifex.md                  Pontifex transport library reference (1296 lines)
```

## Three Runtime Roles

| Role   | Behavior |
|--------|----------|
| **Server** | Listens via Pontifex for a single client, bridges to one TUN. Stays running and accepts new connections. |
| **Client** | Connects to a server via Pontifex, bridges to one TUN. Loops forever, reconnecting after every disconnect with a 1-second delay. |
| **Debug**  | Single-process mode: creates two TUNs on the same machine, uses Direct (in-process) Pontifex transport. Server does not need explicit stopping (Direct transport manager is AppDomain-scoped). |

The connection model is strictly **1-to-1** per app instance: one server ↔ one client.

## Key Design Decisions

### TUN devices are never created by the app
The app only opens existing TUN devices via `/dev/net/tun`. IP addresses, routes, and
device creation are handled externally (by `docker-entrypoint.sh` or host `setup.sh`).
This separation avoids the app needing `NET_ADMIN` for device creation — only the
container/host scripts need root.

### Packets are buffered before the transport connects
The TUN reader thread starts **immediately** at launch — before any Pontifex handshake.
Outgoing packets are buffered in a bounded FIFO queue (byte-capacity capped, default
10 MB). When the buffer is full, the **newest** packets are dropped (not oldest).
This ensures the oldest queued packets get delivered first when the connection comes up.

### Incoming packets are never buffered
Packets received from Pontifex are written directly to the TUN device. No queuing on
the inbound path. This keeps latency minimal for traffic flowing from the remote side.

### Client reconnects forever
The client role wraps its Pontifex transport in a `while (!shutdownRequested)` loop.
On disconnect it waits 1 second and creates a fresh transport. This handles network
outages and server restarts transparently. The reconnectable protocol from Pontifex is
not used — PigeonPost handles reconnection at the application level for V1.

### No async/await in the data path
TUN I/O runs on a dedicated blocking thread (`PigeonPost-TunReader`). Pontifex callbacks
arrive on transport-internal threads. All shared mutable state is protected by locks.
Async is used only at the orchestration level (`RunServerAsync` / `RunClientAsync`) for
the `WaitForShutdownAsync` pattern.

### Threading model
- One dedicated `Thread` per TUN reader (named `PigeonPost-TunReader`, background)
- Pontifex callbacks arrive on transport-internal threads
- `lock(_endpointLock)` protects the endpoint reference
- `lock(_lock)` protects the PacketBuffer queue
- Async/await only in orchestration code (`App.cs`), not in the hot path

### Graceful shutdown on SIGTERM/SIGINT
On POSIX signals, `RequestShutdown()` sets a flag + cancels a `CancellationTokenSource`.
The app then: (1) stops accepting new Pontifex connections, (2) drains buffered packets,
(3) closes the transport, (4) closes TUN file descriptors.

### Buffer overflow drops newest, not oldest
The `PacketBuffer` is a bounded byte-capacity queue. When a new packet would exceed
the capacity, it is dropped. This is the `drop-newest` policy: older packets (already
queued) are preserved, since they represent older traffic that would otherwise be lost
entirely. The `DroppedPackets` counter tracks lost data.

## Build & Test Commands

```bash
# Restore (local nugets + nuget.org)
dotnet restore

# Build all projects
dotnet build

# Run all tests (NUnit)
dotnet test

# Run specific test project
dotnet test tests/PigeonPost.Bridge.Tests/

# Publish for Docker
dotnet publish src/PigeonPost/PigeonPost.csproj -c Release -o /app
```

### Test categories
- **Unit tests** — run on any OS: `PacketBufferTests`, `BridgeTests` (with fakes), `CliParserTests`, `TunDeviceContractTests`, constant/interop struct tests.
- **Integration tests** — **Linux only**: `DebugModeEndToEndTests` (opens real TUNs, sends real ICMP packets), `TunDeviceIntegrationTests` (opens preconfigured TUN devices), `PontifexDirectTransportTests` (in-process transport with fake TUN).

Some integration tests require pre-created TUN devices (`tunA`, `tunB`). Run the test Docker compose to get a clean environment.

## CLI Usage

```
PigeonPost --role <server|client|debug> --tun <name> [--tun <name2>] --url <url> [options]
```

| Argument | Required | Default | Description |
|----------|----------|---------|-------------|
| `-r, --role` | Yes | — | `server`, `client`, or `debug` |
| `-t, --tun` | Server/Client: once. Debug: optional | `tunA tunB` (debug) | TUN device name(s) |
| `-u, --url` | Yes | — | Pontifex transport URL (e.g. `tcp\|10.0.0.1:9000/30`) |
| `-b, --buffer-size` | No | `10485760` (10 MB) | Outgoing packet buffer in bytes (1500–1,073,741,824) |
| `-v, --verbose` | No | `false` | Log every packet size |
| `-h, --help` | No | — | Show man-page help |

### Transport URL format
- **TCP**: `tcp|ip:port/timeout_seconds` — e.g. `tcp|0.0.0.0:9000/30`
- **Direct**: `direct|server_name` — e.g. `direct|ep_debug` (debug mode only)

### Examples
```bash
# Server on TCP port 9000, bridging tun0
PigeonPost --role server --tun tun0 --url 'tcp|0.0.0.0:9000/30'

# Client connecting to the server, bridging tun1
PigeonPost --role client --tun tun1 --url 'tcp|10.0.0.1:9000/30'

# Debug mode with two TUN devices
PigeonPost --role debug --tun tun0 --tun tun1 --url 'direct|ep_debug'
```

## NuGet Dependencies

| Package | Purpose |
|---------|---------|
| `Pontifex` (0.1.2-dev.0) | Core transport abstractions, data types, FSM |
| `Pontifex.Transport.Tcp` (0.1.1-dev.0) | TCP network transport |
| `Pontifex.Transport.Direct` (0.1.1-dev.0) | In-process zero-copy transport |
| `Scriba` | Structured logging |
| `Scriba.JsonFactory` | JSON log formatting |
| `Actuarius` | Memory pooling (transitive) |
| `Operarius` | Scheduling (transitive) |

All custom packages are sourced from a **local NuGet feed** at `./nugets` in the repo
(also configured as `/nugets` build context in Docker). The `nuget.config` lists both
the local feed and nuget.org.

## Deploy Configuration

### Shared Dockerfile (`deploy/docker/Dockerfile`)
All roles share the same multi-stage build:
- **Build stage**: `dotnet/sdk:10.0` — copies local nugets, adds source, restores, publishes to `/app`
- **Runtime stage**: `dotnet/runtime:10.0` — installs `iproute2`, copies app + entrypoint
- Entrypoint: `docker-entrypoint.sh` which creates/configures the TUN device, then execs the app

### Shared entrypoint script (`deploy/docker/docker-entrypoint.sh`)
Reads env vars `TUN_NAME`, `TUN_IP`, `PEER_IP`. Creates the TUN device with `ip tuntap add`,
assigns the /30 IP, brings it up, adds a route to the peer, then `exec dotnet /app/PigeonPost.dll "$@"`.

### Deployment uses host networking
Production `docker-compose.yml` files use `network_mode: host` with `NET_ADMIN` capability
and `/dev/net/tun` device. This gives the container direct access to the host's network stack.

### Test harness uses bridge networking
The test `docker-compose.yml` uses a `pigeon-net` bridge network. Each container gets its own
network namespace with a TUN device, and iperf3 sidecars attach via `network_mode: "service:..."`.
TCP connect timeouts are 30 seconds (hardcoded in URLs).

### Deployment scripts

Two deployment methods are provided per role, **guaranteed equivalent** — any future changes
to one must be mirrored in the other:

- **`deploy-docker.sh`** — builds a Docker image and runs the app in a container (host networking + NET_ADMIN).
- **`deploy-plain.sh`** — publishes the app directly and runs it on the host (requires root for TUN setup).

| Script | Role | URL |
|--------|------|-----|
| `deploy/server/docker/deploy-docker.sh` | Server | `tcp\|0.0.0.0:9000/30` |
| `deploy/server/docker/deploy-plain.sh` | Server | `tcp\|0.0.0.0:9000/30` |
| `deploy/client/docker/deploy-docker.sh` | Client | `tcp\|203.0.113.10:9000/30` |
| `deploy/client/docker/deploy-plain.sh` | Client | `tcp\|203.0.113.10:9000/30` |

### Host setup scripts
- **Server** (`deploy/server/docker/setup.sh`): Creates TUN, enables IP forwarding, NAT from tunnel
  subnet to WAN interface (`POSTROUTING -o $WAN_IF -s 10.0.0.0/30`).
- **Client** (`deploy/client/docker/setup.sh`): Creates TUN, enables IP forwarding, NAT to tunnel
  (`POSTROUTING -o tun0`), marks LAN traffic with fwmark 1, sets up policy routing table 234.

## Pontifex Library (Summary)

Pontifex is a layered, abstract .NET transport library used by PigeonPost for all
network communication. Full documentation is in `pontifex.md` (1296 lines).

**Architecture**: Transports → Protocols (optional) → Handlers (user code). All
transports share the same handshake, messaging, and lifecycle conventions.

**Key concept**: Handlers and endpoints. You implement `IAckRawServerHandler` or
`IAckRawClientHandler` to process incoming messages. After handshake, you receive
an endpoint (`IAckRawServerEndpoint` or `IAckRawClientEndpoint`) through which you
call `Send(UnionDataList)`.

**Transport types used by PigeonPost**:
- `AckRawDirectServer` / `AckRawDirectClient` — in-process, zero-copy. Used in Debug
  mode. Server lives for the AppDomain lifetime.
- `AckRawTcpServer` / `AckRawTcpClient` — TCP network transport with configurable
  timeouts and keep-alive. Constructed via `TransportFactory` from URL strings.
  Types are `internal`, accessed via producer pattern.

**Message format**: `UnionDataList` is the message container. PigeonPost wraps each
raw IP packet as a single `UnionData(new StaticReadOnlyByteArray(packet))` in a
one-element list.

**Handshake**: Acknowledging (`IAck` prefix) transports perform a 3-way handshake:
client sends `WriteAckData`, server calls `TryAck` (accept/reject), then handlers
receive `OnConnected`. PigeonPost's `BridgeServerAcknowledger` accepts all connections
unconditionally.

**Stop reasons**: Typed disconnect reasons (`StopReason.UserIntention`, `TimeOut`,
`ExceptionFail`, etc.). The client handler routes `OnStopped` to the bridge which
triggers reconnection.

**Memory model**: Messages are pooled and reference-counted via `Actuarius.Memory`.
`UnionDataList` implements `IReleasableResource`. Handlers must dispose received buffers
(via `IReleasableResource_Ext.AsDisposable`).

## Use Cases

### Primary: Site-to-site VPN tunnel
PigeonPost acts as a lightweight alternative to WireGuard/OpenVPN for linking two
networks. The server is placed on a machine with a public IP; the client sits behind
NAT. IP forwarding and NAT rules on both ends route traffic through the tunnel.

### Secondary: Local development/debugging
Debug mode runs both sides in a single process using Direct transport — no network
required. Useful for testing packet flow, buffer behavior, and transport integration
without deploying to two machines.

### Testing: Docker-based integration with iperf3
The test Docker compose brings up server + client containers on a bridge network with
iperf3 sidecars. Tests UDP (100 Mbps, 30s) and TCP (100 Mbps, 30s) throughput through
the tunnel.

## Protocol & Packet Handling

- **TUN mode**: `IFF_TUN | IFF_NO_PI` — raw IP packets, no Ethernet header, no PI header
- **Read chunk size**: 65536 bytes per TUN read
- **TUN send buffer**: 1 MB (set via `TUNSETSNDBUF` ioctl)
- **Message mapping**: One raw IP packet = one Pontifex `UnionDataList` message
- **Transport**: `IAckReliableRaw` (guaranteed delivery). Known inefficiency for UDP
  tunnels — V2 may use unreliable transport for UDP-in-UDP.
- **Pontifex TCP defaults**: 180s disconnect timeout, ~100 MB max message, Nagle disabled

## Project Conventions

- Target `net10.0`, no implicit usings (`<ImplicitUsings>false</ImplicitUsings>`)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Warnings treated as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- No comments in source code unless essential
- Namespaces match folder structure
- Internal implementation classes are `internal`; public APIs are `public`
- Test projects have `InternalsVisibleTo` via csproj
- Tests use NUnit 4
- Logging uses `Scriba` with `ConsoleConsumer`; verbose mode enables per-packet logging
- `PontifexPacketConverter` (static utility) handles the byte[] ↔ UnionDataList mapping
