# Actions roadmap

## Purpose
This directory contains the phased implementation plan for PigeonPost V1 `1-to-many` support.

The actions are ordered so an implementer can move from architecture lock-in to test definition, implementation, rollout, and final validation without reopening major design questions.

## How to use this roadmap
- Execute actions in numeric order unless a later action explicitly says it can be partially prepared earlier.
- Treat each action as a phase with a clear deliverable.
- Do not collapse multiple major phases into one large refactor unless there is a strong reason and tests remain green throughout.
- When a validation phase exposes a defect, fix the defect in the phase that owns that behavior, then resume the roadmap.

## Important note about `action-01`
`action-01-architecture-decisions.md` is a **decision-record phase**, not a standalone code-delivery phase.

You do **not** implement `action-01` as a separate runtime feature. Instead, you:
1. read it,
2. treat it as the architectural contract for V1,
3. implement its decisions across actions `02` through `08`,
4. validate them in `09`.

If a later action contradicts `action-01`, `action-01` wins unless it is explicitly updated.

## Recommended execution order
1. `action-01-architecture-decisions.md`
2. `action-02-test-safety-net.md`
3. `action-03-protocol-and-packet-primitives.md`
4. `action-04-server-hub-and-session-registry.md`
5. `action-05-routing-and-validation.md`
6. `action-06-client-runtime-and-cli-refactor.md`
7. `action-07-debug-mode-shutdown-and-polish.md`
8. `action-08-deploy-and-ops-update.md`
9. `action-09-load-and-failure-validation.md`

## Dependency summary

### Action 01
Foundation only.

### Action 02
Depends on:
- 01

### Action 03
Depends on:
- 01
- 02 (test definitions for protocol/parser areas)

### Action 04
Depends on:
- 01
- 02
- 03

### Action 05
Depends on:
- 01
- 03
- 04

### Action 06
Depends on:
- 01
- 03
- 04
- 05

### Action 07
Depends on:
- 02
- 04
- 05
- 06

### Action 08
Depends on:
- 06
- 07

### Action 09
Depends on:
- 04
- 05
- 06
- 07
- 08 when deployment-level validation is included

## What each action owns

### 01 — Architecture decisions
Owns the V1 rules:
- one client == one IPv4 host route
- explicit `clientId`
- duplicate rejection
- strict source validation
- drop/log policy
- IPv4-only scope

### 02 — Test safety net
Owns the target test shape. It is acceptable here to add failing or staged tests that later phases make pass.

### 03 — Protocol and packet primitives
Owns binary handshake types/codecs and IPv4 parsing. It does **not** own full server-session refactoring.

### 04 — Server hub and session registry
Owns connection/session lifecycle and duplicate detection. It does **not** own the full routing policy.

### 05 — Routing and validation
Owns exact host-route lookup, packet ownership checks, and drop behavior.

### 06 — Client runtime and CLI refactor
Owns the permanent runtime split, `--client-id`, `--debug-clients`, and current runtime safety fixes.

### 07 — Debug mode, shutdown, and polish
Owns representative in-process multi-client debug behavior and finalized runtime shutdown/order polish.

### 08 — Deploy and ops update
Owns scripts, compose, entrypoints, environment wiring, and operator-facing rollout artifacts.

### 09 — Load and failure validation
Owns final confidence testing for concurrency, reconnects, shutdown, and failure behavior.

## Concrete phase execution checklist

Use the following checklist when implementing the roadmap:

1. Read `action-01-architecture-decisions.md` completely before changing code.
2. Implement one action at a time in numeric order.
3. At the start of each action:
   - read the action file,
   - read the files listed in its “Likely files affected” section,
   - confirm the previous action’s success criteria are already satisfied.
4. During each action:
   - keep changes scoped to that action’s ownership,
   - add or update tests before or alongside production code,
   - avoid reformatting unrelated files.
