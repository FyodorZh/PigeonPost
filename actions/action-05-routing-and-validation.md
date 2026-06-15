# Action 05 — Routing and validation

## Objective
Implement deterministic packet routing from the server TUN to the correct client and strict source validation for packets coming from clients.

## Depends on
- `action-01-architecture-decisions.md`
- `action-03-protocol-and-packet-primitives.md`
- `action-04-server-hub-and-session-registry.md`

## Produces
- exact host-route lookup and packet delivery behavior in `ServerHub`
- strict inbound source validation tied to session ownership
- explicit drop/log behavior for all unsupported routing cases

## Why this phase exists
Once multiple client sessions can coexist, the next hard problem is deciding which client should receive each packet and ensuring a client cannot send traffic claiming to be another client.

## Scope
Add exact host-route lookup, outbound drop policy, and inbound source validation using the V1 single-host model.

This phase consumes the server-session model from the previous phase. It should not redesign handshake acceptance or CLI/orchestration concerns.

## Routing model for V1
- Each connected client advertises exactly one IPv4 host address.
- The server treats that host address as a `/32` route.
- When the server TUN produces a packet:
  - parse IPv4 destination address,
  - find the session whose advertised host IP matches exactly,
  - send to that client only.
- If no match exists: drop and log.

## Inbound validation model for V1
- When a client sends a packet to the server:
  - parse IPv4 source address,
  - compare it to the session’s advertised host IP,
  - if equal, accept and write to TUN,
  - otherwise drop and log.

## Outbound buffering policy
### Server side
Do **not** buffer per-route or globally for missing/unavailable clients in V1.

Behavior:
- no matching route => drop
- matching route but endpoint unavailable => drop
- `Send(...)` returns failure => drop

### Client side
The existing client-side pre-connection buffer may remain for the single client->server transport path, but server-side routing should not depend on it.

## Recommended `ServerHub` methods
Example conceptual API:
- `OnPacketFromTun(byte[] packet)`
- `OnPacketFromClient(ClientId clientId, byte[] packet)`
- `TryRegisterSession(...)`
- `RemoveSession(ClientId clientId)`
- `StopAll(StopReason reason)`

## Logging expectations
Logs should include enough context to operate the system:
- `clientId`
- advertised host IP
- drop reason (`NoRoute`, `InvalidSource`, `SendFailed`, `MalformedIpv4`, `NonIpv4`)

Add counters if practical, but keep V1 simple.

## Likely files affected
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Protocol/Ipv4PacketParser.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs`
- maybe `src/PigeonPost.Bridge/PacketBuffer.cs` only if server-side use is intentionally reduced
- tests under `tests/PigeonPost.Bridge.Tests/Server/`

## Suggested sequencing inside this phase
1. Add exact host-route lookup in `ServerHub`.
2. Route server-TUN packets to the chosen client.
3. Add inbound source validation for client packets.
4. Add structured log messages for every drop path.
5. Confirm direct-transport tests for multi-client delivery pass.

## Success criteria
- A server packet addressed to client A’s host IP is sent only to client A.
- A packet addressed to an unknown host IP is dropped and logged.
- A client packet with the wrong source IP is dropped and logged.
- A malformed or non-IPv4 packet is dropped and logged.
- One client’s traffic never reaches another client unless explicitly addressed to that client’s host IP.

## Done means
- The server has a fully defined routing rule, not just multiple open connections.
- Packet ownership and validation are tied to session identity.
- The routing behavior matches the V1 single-host model exactly.
- Later runtime and deployment phases can treat routing semantics as stable.

## Important notes for the implementer
- Do not broaden this phase into general subnet routing.
- Keep the route key exact and explicit; no longest-prefix logic is needed in V1.
- Never hold server registry locks across TUN writes or endpoint sends.
- If logging becomes noisy during tests, consider test log filtering rather than weakening validation.

