# Action 03 — Protocol and packet primitives

## Objective
Introduce the minimal shared primitives needed by both the server and client sides of the new architecture: handshake types, binary codec, reject codes, and IPv4 packet parsing.

## Depends on
- `action-01-architecture-decisions.md`
- the protocol-oriented tests prepared in `action-02-test-safety-net.md`

## Produces
- concrete protocol/domain types under `src/PigeonPost.Bridge/Protocol/`
- a binary handshake codec used by both client and server
- an IPv4 parser that later server phases can consume directly

## Why this phase exists
The current code treats the handshake as empty and forwards opaque packet bytes. The `1-to-many` server requires structured client identity and route claims before it can safely register sessions and route packets.

## Scope
Add foundational types with unit tests and no large runtime orchestration changes yet.

This phase may wire the client-side handshake writer, but it should not yet perform the full server session-registration refactor. That remains the responsibility of `action-04-server-hub-and-session-registry.md`.

## Recommended new types
Create a small protocol area, for example:
- `src/PigeonPost.Bridge/Protocol/ClientId.cs`
- `src/PigeonPost.Bridge/Protocol/ClientHandshake.cs`
- `src/PigeonPost.Bridge/Protocol/HandshakeAck.cs`
- `src/PigeonPost.Bridge/Protocol/HandshakeRejectCode.cs`
- `src/PigeonPost.Bridge/Protocol/HandshakeCodec.cs`
- `src/PigeonPost.Bridge/Protocol/Ipv4PacketInfo.cs`
- `src/PigeonPost.Bridge/Protocol/Ipv4PacketParser.cs`

Keep namespaces aligned with folder structure.

## Handshake model for V1

### Client -> server request
The handshake request should contain:
- `clientId` (UTF-8)
- `advertisedHostIpv4` (single host route, `/32` semantics)

### Server -> client ack
The ack response should contain:
- `status` (`Accepted` or `Rejected`)
- `rejectCode` only when rejected

### Recommended reject codes
- `DuplicateClientId`
- `DuplicateHostIp`
- `InvalidHandshake`
- `UnsupportedPacketFamily` (optional for future use)
- `ServerShuttingDown`

## Recommended compact binary layout
Use a deliberately small binary payload.

### Handshake request
- 4 bytes: magic constant (for example `PPHM`)
- 1 byte: `clientIdLength`
- N bytes: UTF-8 `clientId`
- 4 bytes: IPv4 address in network byte order

### Handshake ack
- 4 bytes: magic constant (same family marker)
- 1 byte: status
- 1 byte: reject code (`0` when accepted)

## Important design notes
- Do **not** add protocol versioning in V1.
- Still use enums for status/reject codes to make future extension safer.
- Validate all lengths and enum values strictly.
- Fail closed on malformed payloads.

## IPv4 packet parser requirements
Add a minimal parser that extracts only what routing/validation needs.

### Required output
- `SourceAddress`
- `DestinationAddress`
- `HeaderLength`
- maybe `Protocol` if useful for logging

### Required behavior
- reject buffers shorter than the minimum IPv4 header length
- reject wrong version nibble
- reject invalid IHL
- do not attempt deep protocol parsing in V1

## Likely files affected
- new files under `src/PigeonPost.Bridge/Protocol/`
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs`
- `tests/PigeonPost.Bridge.Tests/Protocol/*`

## Suggested sequencing inside this phase
1. Add enums and immutable record types.
2. Add binary codec.
3. Add unit tests for codec.
4. Add IPv4 parser.
5. Add unit tests for parser.
6. Wire the client handler’s `WriteAckData(...)` to produce a real request payload.

## Success criteria
- Handshake payloads are no longer empty.
- A client can serialize `clientId` and one IPv4 host route claim.
- The server can parse and validate handshake bytes deterministically.
- IPv4 source/destination extraction is available to later routing phases.
- All new unit tests pass.

## Done means
- Later phases no longer need to invent ad-hoc handshake or packet parsing logic.
- The project has a single binary format definition for the new protocol.
- Invalid handshake buffers are rejected consistently.
- Server-side session management can now consume stable protocol primitives instead of raw ad-hoc buffers.

## Important notes for the implementer
- Keep the codec allocation-light but readability matters more than micro-optimizing the first version.
- Avoid spreading `UnionDataList` manipulation details throughout the codebase; centralize conversion helpers.
- If you need to report a reject reason to the client, the ack payload defined here is the authoritative place to encode it.

