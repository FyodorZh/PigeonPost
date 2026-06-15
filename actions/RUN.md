# Run prompt for executing all actions

Use the following prompt with an implementation agent to execute the complete PigeonPost V1 `1-to-many` roadmap.

---

Implement the full `actions/` roadmap for PigeonPost V1 `1-to-many` support.

## Context
- The project is a .NET 10 Linux-only console application.
- The authoritative architecture contract is `actions/action-01-architecture-decisions.md`.
- The roadmap index and execution rules are in `actions/README.md`.
- All actions from `action-01` through `action-09` must be respected.
- `action-01` is a decision-record phase, not a standalone runtime feature. Its decisions must be implemented through later phases.

## Overall objective
Refactor PigeonPost from a `1-to-1` tunnel to a V1 `1-to-many` design where:
- the server supports multiple concurrent clients,
- each client is identified by an explicit `clientId` sent during handshake,
- each client advertises exactly one IPv4 host route,
- duplicate `clientId` and duplicate host-IP claims are rejected,
- routing is exact-host only,
- invalid or unsupported packets are dropped and logged,
- the client and server runtimes are cleanly separated,
- debug mode can simulate `N` concurrent clients,
- deployment artifacts are updated,
- final concurrency/failure validation is performed.

## Required execution order
Follow the roadmap in numeric order:
1. `action-01-architecture-decisions.md`
2. `action-02-test-safety-net.md`
3. `action-03-protocol-and-packet-primitives.md`
4. `action-04-server-hub-and-session-registry.md`
5. `action-05-routing-and-validation.md`
6. `action-06-client-runtime-and-cli-refactor.md`
7. `action-07-debug-mode-shutdown-and-polish.md`
8. `action-08-deploy-and-ops-update.md`
9. `action-09-load-and-failure-validation.md`

## Phase execution rules
For each action:
1. Read the action file fully.
2. Read the files listed in its “Likely files affected” section.
3. Make the smallest correct set of changes needed for that action.
4. Add or update tests before or alongside production code.
5. Run targeted tests for that phase.
6. Run broader affected test projects before moving on.
7. Do not proceed to the next action until the current one is stable.

## Architecture rules that must not be violated
- One client == one advertised IPv4 host route.
- IPv4 only in V1 multi-client routing.
- No authentication in V1.
- No backward compatibility mode for the old `1-to-1` protocol/CLI.
- No default route or fallback client.
- No buffering for missing or unavailable server-side routes; drop and log.
- Duplicate `clientId` connections are rejected.
- Duplicate advertised host IP claims are rejected.
- Inbound client packets must have source IPv4 equal to the client’s advertised host IP; otherwise drop and log.
- Do not evolve the old server-side `Bridge` into the permanent multi-client server abstraction.

## Implementation guidance
- Treat `action-01` as the architectural contract.
- Prefer test-first within each phase.
- Keep namespaces aligned with folder structure.
- Preserve existing style and avoid unrelated reformatting.
- Warnings are treated as errors; keep the code warning-free.
- When validation reveals a bug, fix the bug in the phase that owns that behavior.

## Expected major outputs by phase
- `02`: test safety net for protocol, session registry, routing, duplicate rejection, CLI, and integration shape.
- `03`: protocol primitives, handshake codec, reject codes, IPv4 parser.
- `04`: server-side `ServerHub`, `ClientSession`, deterministic accept/reject flow.
- `05`: exact host routing and strict inbound source validation.
- `06`: client/server runtime split, `--client-id`, `--debug-clients`, lifecycle fixes.
- `07`: multi-client debug mode, shutdown polish, runtime observability improvements.
- `08`: deployment and compose updates, `CLIENT_ID` rollout, parity and idempotency review.
- `09`: concurrency/reconnect/shutdown/failure validation with repeated test runs where practical.

## Testing requirements
At minimum:
- run targeted unit tests after each action,
- run affected integration/direct-transport tests after server/runtime actions,
- run broader regression passes at meaningful checkpoints,
- run repeated stress-oriented subsets during `action-09` to detect flakiness.

If a test cannot yet pass because a later phase owns the required runtime behavior, it is acceptable to stage the test structure earlier as described in the roadmap, but the final result by the end of `action-09` must be green.

## Deliverable expectations
When finished:
- all action outputs are implemented,
- tests are updated and passing,
- deployment artifacts reflect the new CLI/runtime contract,
- the repository is ready for real V1 `1-to-many` use,
- any remaining limitations are explicit and documented.

## Reporting expectations
During execution, report progress by action number.
For each action, summarize:
- what was changed,
- what tests were run,
- whether the phase is complete,
- any follow-up items discovered.

Do not skip phases silently.
Do not reinterpret architecture rules unless the roadmap is explicitly updated.

---

Optional execution note: use `actions/README.md` as the checklist and coordination file while implementing.

