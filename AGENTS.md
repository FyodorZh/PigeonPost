# PigeonPost — AGENTS.md

## Overview

PigeonPost is a .NET 10 console application that bridges TUN virtual network devices
over a transport layer, creating a bidirectional IP tunnel between Linux machines.
In the current V1 implementation, one server can bridge multiple concurrent clients.
It runs on .NET 10 (`net10.0`), Linux only (Debian/Ubuntu), with no implicit usings
and nullable enabled project-wide.

PigeonPost now implements the V1 `1-to-many` server model with explicit client
identity, exact-host IPv4 routing, and separate client/server runtime responsibilities.

```
(server WAN) ←routing→ (server TUN) ←→ (PigeonPost server)
                                       ↕ transport
                   ┌───────────────────┼───────────────────┐
                   ↓                   ↓                   ↓
            (PigeonPost client) (PigeonPost client) (PigeonPost client)
                   ↕                   ↕                   ↕
               (client TUN)       (client TUN)       (client TUN)
```

## Project Structure

```
PigeonPost.sln
├── src/
│   ├── PigeonPost/             Console app — entry point, CLI parsing, orchestration
│   ├── PigeonPost.Tun/         Class library — Linux TUN device I/O via P/Invoke
│   └── PigeonPost.Bridge/      Class library — client bridge, protocol, server hub, Pontifex handlers
│       ├── Protocol/           Handshake codec, client identity, IPv4 packet parsing
│       ├── Server/             Session registry and exact-host routing hub
│       └── Handlers/           Pontifex client/server handlers and acknowledger
├── tests/
│   ├── PigeonPost.Tests/       NUnit — CLI parsing, app/debug integration, reconnection
│   │   ├── App/                Debug CLI coverage
│   │   └── Integration/        Debug mode, reconnection, Linux TUN integration
│   ├── PigeonPost.Bridge.Tests/ NUnit — packet buffer, protocol, server hub, direct transport
│   │   ├── Protocol/           Handshake codec and packet parser tests
│   │   └── Server/             Session registry, routing, validation tests
│   └── PigeonPost.Tun.Tests/   NUnit — TUN device contracts, constants, P/Invoke
├── deploy/
│   ├── client/                   deploy-docker.sh + deploy-plain.sh + pre-deploy.sh
│   │   └── docker/               docker-compose.yml
│   ├── server/                   deploy-docker.sh + deploy-plain.sh + pre-deploy.sh
│   │   └── docker/               docker-compose.yml
│   └── test/docker/              docker-compose.yml + iperf harness
├── docker/                   Shared Dockerfile + docker-entrypoint.sh
├── Directory.Build.props        Global: net10.0, no implicit usings, nullable, warn-as-error
├── nuget.config                 Sources: local ./nugets + nuget.org
└── pontifex.md                  Pontifex transport library reference (1296 lines)
```

## Implementation Status

### Current V1 implementation
- Server runtime owns one TUN device and supports multiple concurrent client sessions.
- Each client session is keyed by `clientId` and one advertised IPv4 host address.
- Client runtime reconnects forever and uses one TUN device.
- Debug mode simulates one server and `N` concurrent clients using Direct transport.

### Architecture rules
Important V1 rules:
- one client == one advertised IPv4 host route
- explicit `clientId` in handshake
- duplicate `clientId` and duplicate host-IP claims are rejected
- IPv4-only server-side routing in V1
- no fallback route and no server-side buffering for missing routes
- client and server runtime responsibilities are split into separate abstractions
- invalid, malformed, unmatched, or invalid-source packets are dropped and logged
- no authentication and no backward-compatibility mode in V1

These rules are design constraints, not optional implementation details.

## Three Runtime Roles

| Role   | Behavior |
|--------|----------|
| **Server** | Listens via Pontifex for multiple simultaneous clients, bridges them to one TUN device, and routes packets by exact IPv4 host match. Rejects duplicate `clientId` and duplicate host-IP claims during handshake. |
| **Client** | Connects to a server via Pontifex, bridges to one TUN. Loops forever, reconnecting after every disconnect with a 1-second delay. |
| **Debug**  | Single-process mode that runs one server plus `N` concurrent clients using Direct transport. Uses one server TUN and one client TUN per simulated client. |

