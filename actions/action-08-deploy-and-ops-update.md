# Action 08 — Deploy and ops update

## Objective
Update deployment artifacts, operational scripts, Docker/compose wiring, and usage examples so the new `1-to-many` architecture is actually runnable outside tests.

## Depends on
- `action-06-client-runtime-and-cli-refactor.md`
- `action-07-debug-mode-shutdown-and-polish.md` for finalized debug/runtime wording where relevant

## Produces
- deployment scripts and compose files aligned with the new CLI contract
- operational examples that match the multi-client architecture
- an idempotent repository rollout path for real environments

## Why this phase exists
The earlier phases cover the code architecture, protocol, routing, CLI, and debug flow. They do not fully complete the real project rollout because PigeonPost is deployed through shell scripts, Docker, and compose files.

After the `1-to-many` refactor, at least these things change operationally:
- the client must provide a required `clientId`
- help/examples must reflect the new model
- debug mode semantics change (`N` concurrent clients)
- deployment entrypoints and compose files must stay aligned with CLI changes
- deployment parity between Docker and plain scripts must be preserved

Without this phase, the code can compile and tests can pass while actual deployments still fail or use stale semantics.

## Scope
This phase covers repository-level rollout items:
- deployment shell scripts
- Docker compose files
- runtime command lines
- environment variable plumbing
- examples and operator-facing docs
- idempotency review for changed scripts

This phase consumes the application/runtime contract from earlier actions. It should not introduce new protocol or CLI semantics.

## Recommended operational decisions

### Client identity source
Because `--client-id` is required for the new client architecture, deployment must provide it explicitly.

**Recommended V1 approach:** use an environment variable.

Suggested name:
- `CLIENT_ID`

**Reasoning:**
- works in both Docker and plain deploy flows
- avoids hardcoding identity in source-controlled scripts
- easier to integrate with host-specific automation later
- keeps CLI behavior explicit and testable

### Validation behavior in scripts
Scripts should fail fast if required variables are missing.

For client deployment, fail if any of these are unset or empty:
- `CLIENT_ID`
- existing transport URL inputs, if script already externalizes them
- TUN-related variables already required by the current setup

### Server deployment
The server should not require `clientId`, but its examples/help should describe multi-client behavior, not single-client behavior.

## Likely files affected
- `deploy/client/deploy-docker.sh`
- `deploy/client/deploy-plain.sh`
- `deploy/client/pre-deploy.sh` if it references runtime assumptions or examples
- `deploy/client/docker/docker-compose.yml`
- `deploy/server/deploy-docker.sh`
- `deploy/server/deploy-plain.sh`
- `deploy/server/docker/docker-compose.yml`
- `docker/docker-entrypoint.sh`
- `deploy/test/docker/docker-compose.yml`
- possibly helper files under `deploy/test/docker/`
- possibly operator docs or examples in `AGENTS.md` / project docs if you choose to keep them synchronized

## Implementation details

### 1. Update client runtime invocation
Wherever the client role is started, append the required argument:
- `--client-id "$CLIENT_ID"`

Ensure the invocation also remains properly quoted for URLs containing `|`.

### 2. Preserve Docker/plain deployment equivalence
The project requires Docker and plain deploy methods to remain behaviorally equivalent.

That means if the Docker deployment accepts/provides:
- `CLIENT_ID`
- `TUN_NAME`
- transport URL

then the plain deployment must do the same in a semantically equivalent way.

Do not let one path silently auto-generate values while the other requires explicit ones.

### 3. Update compose files
For client compose:
- add `CLIENT_ID` as an environment variable or otherwise inject it into the command path
- make sure the final executed command includes the new client argument

For test/debug-related compose:
- review whether any client containers need unique IDs
- if multiple client containers are modeled, each must get a distinct `CLIENT_ID`

### 4. Review `docker/docker-entrypoint.sh`
If the entrypoint simply execs the app with passed arguments, it may not need changes.

But verify:
- whether arguments are hardcoded there
- whether environment substitution is expected there
- whether debug mode support or multi-client local testing would benefit from helper defaults

Prefer keeping identity selection outside the entrypoint unless there is a strong operational benefit.

### 5. Update usage examples and script banners
Any script help text, echo output, README-like comments, or docs that still say:
- server accepts a single client
- debug mode uses exactly two TUNs only

should be updated.

Examples should reflect:
- client requires `--client-id`
- server can accept multiple clients
- debug mode can simulate multiple concurrent clients via a parameter

### 6. Preserve idempotency requirements
Any changed deployment/setup script must remain idempotent.

This phase should verify:
- rerunning deploy scripts does not duplicate state
- rerunning compose-based deploy does not create inconsistent command/env configuration
- new validation checks fail cleanly and consistently, not halfway through setup

### 7. Decide how test deployments derive client IDs
For automated test harnesses, use deterministic IDs, for example:
- `pp-client-1`
- `pp-client-2`
- `pp-client-3`

This makes test logs and failures much easier to interpret.

## Suggested sequencing inside this phase
1. Inventory all places where `dotnet /app/PigeonPost.dll` or equivalent is invoked.
2. Add `CLIENT_ID` plumbing to client deploy scripts.
3. Update Docker compose definitions.
4. Review entrypoint behavior and adjust only if needed.
5. Update script comments/examples/help text.
6. Run each affected deploy script twice conceptually for idempotency review.
7. Run any lightweight deployment validation commands/tests available in the repository.

## Success criteria
- Every client deployment path provides a valid `clientId`.
- Docker and plain deploy methods remain equivalent in behavior.
- No deployment artifact still assumes single-client server semantics.
- Multi-client test/deploy harnesses assign distinct client IDs.
- Updated scripts remain idempotent by project standards.

## Done means
- A person deploying the new architecture can use the repository’s existing operational entry points without manual patching.
- Required client identity is handled cleanly by scripts and compose.
- The repository’s operational layer matches the new CLI/runtime contract.
- Repository rollout artifacts are no longer a blocker for real-world implementation and testing.

## Verification guidance
At minimum, verify:
- client Docker deploy command contains `--client-id`
- client plain deploy command contains `--client-id`
- server deploy paths still work without needing `clientId`
- test compose or multi-client harness assigns unique IDs
- rerunning modified scripts does not produce duplicate rules or broken state

## Important notes for the implementer
- Keep environment variable names simple and explicit.
- Do not bury `clientId` generation in opaque shell logic unless absolutely necessary.
- Prefer deterministic identities in automated environments.
- If any deployment artifact cannot yet express multi-client testing cleanly, record that limitation explicitly instead of silently keeping stale single-client assumptions.