5. At the end of each action:
   - run the smallest relevant test subset first,
   - then run broader affected test projects,
   - confirm no new warnings violate project settings,
   - check the next action does not need the current branch to be conceptually split again.
6. Only move to the next action when the current one has a stable result.

## Suggested commit and PR policy

### Recommended boundary
Use **one action = one main commit or one small PR series**.

This keeps regressions attributable and allows review against the roadmap.

### Allowed exceptions
- `action-02` may be split into:
  - test structure/scaffolding commit
  - assertions/failing-tests commit
- `action-06` may be split into:
  - CLI/configuration changes
  - runtime split and lifecycle fixes
- `action-09` may contain several validation-only commits if that helps isolate flaky or concurrency-sensitive test additions.

### Review rule
If a commit changes behavior owned by more than one action, either:
- split the commit, or
- document clearly why the combined change is unavoidable.

## Merge-independence guidance

The following indicates whether a phase is a reasonable standalone merge target.

| Action | Can be merged independently? | Notes |
|---|---|---|
| 01 | Yes | Documentation-only decision record. |
| 02 | Yes | Even if some tests are initially pending/failing by design, the branch should be reviewable as test-shape groundwork. Prefer keeping mainline green if possible. |
| 03 | Yes | Protocol primitives are a clean merge boundary. |
| 04 | Usually yes | Best merged once session registration/rejection tests pass. |
| 05 | Yes | Routing/validation is a clean behavior boundary once tests pass. |
| 06 | Yes | CLI/runtime split is a major but valid standalone merge if all affected tests pass. |
| 07 | Yes | Debug/runtime polish can be merged after the core runtime is stable. |
| 08 | Yes | Deployment/doc rollout should be independently reviewable. |
| 09 | Yes | Validation-only or mostly-validation changes are a valid final merge stage. |

## Estimated file-touch map by action

These lists are intentionally approximate. They are meant to help planning, not to prevent necessary edits elsewhere.

### 01 — Architecture decisions
**Primary files:**
- `actions/action-01-architecture-decisions.md`
- `actions/README.md`

**Expected code impact in this phase:**
- none required

### 02 — Test safety net
**Primary files:**
- `tests/PigeonPost.Bridge.Tests/BridgeTests.cs`
- `tests/PigeonPost.Bridge.Tests/Pontifex/PontifexDirectTransportTests.cs`
- `tests/PigeonPost.Tests/CliParserTests.cs`
- new files under `tests/PigeonPost.Bridge.Tests/Protocol/`
- new files under `tests/PigeonPost.Bridge.Tests/Server/`
- new files under `tests/PigeonPost.Tests/Integration/`

**Typical output:**
- staged or failing tests describing target behavior

### 03 — Protocol and packet primitives
**Primary files:**
- new files under `src/PigeonPost.Bridge/Protocol/`
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs`
- protocol-related test files created in action 02

**Typical output:**
- handshake codec
- ack/reject enums and records
- IPv4 parser

### 04 — Server hub and session registry
**Primary files:**
- new files under `src/PigeonPost.Bridge/Server/`
- `src/PigeonPost.Bridge/Handlers/BridgeServerAcknowledger.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs`
- server-side tests under `tests/PigeonPost.Bridge.Tests/Server/`
- direct transport integration tests under `tests/PigeonPost.Bridge.Tests/Pontifex/`

**Typical output:**
- `ServerHub`
- `ClientSession`
- duplicate rejection path

### 05 — Routing and validation
**Primary files:**
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Protocol/Ipv4PacketParser.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeServerHandler.cs`
- routing/validation tests under `tests/PigeonPost.Bridge.Tests/Server/`

**Typical output:**
- exact host routing
- strict source validation
- structured drop reasons/logging

