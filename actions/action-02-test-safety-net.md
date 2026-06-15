# Action 02 — Test safety net

## Objective
Create a test-first safety net for the new multi-client architecture before modifying runtime code.

## Depends on
- `action-01-architecture-decisions.md`

## Produces
- failing or pending tests that define the target behavior of the new architecture
- refactored test structure that no longer enforces the old single-endpoint server model

## Why this phase exists
The current tests mainly encode `1-to-1` assumptions. Without a new test layer, refactoring toward `1-to-many` will either break behavior silently or force implementers to keep the wrong abstractions alive.

## Scope
Add or reorganize tests so they describe the target V1 behavior:
- multiple concurrent clients on one server
- explicit client identity
- duplicate client rejection
- exact host-route mapping
- strict source validation
- drop-and-log policies
- debug mode with `N` clients

## Suggested test structure
Create focused test groups under `tests/PigeonPost.Bridge.Tests/` and `tests/PigeonPost.Tests/`.

### New / updated test areas
- `Protocol/HandshakeCodecTests.cs`
- `Protocol/Ipv4PacketInfoTests.cs`
- `Server/ClientSessionRegistryTests.cs`
- `Server/ServerHubRoutingTests.cs`
- `Server/DuplicateClientRejectionTests.cs`
- `Server/InboundSourceValidationTests.cs`
- `App/DebugCliTests.cs`
- `Integration/MultiClientDirectTransportTests.cs`

When a test area depends on runtime pieces from a later phase, it is still acceptable in this phase to add the file with pending/failing tests or to stage the test shape first and finish assertions when the runtime becomes available.

## Implementation details

### 1. Add protocol round-trip tests first
Test compact handshake request/ack encoding and decoding before any server logic is added.

Must cover:
- valid `clientId` round-trip
- valid IPv4 address round-trip
- invalid buffer lengths
- invalid `clientId` length
- unknown ack status / reject code values

### 2. Add packet parser tests
Add tests for a small IPv4 parser utility.

Must cover:
- valid IPv4 packet with correct source/destination extraction
- too-short packet
- invalid version nibble
- invalid IHL
- non-IPv4 payload rejected

### 3. Add server-session tests using fakes
Write tests for pure in-memory server session bookkeeping before transport integration.

Must cover:
- add two distinct clients successfully
- reject duplicate `clientId`
- reject duplicate advertised host IP
- remove only the disconnected client
- keep other clients active after one disconnects

### 4. Add routing policy tests
Write tests against a future `ServerHub` abstraction.

Must cover:
- server packet destined to client A goes only to A
- no matching host route => dropped
- send failure => dropped
- disconnected client => dropped
- no broadcast behavior

### 5. Add strict source-validation tests
Must cover:
- client packet source equals advertised IP => accepted
- client packet source differs => dropped
- malformed IPv4 => dropped
- IPv6 / non-IPv4 => dropped

### 6. Add direct-transport integration tests
Use Pontifex Direct transport to exercise the real handshake and multi-client flow without Linux TUN dependencies where possible.

Must cover:
- server accepts 2–3 clients simultaneously
- duplicate `clientId` is reported to the client
- traffic to each advertised host reaches only the correct client
- one client disconnect does not break others

The more advanced debug-mode orchestration scenarios are completed in `action-07-debug-mode-shutdown-and-polish.md`; this phase only needs enough coverage to protect the core server/client architecture.

### 7. Update existing `1-to-1` tests intentionally
Existing tests that assert singular endpoint state should either:
- be moved to client-only coverage, or
- be replaced with server-session-count assertions.

Do **not** keep tests that force the new server implementation to expose a fake single-endpoint API.

## Likely files affected
- `tests/PigeonPost.Bridge.Tests/BridgeTests.cs`
- `tests/PigeonPost.Bridge.Tests/Pontifex/PontifexDirectTransportTests.cs`
- `tests/PigeonPost.Tests/CliParserTests.cs`
- new test files under `tests/PigeonPost.Bridge.Tests/Protocol/`
- new test files under `tests/PigeonPost.Bridge.Tests/Server/`
- new test files under `tests/PigeonPost.Tests/Integration/`

## Suggested sequencing inside this phase
1. Add protocol tests.
2. Add packet parser tests.
3. Add pure server-registry tests.
4. Add routing/validation tests.
5. Add transport integration tests.
6. Only then begin production refactoring.

## Success criteria
- The solution still builds.
- New tests compile and express the target architecture clearly.
- There are tests for every major V1 rule from `action-01-architecture-decisions.md`.
- Existing test suites no longer assume a singular server endpoint.
- The test plan is sequenced so later runtime phases can implement against it without ambiguity.

## Done means
- An implementer can refactor the runtime while continuously checking correctness against the new target model.
- Duplicate client and routing behavior are specified by tests, not only by prose.
- The project has at least one integration-level test proving multiple simultaneous clients are possible.

## Important notes for the implementer
- Prefer small fake objects for unit tests and Pontifex Direct transport for integration tests.
- Keep Linux-specific real-TUN tests separate from protocol/routing tests.
- Avoid hard sleeps when possible; prefer signaling primitives in new tests.
- If a new abstraction is hard to test, that is a design smell—refactor the abstraction rather than weakening the test.