The connection model is now **1-to-many** on the server side and **single-peer** on the
client side.

## Key Design Decisions

### TUN devices are never created by the app
The app only opens existing TUN devices via `/dev/net/tun`. IP addresses, routes, and
device creation are handled externally (by `docker-entrypoint.sh` or host `pre-deploy.sh`).
This separation avoids the app needing `NET_ADMIN` for device creation — only the
container/host scripts need root.

This remains true in the current `1-to-many` design. The app continues to operate on
pre-existing TUN devices rather than creating network topology itself.

### Packets are buffered before the transport connects
The TUN reader thread starts **immediately** at launch — before any Pontifex handshake.
Outgoing packets are buffered in a bounded FIFO queue (byte-capacity capped, default
10 MB). When the buffer is full, the **newest** packets are dropped (not oldest).
This ensures the oldest queued packets get delivered first when the connection comes up.

This buffering rule applies to the client-side single-peer transport path. The multi-
client server routing path drops packets immediately
when no matching client route exists or when the selected client is unavailable.

### Incoming packets are never buffered
Packets received from Pontifex are written directly to the TUN device. No queuing on
the inbound path. This keeps latency minimal for traffic flowing from the remote side.

The same direct-write rule applies to packets accepted from clients
after source-IP validation.

### Client reconnects forever
The client role wraps its Pontifex transport in a `while (!shutdownRequested)` loop.
On disconnect it waits 1 second and creates a fresh transport. This handles network
outages and server restarts transparently. The reconnectable protocol from Pontifex is
not used — PigeonPost handles reconnection at the application level for V1.

### Client advertises its TUN IPv4 automatically
The client resolves the IPv4 address configured on its TUN interface and advertises
that address during the handshake. V1 requires exactly one IPv4 address on the client
TUN interface; zero or multiple IPv4 addresses are treated as configuration errors.

### No async/await in the data path
TUN I/O runs on a dedicated blocking thread (`PigeonPost-TunReader`). Pontifex callbacks
arrive on transport-internal threads. All shared mutable state is protected by locks.
Async is used only at the orchestration level (`RunServerAsync` / `RunClientAsync`) for
the `WaitForShutdownAsync` pattern.

### Threading model
- One dedicated `Thread` per TUN reader (named `PigeonPost-TunReader`, background)
- Pontifex callbacks arrive on transport-internal threads
- `lock(_endpointLock)` protects the client-side endpoint reference inside `Bridge`
- `lock(_lock)` protects the `PacketBuffer` queue and the `ServerHub` session registry
- Async/await only in orchestration code (`App.cs`), not in the hot path

### Graceful shutdown on SIGTERM/SIGINT
On POSIX signals, `RequestShutdown()` sets a flag + cancels a `CancellationTokenSource`.
The app then:
- **Server/Debug**: stops accepting new clients, disconnects active sessions, stops runtime components, closes TUN file descriptors.
- **Client**: stops the reconnect loop, stops the active transport, stops the bridge, closes the TUN file descriptor.

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
- **Unit tests** — run on any OS: packet buffer tests, handshake codec tests, IPv4 parser tests, server session registry tests, routing/validation tests, CLI parser tests, TUN contract tests.
- **Integration tests** — **Linux only**: `DebugModeEndToEndTests`, `ReconnectionTests`, `TunDeviceIntegrationTests`, and Direct transport integration tests covering server/client interaction.

Some integration tests require pre-created TUN devices (`tunA`, `tunB`). Run the test Docker compose to get a clean environment.

## CLI Usage

```
PigeonPost --role <server|client|debug> --tun <name> [--tun <name2> ...] --url <url>
           [--client-id <id>] [--debug-clients <N>] [options]
```

| Argument | Required | Default | Description |
|----------|----------|---------|-------------|
| `-r, --role` | Yes | — | `server`, `client`, or `debug` |
| `-t, --tun` | Server/Client: once. Debug: optional/repeatable | Debug: auto-generated if omitted | TUN device name(s) |
| `-u, --url` | Yes | — | Pontifex transport URL (e.g. `tcp\|10.0.0.1:9000/30`) |
| `--client-id` | Client only | — | Required client identity sent during handshake |
| `--debug-clients` | Debug only | `1` | Number of concurrent debug clients |
| `-b, --buffer-size` | No | `10485760` (10 MB) | Outgoing packet buffer in bytes (1500–1,073,741,824) |
| `-v, --verbose` | No | `false` | Log every packet size |
| `-h, --help` | No | — | Show man-page help |

