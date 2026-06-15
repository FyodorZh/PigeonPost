# Action 06 — Client runtime and CLI refactor

## Objective
Separate client-side runtime concerns from the new server hub model and update the CLI/app orchestration to support explicit `clientId` and debug mode sizing.

## Depends on
- `action-01-architecture-decisions.md`
- `action-03-protocol-and-packet-primitives.md`
- `action-04-server-hub-and-session-registry.md`
- `action-05-routing-and-validation.md`

## Produces
- the permanent runtime split between client-side and server-side bridging logic
- CLI/configuration support for `clientId` and debug client count
- safer lifecycle behavior in `App`, bridge shutdown, reconnect cleanup, and TUN writes

## Why this phase exists
The current `Bridge` abstraction mixes together client-side packet buffering, a single endpoint, and server usage. The new architecture needs:
- a dedicated client-side bridge/runtime,
- a dedicated server hub/runtime,
- explicit client identity in the CLI and handshake,
- a clean orchestration layer in `App`.

## Scope
Refactor the runtime entry points and CLI so the server and client roles no longer share the same core type.

## Recommended runtime split

### Client side
Rename or replace the current single-peer bridge with something explicitly client-scoped, for example:
- `ClientBridge`
- or `SinglePeerBridge`

Responsibilities:
- open/read/write one TUN
- buffer outbound packets before transport connection if needed
- hold one current server endpoint
- reconnect behavior remains in `App`

### Server side
`ServerHub` becomes the server runtime abstraction.

Responsibilities:
- own one TUN
- manage multiple sessions
- route packets to a chosen client
- validate inbound packets

## CLI changes

### Client role
Add a required argument:
- `--client-id <id>`

This value becomes the handshake identity.

### Debug role
Add a new argument:
- `--debug-clients <N>`

Recommended semantics:
- default `N = 1`
- debug mode creates 1 server + `N` clients in-process
- if `--tun` is not provided, auto-generate names such as:
  - server: `tunServer`
  - clients: `tunClient1`, `tunClient2`, ...
- if `--tun` is provided in debug mode, require exactly `N + 1` names

### Help text
Update `CliParser` help so it no longer describes server mode as single-client.

This phase owns the **application CLI contract**. Later deployment work in `action-08-deploy-and-ops-update.md` must consume that contract, not redefine it.

## How client route advertisement should work
Because the user does not want route CLI arguments, the client must discover its own TUN IPv4 address automatically.

### Recommended helper
Add a small Linux-only interface-address resolver, for example:
- `src/PigeonPost.Tun/TunIpv4AddressResolver.cs`

Behavior:
- find the configured IPv4 address for the opened TUN interface name
- fail startup if none is configured
- fail startup if more than one IPv4 address is configured on that interface

This address is what the client advertises in the handshake.

## `App` orchestration changes

### `RunServerAsync()`
- create `ServerHub`
- initialize the server acknowledger with the hub
- start the hub and server transport
- on shutdown: stop accepting, disconnect all sessions, close TUN, exit

### `RunClientAsync()`
- create `ClientBridge`
- resolve TUN IPv4 before or during handshake setup
- pass `clientId` + host IP into the client handler
- keep reconnect loop behavior
- ensure each transport instance is stopped/disposed properly on each iteration

### `RunDebugAsync()`
- create one direct server
- create `N` direct clients
- auto-generate client IDs like `debug-client-1`, `debug-client-2`, ... unless later extended
- create `N + 1` bridges/TUN bindings

## Also fix existing runtime problems while refactoring
This phase should absorb the current cleanup items already found in the codebase:
- make stop paths idempotent
- avoid self-join on the TUN reader thread
- ensure client reconnect loop stops/disposes old transport instances
- detect short TUN writes
- remove warning-producing unused code in `App`

## Likely files affected
- `src/PigeonPost/App.cs`
- `src/PigeonPost/CliParser.cs`
- `src/PigeonPost/BridgeConfiguration.cs`
- `src/PigeonPost.Bridge/Bridge.cs` (rename/replace)
- `src/PigeonPost.Bridge/IBridge.cs` (likely removed or split)
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- `src/PigeonPost.Tun/TunDevice.cs`
- new resolver file(s) under `src/PigeonPost.Tun/`
- CLI tests under `tests/PigeonPost.Tests/`

## Suggested sequencing inside this phase
1. Extend configuration model with `ClientId` and `DebugClientCount`.
2. Update CLI parsing and help text.
3. Introduce TUN IPv4 discovery helper.
4. Split client runtime from server runtime.
5. Refactor `App` to use the new types.
6. Fold in the stop/dispose/write-safety fixes.

## Success criteria
- Client role requires and uses `--client-id`.
- The client handshake advertises the discovered TUN IPv4 address.
- The server role no longer depends on the old single-peer bridge abstraction.
- Debug mode can model `N` concurrent clients.
- `App` no longer contains warning-level issues that would violate project settings.

## Done means
- Runtime responsibilities are explicit and maintainable.
- The CLI can express the new architecture clearly.
- The client is able to self-describe its host route without extra route arguments.
- The codebase is ready for deployment-script updates without further CLI churn.

## Important notes for the implementer
- Keep CLI behavior strict; fail fast on ambiguous debug arguments.
- Avoid making Linux address discovery a shell-out to `ip` if a managed or P/Invoke approach is practical; it is more testable and self-contained.
- When renaming core types, update tests immediately to avoid drifting nomenclature.

