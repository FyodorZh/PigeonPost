# Action 01 — Architecture decisions

## Objective
Lock the V1 `1-to-many` architecture decisions before refactoring code so every later phase targets the same server/client model and avoids rework.

## Phase type
Decision-record phase. This action does **not** primarily produce runtime code. It produces the architectural rules that later implementation phases must follow.

## Depends on
- product decisions already captured from the user
- current repository constraints from `AGENTS.md`

## Produces
- a locked V1 routing model
- a locked V1 handshake contract at the conceptual level
- acceptance/rejection rules for sessions
- scope boundaries for all later actions

## Why this phase exists
The current codebase is built around a single endpoint (`1-to-1`). The `1-to-many` design needs a few explicit choices up front because they affect the protocol, server state model, CLI, tests, and shutdown behavior.

## Adopted decisions for V1

### Confirmed by product direction
- Client identity is an explicit `clientId` sent during handshake.
- Duplicate `clientId` connections are rejected. The new connection must not replace the existing one.
- No authentication in V1.
- Each client represents a single remote host.
- Server has one TUN device.
- Each client has one TUN device.
- No default route / fallback client.
- If no route matches, drop and log.
- If a target client is unavailable or `Send(...)` fails, drop and log.
- Server accepts new clients continuously; no fixed connection cap in V1.
- On graceful shutdown, server stops accepting, disconnects all clients, exits.
- No session recovery on reconnect in V1.
- Handshake metadata is the source of routing information.
- Stay CLI-only in V1.
- No backward compatibility with the old `1-to-1` behavior.
- Compact binary handshake format is preferred.
- No protocol versioning field in V1.
- A larger clean architectural split is preferred over a minimal patch.
- Test-first delivery is preferred.

### Recommended answers to previously open questions

#### Q5 — How should routes be supplied?
**Decision:** Use handshake-driven route advertisement, but because one client represents a single remote host, the client advertises exactly **one IPv4 host route (`/32`)**.

**Reasoning:**
- The user does not want a config file.
- The user does not want route CLI flags.
- The server still needs a deterministic mapping from destination IP to client.
- A single-host model is the simplest V1 that still supports multiple clients.

**Implementation consequence:**
- The handshake contains `clientId` + the client TUN’s IPv4 address.
- The server registers an exact host route: `clientTunIpv4 -> clientId`.
- There is no general CIDR routing table in V1; only exact host routes.

#### Q6 — Can routes overlap?
**Decision:** No overlaps allowed in V1.

**Reasoning:**
- In the chosen single-host model, overlap means two clients claim the same host IP.
- Allowing ties would complicate routing, troubleshooting, and correctness.

**Implementation consequence:**
- Reject a client if another connected client already advertises the same host IP.
- Report the reason to the client as a compact reject code.

#### Q9 — Should the server validate packets from clients?
**Decision:** Yes, strict validation.

**Reasoning:**
- Each client owns exactly one host route.
- The simplest safe rule is: packets received from a client must have a source IPv4 equal to that client’s advertised host IP.
- This prevents accidental spoofing and catches broken deployments early.

**Implementation consequence:**
- On every packet received from a client, parse IPv4 source address.
- If the packet is not IPv4, malformed, or the source does not equal the advertised host IP, drop and log.

#### Q10 — What should happen on validation failure?
**Decision:** Drop and log; do **not** disconnect in V1.

**Reasoning:**
- No authentication is used in V1, so disconnecting may create noisy reconnect loops for simple misconfiguration.
- Drop-and-log is safer for first release and easier to operate.

**Implementation consequence:**
- Add counters/log entries for invalid inbound packets.
- Keep the session alive unless the transport itself stops.

### Additional V1 decisions needed by implementers

#### IP family
**Decision:** Support **IPv4 only** in V1 multi-client routing.

**Reasoning:**
- Handshake advertises a single IPv4 host route.
- The packet parser and route table stay simple.
- IPv6 can be added in a later version with a new protocol shape.

**Implementation consequence:**
- Non-IPv4 packets read from the server TUN are dropped and logged.
- Non-IPv4 packets received from clients are dropped and logged.

#### How does the client know which host IP to advertise?
**Decision:** The client auto-discovers the IPv4 address configured on its TUN interface and sends that value in the handshake.

**Reasoning:**
- The app does not create/configure TUN addresses.
- The user does not want route CLI flags.
- The server still needs an explicit route claim.

**Implementation consequence:**
- Add a small Linux-only TUN IPv4 discovery helper.
- Fail startup if the client TUN has zero or multiple IPv4 addresses.

#### How should duplicate `clientId` rejection be reported to the client?
**Decision:** Use a compact server ack payload with a status code. If the Pontifex hard-reject path cannot carry a reason, use a **soft reject** pattern:
1. handshake is accepted far enough to send an ack response,
2. ack response contains a reject code,
3. server disconnects immediately,
4. client logs a clear reason and refuses to enter the normal connected state.

**Reasoning:**
- The user explicitly wants the reason reported to the client.
- Pontifex `AckRejected` may be too generic by itself.

## Proposed new concepts
- `ClientId`
- `ClientHandshake`
- `HandshakeAck`
- `HandshakeRejectCode`
- `Ipv4PacketInfo`
- `ServerHub`
- `ClientSession`
- `ClientBridge`

## Non-goals for V1
- Subnet routing behind a client
- Overlapping route resolution
- Authentication or authorization
- IPv6 routing
- Persisted session state
- Backward compatibility mode

## Success criteria
- Every later phase can reference this file without re-opening design questions.
- There is a single unambiguous routing model: **one client == one IPv4 host route**.
- Duplicate identity and duplicate host-route behavior are fully specified.
- The implementer knows that the current `Bridge` abstraction is not the target server abstraction.

## Done means
- later phases can implement against this document without inventing missing semantics
- no later phase needs to reinterpret the meaning of `clientId`, host-route ownership, or validation policy
- the team treats this action as a prerequisite, not as a code-delivery phase

## Important notes for the implementer
- Do not attempt to evolve the existing `Bridge` into a multi-client server.
- Split client-side and server-side responsibilities early.
- Keep the protocol binary and minimal, but leave room for future extension via enums and reserved fields.
- Treat this document as the source of truth for V1 behavior unless later phases update it explicitly.

