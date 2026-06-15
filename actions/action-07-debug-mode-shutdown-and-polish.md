# Action 07 — Debug mode, shutdown, and polish

## Objective
Finish the `1-to-many` migration by exercising multi-client debug mode, tightening shutdown behavior, and updating tests/documentation to match the final architecture.

## Depends on
- `action-02-test-safety-net.md`
- `action-04-server-hub-and-session-registry.md`
- `action-05-routing-and-validation.md`
- `action-06-client-runtime-and-cli-refactor.md`

## Produces
- a representative in-process debug topology for multi-client development
- finalized runtime shutdown ordering
- final debug-oriented integration coverage and observability polish

## Why this phase exists
After the core protocol, server hub, routing, and client refactor are in place, the system still needs a practical way to test multiple clients locally and a clean operational finish around shutdown and observability.

## Scope
- multi-client debug mode
- graceful shutdown behavior
- final integration tests
- updated debug/runtime-facing help text and docs
- sanity review of drop/log behavior

Repository-level deployment/operator documentation remains the responsibility of `action-08-deploy-and-ops-update.md`.

## Debug mode design

### Goal
Allow one process to simulate one server and `N` concurrent clients using Direct transport.

### Recommended behavior
- `--role debug --debug-clients N`
- create one direct server transport
- create `N` direct client transports
- each client gets:
  - its own `clientId`
  - its own client bridge
  - its own TUN name
  - its own advertised host IPv4
- one shared server hub receives all client sessions

### Default identities
Auto-generate predictable debug IDs:
- `debug-client-1`
- `debug-client-2`
- ...

### Default TUN names
If not provided explicitly:
- `tunServer`
- `tunClient1`
- `tunClient2`
- ...

## Graceful shutdown behavior
For server/debug shutdown:
1. mark shutdown requested,
2. stop accepting new sessions,
3. disconnect all active clients,
4. stop bridges/hub,
5. close TUN handles,
6. exit.

For client shutdown:
1. stop reconnect loop,
2. stop active transport,
3. stop bridge,
4. close TUN handle.

No packet draining is required for disconnected/missing routes in V1 because the routing policy is immediate drop.

## Observability and logging polish
At minimum, ensure logs can answer:
- which `clientId` connected/disconnected
- which host IP each client claimed
- why a handshake was rejected
- why a packet was dropped
- which packets were routed to which client in verbose mode

If feasible, add lightweight counters for:
- accepted sessions
- rejected sessions by reason
- routed packets by client
- dropped packets by reason

## Final test coverage expectations
Add or complete integration tests for:
- debug mode with 3 clients connected simultaneously
- packet routed from server to the correct debug client
- duplicate `clientId` rejected in debug/direct transport
- client disconnect does not affect sibling clients
- graceful shutdown disconnects all clients cleanly

Also re-run the earlier unit suites after the debug mode additions.

## Likely files affected
- `src/PigeonPost/App.cs`
- `src/PigeonPost/CliParser.cs`
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- `tests/PigeonPost.Tests/Integration/*`
- `tests/PigeonPost.Bridge.Tests/Pontifex/*`
- possibly `AGENTS.md` or project docs if you want the architecture note recorded there later

## Suggested sequencing inside this phase
1. Extend debug CLI parsing.
2. Implement multi-client debug orchestration.
3. Add debug-mode integration tests.
4. Tighten graceful shutdown ordering.
5. Review logs/help text for stale single-client wording.

## Success criteria
- Debug mode can exercise more than one client at the same time.
- Server/debug shutdown disconnects all sessions cleanly.
- Final integration tests prove the system works in the intended multi-client topology.
- User-facing help/docs no longer describe the server as single-client.

## Done means
- The project can be developed and demoed locally in the same architectural shape as production.
- The operational story is complete enough for a first V1 release.
- There are no leftover references that imply the old single-peer model is still the primary design.
- The runtime is ready for deployment rollout without further debug-model redesign.

## Important notes for the implementer
- Keep debug mode representative, but do not let it dictate production abstractions.
- Prefer deterministic generated IDs/names in tests so failures are easier to understand.
- Review every shutdown path for double-stop and thread-join hazards while the code is still fresh.

