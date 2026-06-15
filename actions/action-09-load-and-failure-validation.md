# Action 09 — Load and failure validation

## Objective
Stress the new `1-to-many` design under realistic concurrency, reconnect, and failure scenarios so V1 behavior is proven beyond correctness in happy-path unit and integration tests.

## Depends on
- `action-04-server-hub-and-session-registry.md`
- `action-05-routing-and-validation.md`
- `action-06-client-runtime-and-cli-refactor.md`
- `action-07-debug-mode-shutdown-and-polish.md`
- `action-08-deploy-and-ops-update.md` if deployment-level validation is included

## Produces
- final confidence that the implementation behaves correctly in its intended 2–5 client envelope
- regression tests and/or harness runs that specifically target concurrency and failure modes
- evidence for release-readiness decisions

## Why this phase exists
The earlier phases establish correctness of the architecture and rollout. They do not fully answer whether the implementation remains stable when multiple clients connect, disconnect, reject, and exchange traffic concurrently.

This phase is the confidence-building step before treating the new server hub as production-ready.

## Scope
Focus on validation rather than major new architecture:
- concurrency behavior with 2–5 clients
- reconnect and duplicate-identity races
- route-miss/drop behavior under load
- shutdown during active traffic
- log and counter sanity
- memory/thread/resource stability across repeated cycles

This phase should avoid redesign unless validation exposes a real defect. Any bug fixes discovered here should be made as focused corrections against earlier phase outputs.

## V1 validation goals
The goal is not to benchmark maximum throughput or build a long-lived soak platform. The goal is to confirm that the intended V1 operating envelope behaves predictably and safely.

Primary envelope:
- 2–5 concurrent clients
- repeated connects/disconnects
- occasional route misses and invalid packets
- orderly and disorderly shutdowns

## Test categories

### 1. Multi-client connect/disconnect churn
Validate that the server maintains correct session state while clients repeatedly connect and disconnect.

Scenarios:
- 3 clients connect concurrently
- one disconnects, two remain healthy
- disconnected client reconnects with same `clientId` after the old session is gone
- repeated cycles of connect/disconnect do not corrupt session registry state

Expected outcomes:
- no session leaks
- no accidental removal of sibling sessions
- no stale host-route entries remain after disconnect

### 2. Duplicate identity contention
Validate correctness when conflicting clients attempt to join.

Scenarios:
- two clients attempt the same `clientId` concurrently
- two clients attempt the same advertised host IP concurrently
- existing healthy client remains unaffected by rejected duplicate

Expected outcomes:
- exactly one accepted session for the contested identity/host IP
- rejected client receives a clear rejection reason
- accepted client keeps working normally

### 3. Routing correctness under concurrent traffic
Validate that traffic is delivered only to the intended client while multiple clients are active.

Scenarios:
- packets destined to each connected host are generated interleaved
- unknown-destination packets are mixed in
- one client disconnects while traffic to others continues

Expected outcomes:
- only the matching client receives each routed packet
- no broadcast or cross-delivery occurs
- no-route packets are dropped and logged
- traffic for remaining clients continues after one client disappears

### 4. Invalid inbound packet handling
Validate the safety checks under noisy or incorrect client behavior.

Scenarios:
- client sends packet with wrong source IP
- client sends malformed IPv4 packet
- client sends non-IPv4 payload
- mix valid and invalid packets from multiple clients

Expected outcomes:
- invalid packets are dropped and logged
- valid packets still flow
- offending client is not disconnected automatically in V1 unless transport fails

### 5. Shutdown under load
Validate graceful shutdown while traffic is active.

Scenarios:
- server shutdown while 2–3 clients are connected and sending traffic
- client shutdown during active traffic
- debug mode shutdown with multiple in-process clients

Expected outcomes:
- server stops accepting new sessions
- all active clients are disconnected cleanly
- TUN and transport resources are released
- no deadlocks or thread self-join issues occur

### 6. Reconnect loop stability
Validate that repeated reconnect cycles do not accumulate stale transport instances or handlers.

Scenarios:
- client repeatedly loses and re-establishes connection
- duplicate-reject attempts are retried with corrected identity later
- multiple sequential reconnections over the same process lifetime

Expected outcomes:
- no obvious resource growth from abandoned transports
- connection recovery works after temporary failures
- session registry returns to expected size after each cycle

## Suggested validation methods
Use a mix of:
- NUnit integration tests with Pontifex Direct transport
- targeted fake-based tests for corner cases
- optional Docker-based multi-container validation if practical after rollout changes

### Prefer Direct transport first
Direct transport is ideal for repeated concurrency and rejection tests because it:
- runs quickly
- reduces external flakiness
- isolates app logic from network noise

### Use Docker/test harness selectively
Use it for confidence that CLI, scripts, and multi-process behavior align, not as the only validation layer.

## Metrics / signals to watch
Even if you do not build a formal metrics subsystem, validate these signals:
- active session count
- rejected session count by reason
- no-route drop count
- invalid-source drop count
- thread count stability during repeated reconnects, if easy to observe
- absence of growing stale objects/log spam patterns in repeated runs

## Likely files affected
- mostly new or expanded tests under `tests/PigeonPost.Bridge.Tests/`
- `tests/PigeonPost.Tests/Integration/`
- possibly `deploy/test/docker/` if you choose to extend the harness
- maybe lightweight instrumentation in runtime classes if tests need observability hooks

## Suggested sequencing inside this phase
1. Add concurrent multi-client direct-transport tests.
2. Add duplicate-contention tests.
3. Add invalid-packet stress tests.
4. Add shutdown-under-load tests.
5. Run repeated test loops locally or through a scripted harness.
6. If needed, add minimal counters/log hooks to improve observability.

## Success criteria
- Multi-client session state remains correct across repeated connect/disconnect cycles.
- Duplicate `clientId` and duplicate host IP behavior is deterministic under contention.
- Routing remains correct under concurrent traffic.
- Invalid packets do not destabilize valid sessions.
- Shutdown works cleanly during active traffic.
- No obvious resource or lifecycle regressions appear in repeated reconnect scenarios.

## Done means
- The implementation is not only logically correct but operationally credible in its intended V1 envelope.
- The most likely concurrency and failure regressions have dedicated automated coverage.
- The team has confidence to move from architecture-complete to release-candidate quality.
- Remaining known limitations, if any, are explicit rather than hidden in flaky behavior.

## Verification guidance
At minimum, run:
- targeted multi-client integration tests repeatedly
- duplicate rejection tests repeatedly
- shutdown-under-load tests repeatedly
- any available Docker/test harness validation after deployment updates are done

If practical, execute a short repeated loop of the relevant test subset to look for flakiness.

## Important notes for the implementer
- Keep this phase focused on validation, not on inventing major new features.
- If a test reveals a missing observable signal, add the smallest useful hook or counter rather than weakening the test.
- Flaky tests should be treated as a product problem first, not just a test problem.
- Prefer deterministic client IDs, host IPs, and packet payloads in stress tests so failures are diagnosable.