### 06 — Client runtime and CLI refactor
**Primary files:**
- `src/PigeonPost/App.cs`
- `src/PigeonPost/CliParser.cs`
- `src/PigeonPost/BridgeConfiguration.cs`
- `src/PigeonPost.Bridge/Bridge.cs` or its replacement
- `src/PigeonPost.Bridge/IBridge.cs` or its replacement split
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- `src/PigeonPost.Tun/TunDevice.cs`
- new resolver file(s) under `src/PigeonPost.Tun/`
- CLI and app tests under `tests/PigeonPost.Tests/`

**Typical output:**
- `--client-id`
- `--debug-clients`
- runtime split
- lifecycle safety fixes

### 07 — Debug mode, shutdown, and polish
**Primary files:**
- `src/PigeonPost/App.cs`
- `src/PigeonPost/CliParser.cs`
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Handlers/BridgeClientHandler.cs`
- integration tests under `tests/PigeonPost.Tests/Integration/`
- direct-transport tests under `tests/PigeonPost.Bridge.Tests/Pontifex/`

**Typical output:**
- multi-client debug orchestration
- finalized shutdown order
- better runtime observability

### 08 — Deploy and ops update
**Primary files:**
- `deploy/client/deploy-docker.sh`
- `deploy/client/deploy-plain.sh`
- `deploy/client/docker/docker-compose.yml`
- `deploy/server/deploy-docker.sh`
- `deploy/server/deploy-plain.sh`
- `deploy/server/docker/docker-compose.yml`
- `docker/docker-entrypoint.sh`
- `deploy/test/docker/docker-compose.yml`
- related helper files and operator-facing examples

**Typical output:**
- `CLIENT_ID` rollout
- CLI-aligned deploy scripts
- compose parity and idempotency review

### 09 — Load and failure validation
**Primary files:**
- `tests/PigeonPost.Bridge.Tests/`
- `tests/PigeonPost.Tests/Integration/`
- optionally `deploy/test/docker/`
- possibly lightweight runtime instrumentation hooks

**Typical output:**
- concurrency/reconnect/shutdown stress coverage
- release-confidence evidence

## Action-by-action exit checklist

### Action 01
- decision record is internally consistent
- later actions do not conflict with it

### Action 02
- new tests exist for protocol, session registry, routing, and duplicate rejection
- old tests no longer force a singular server endpoint abstraction

### Action 03
- handshake buffers are defined and tested
- IPv4 parsing is defined and tested

### Action 04
- multiple clients can coexist in server-side session state
- duplicate `clientId` and duplicate host IP are rejected deterministically

### Action 05
- exact host-route delivery works
- invalid source and no-route packets are dropped and logged

### Action 06
- CLI/runtime changes compile and tests pass
- lifecycle fixes are in place

### Action 07
- debug mode can model more than one client
- shutdown behavior is deterministic and clean

### Action 08
- deploy artifacts reflect the new CLI/runtime contract
- Docker/plain flows remain equivalent

### Action 09
- repeated validation runs do not expose obvious lifecycle or concurrency regressions

## Suggested test cadence by phase

- After `02`: run affected test projects to ensure scaffolding compiles.
- After `03`: run protocol-focused unit tests.
- After `04`: run server-session and direct-transport tests.
- After `05`: run routing/validation and direct-transport tests.
- After `06`: run CLI/app tests plus affected bridge tests.
- After `07`: run integration/debug-mode tests plus earlier core subsets.
- After `08`: run any available deployment-oriented checks and ensure the solution still builds/tests.
- After `09`: run the targeted stress subsets repeatedly, then run the broadest practical regression pass.

## Implementation policy
- Prefer test-first within each phase.
- Keep changes small enough that failures can be attributed to a single phase.
- Avoid introducing temporary abstractions that preserve the old single-endpoint server model longer than necessary.
- Do not change protocol or CLI semantics in deployment/validation phases unless earlier actions are explicitly revised.

## Ready-for-implementation checklist
The roadmap is ready for implementation when:
- action ownership is clear,
- dependencies are explicit,
- no phase has hidden prerequisite decisions,
- deployment and validation are represented, not only code refactoring.

This directory now satisfies those conditions.