### Transport URL format
- **TCP**: `tcp|ip:port/timeout_seconds` — e.g. `tcp|0.0.0.0:9000/30`
- **Direct**: `direct|server_name` — e.g. `direct|ep_debug` (debug mode only)

### Examples
```bash
# Server on TCP port 9000, bridging tun0 and accepting multiple clients
PigeonPost --role server --tun tun0 --url 'tcp|0.0.0.0:9000/30'

# Client connecting to the server, bridging tun1
PigeonPost --role client --client-id office-a --tun tun1 --url 'tcp|10.0.0.1:9000/30'

# Debug mode with three clients
PigeonPost --role debug --debug-clients 3 --url 'direct|ep_debug'
```

## NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Pontifex` | 0.1.2-dev.0 | Core transport abstractions, data types, FSM |
| `Pontifex.Transport.Tcp` | 0.1.1-dev.0 | TCP network transport |
| `Pontifex.Transport.Direct` | 0.1.1-dev.0 | In-process zero-copy transport |
| `Scriba` | 0.2.3-dev.0 | Structured logging |
| `Scriba.JsonFactory` | 0.2.1-dev.0 | JSON log formatting |
| `Actuarius.Memory` | 0.1.3-dev.0 | Memory pooling (transitive) |
| `Actuarius.Collections` | 0.1.3-dev.0 | Collections (transitive) |
| `Actuarius.Concurrent` | 0.1.4-dev.0 | Concurrency primitives (transitive) |
| `Operarius` | 0.2.2-dev.0 | Scheduling (transitive) |
| `System.Reactive` | 6.1.0 | Reactive extensions (transitive) |
| `NUnit` | 4.2.2 | Test framework |
| `NUnit3TestAdapter` | 4.6.0 | Test adapter |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | Test SDK |
| `Microsoft.CodeCoverage` | 17.12.0 | Code coverage (transitive) |
| `Microsoft.TestPlatform.ObjectModel` | 17.12.0 | Test platform (transitive) |
| `Microsoft.TestPlatform.TestHost` | 17.12.0 | Test host (transitive) |
| `Newtonsoft.Json` | 13.0.1 | JSON (transitive) |

All packages (including transitive dependencies from nuget.org) are bundled in the
**local NuGet feed** at `./nugets`. This enables fully offline builds and Docker builds
without internet access. The `nuget.config` lists both the local feed and nuget.org.

**Important:** Whenever a package reference in any `.csproj` file is added, removed, or
version-bumped, the local `./nugets/` directory must be updated to match. Run the
following to refresh it:

```bash
# 1. Download all packages (including transitive) to a temp directory
dotnet restore --packages /tmp/nuget-fresh

# 2. Copy the .nupkg files to the local feed
find /tmp/nuget-fresh -name '*.nupkg' -exec cp {} ./nugets/ \;

# 3. Verify the solution restores offline
dotnet nuget locals all --clear
dotnet restore --packages /tmp/nuget-verify

# 4. Commit the updated ./nugets/ directory
```
The local feed must always contain every package the solution needs — no missing
transitive dependencies allowed.

## Deploy Configuration

### Shared Dockerfile (`docker/Dockerfile`)
All roles share the same multi-stage build:
- **Build stage**: `dotnet/sdk:10.0` — copies local nugets, adds source, restores, publishes to `/app`
- **Runtime stage**: `dotnet/runtime:10.0` — installs `iproute2`, copies app + entrypoint
- Entrypoint: `docker-entrypoint.sh` which creates/configures the TUN device, then execs the app

### Shared entrypoint script (`docker/docker-entrypoint.sh`)
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
| `deploy/server/deploy-docker.sh` | Server | `tcp\|0.0.0.0:9000/30` |
| `deploy/server/deploy-plain.sh` | Server | `tcp\|0.0.0.0:9000/30` |
| `deploy/client/deploy-docker.sh` | Client | `tcp\|203.0.113.10:9000/30` |
| `deploy/client/deploy-plain.sh` | Client | `tcp\|203.0.113.10:9000/30` |

