# Action 04 — Server hub and session registry

## Objective
Replace the server-side single-endpoint model with a real multi-client server hub that tracks sessions by `clientId` and advertised host IP.

## Depends on
- `action-01-architecture-decisions.md`
- `action-02-test-safety-net.md`
- `action-03-protocol-and-packet-primitives.md`

## Produces
- a session registry keyed by `clientId` and advertised host IP
- server-side handler flow that can accept or reject each client deterministically
- explicit separation between connection management and later packet-routing logic

## Why this phase exists
The current server path stores one endpoint in `Bridge`. That is the main blocker for `1-to-many`. The server needs a dedicated abstraction that owns:
- active sessions
- duplicate checks
- connect/disconnect bookkeeping
- targeted packet delivery decisions

## Scope
Refactor the server-side runtime around explicit session objects and a hub/registry.

This phase owns **session lifecycle**. It should not expand into full packet-routing semantics beyond what is needed to prove session registration and rejection behavior. Full routing/validation behavior belongs to `action-05-routing-and-validation.md`.

## Recommended new abstractions
Create new server-specific types, for example:
- `src/PigeonPost.Bridge/Server/IServerHub.cs`
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Server/ClientSession.cs`
- `src/PigeonPost.Bridge/Server/SessionRegistrationResult.cs`

### Suggested responsibilities
#### `ServerHub`
- owns the server-side TUN
- tracks active client sessions
- registers/unregisters sessions
- handles packets from the server TUN
- handles packets from clients
- disconnects all sessions on shutdown

#### `ClientSession`
- `ClientId`
- advertised host IPv4
- endpoint reference
- connection timestamp (optional but useful for logs)
- per-session counters (optional)

## Registry model
Use two maps protected by a lock:
- `Dictionary<ClientId, ClientSession>`
- `Dictionary<uint, ClientId>` or equivalent IPv4-key map for exact host routing

The second map exists to make destination-IP lookup cheap and deterministic.

## Handshake acceptance flow

### Goal
Reject duplicates **before** a normal connected session is established, while still reporting the reason to the client.

### Recommended approach
Implement the server acknowledger so it:
1. decodes the client handshake,
2. checks for duplicate `clientId`, duplicate host IP, or invalid payload,
3. returns a handler carrying the precomputed handshake outcome,
4. that handler emits an ack response containing either `Accepted` or a reject code,
5. if rejected, `OnConnected(...)` immediately disconnects the endpoint and never registers the session.

This is the preferred **soft reject** pattern when a pure hard-reject cannot communicate the reason.

## Handler changes
Refactor current server handler logic so every callback is bound to a specific proposed client identity.

### Recommended shape
- `BridgeServerAcknowledger` parses handshake and creates a per-connection handler.
- `BridgeServerHandler` stores either:
  - a successful `ClientHandshake`, or
  - a reject decision.
- `OnConnected(...)` registers only accepted sessions.
- `OnDisconnected(...)` removes only that session.
- `OnReceived(...)` passes packets to `ServerHub.OnPacketFromClient(clientId, packet)`.

## Concurrency notes
- A client disconnect must remove only its own session.
- One client failing must not clear other clients.
- Keep lock scopes tight; do not hold a registry lock while calling `Send(...)` or writing to TUN.
- Session registration and lookup should be deterministic and idempotent.

## Likely files affected
- `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs`
- `src/PigeonPost.Bridge/Bridge.cs` (likely replaced or server usage removed)
- new files under `src/PigeonPost.Bridge/Server/`
- tests under `tests/PigeonPost.Bridge.Tests/Server/`
- direct transport tests under `tests/PigeonPost.Bridge.Tests/Pontifex/`

## Suggested sequencing inside this phase
1. Introduce `ClientSession` and registry data structures.
2. Build pure registration logic with unit tests.
3. Refactor server acknowledger to parse the handshake.
4. Carry ack status/reject code to the client.
5. Register accepted sessions only.
6. Remove the server’s dependency on a singular endpoint field.

## Success criteria
- Two or more clients can connect simultaneously to one server instance.
- Duplicate `clientId` connections are rejected without affecting the already connected client.
- Duplicate advertised host IP connections are rejected.
- Disconnecting one client removes only its own session.
- The server keeps accepting new clients after others are already connected.

## Done means
- The server no longer relies on the old `Bridge` as its primary endpoint holder.
- Session identity is explicit everywhere on the server side.
- Multi-client connection management works before packet routing logic becomes complex.
- The server has a stable foundation that later phases can use without reopening handshake acceptance rules.

## Important notes for the implementer
- Treat this as the real architectural pivot point of the project.
- Avoid leaking client-session logic into client-side classes.
- If you need a temporary compatibility shim during refactor, keep it internal and short-lived; do not let it become the new public architecture.