Client deployment artifacts accept `CLIENT_ID` and pass it to `--client-id`. Current
client helper scripts default `CLIENT_ID` to `pp-client-1` if it is not set explicitly.

### Idempotency Requirements

All deployment and setup scripts must be **fully idempotent** — running them multiple
times must produce the same state as running them once, with no errors and no duplicate
rules or entries.

**`pre-deploy.sh` scripts** — TUN creation, IP assignment, routing, NAT, ipset setup,
policy routing. Every operation must check existence before acting.

**`deploy-*.sh` scripts** — Docker build/up or `dotnet publish` + TUN setup. Rebuild is
acceptable; TUN setup must be guarded; container/service start must handle already-running
state.

**Ingress scripts** (`deploy/client/ingress/`) — Register traffic sources into the
`pp-ingress` ipset. Adding an already-registered entry must be a no-op (`-exist` flag).

Every change to any of these scripts must be verified for idempotency: run the script
twice and confirm the second run produces zero errors and no duplicate rules or entries.

### Host setup scripts

- **Server** (`deploy/server/pre-deploy.sh`): Creates TUN, enables IP forwarding, NAT from tunnel
  subnet to WAN interface (`POSTROUTING -o $WAN_IF -s 10.0.0.0/30`).
- **Client** (`deploy/client/pre-deploy.sh`): Creates TUN, enables IP forwarding, NAT to tunnel
  (`POSTROUTING -o tun0`), sets up ipset `pp-ingress` + mangle rule referencing it, sets up policy
  routing table 234.

### Ingress scripts (`deploy/client/ingress/`)
Ingress scripts register traffic sources (e.g. LAN subnet, PPTP pool, WireGuard peers) into the
`pp-ingress` ipset. Packets with source IP matching an entry in this set get marked with fwmark 1
and routed through the PigeonPost tunnel.

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
receive `OnConnected`. PigeonPost uses a compact binary handshake carrying
`clientId` and one advertised IPv4 host route. The server acknowledger rejects
duplicate identities, duplicate host-IP claims, malformed handshakes, and shutdown-time
connections, and reports rejection reasons to the client via a compact ack payload.

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

In the current V1 implementation, each client advertises exactly one remote host and
the server routes by exact host match.

### Secondary: Local development/debugging
Debug mode runs one server and `N` clients in a single process using Direct transport —
no network required. Useful for testing packet flow, routing, rejection behavior, and
transport integration without deploying to multiple machines.

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

Current V1 packet rules:
- server-side routing is IPv4-only in V1
- each client owns exactly one advertised IPv4 host address
- client->server packets must have source IP equal to the advertised host IP
- unmatched, malformed, non-IPv4, or invalid-source packets are dropped and logged

## Operational Guidance

When extending the current V1 architecture, preserve these invariants:
- keep `clientId` ownership and advertised host-IP ownership explicit
- keep routing exact-host and IPv4-only unless the protocol is intentionally expanded
- keep duplicate identity and duplicate host-IP rejection deterministic
- keep missing-route and invalid-source behavior as drop-and-log unless a new design explicitly replaces it
- keep deployment artifacts and CLI behavior equivalent across Docker and plain-host flows
- add tests before broadening routing, handshake, or runtime responsibilities

## Project Conventions

- Target `net10.0`, no implicit usings (`<ImplicitUsings>false</ImplicitUsings>`)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Warnings treated as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- No comments in source code unless essential
- Namespaces do not mirror directory structure — use a flat namespace per project (e.g. all types in `src/PigeonPost.Bridge/` use `namespace PigeonPost.Bridge;` regardless of subdirectory)
- Internal implementation classes are `internal`; public APIs are `public`
- Test projects have `InternalsVisibleTo` via csproj
- Tests use NUnit 4
- Logging uses `Scriba` with `ConsoleConsumer`; verbose mode enables per-packet logging
- `PontifexPacketConverter` (static utility) handles the byte[] ↔ UnionDataList mapping
