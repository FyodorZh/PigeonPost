# Stage 15 - Deployment Contract

## Goal

Document and verify the operator-facing network contract so Linux clients, endpoint/mobile clients, and server egress all align with the new multiplatform product.

## Why This Stage Exists

- The `deploy/` directory is part of the core product understanding.
- The runtime already depends on the unified `10.0.10.0/24` subnet model.
- Mobile rollout will fail operationally if the address-allocation contract remains implicit.

## User-Visible Value

Operators can deploy and reason about Linux and endpoint/mobile clients consistently, with clear IP ranges and verifiable server expectations.

## Create Or Modify

- Update deployment docs to make the range contract explicit.
- Add or extend verification scripts under `deploy/server/` for endpoint/mobile readiness.
- Add a short operator guide covering manual IP selection, duplicate-IP rejection, and endpoint isolation.
- Update any stale docs that still imply the old `/30` deploy model.

## Technical Decisions

- Treat the unified model as the only production baseline.
- Document these ranges explicitly.
- Server uses `10.0.10.1`.
- Linux TUN clients use `10.0.10.2-10`.
- Endpoint/mobile clients use `10.0.10.11-254`.
- Keep client-side MASQUERADE for Linux clients.
- Keep duplicate-IP rejection as the only occupancy check in V1.

## Implementation Steps

1. Update docs in `task/` and `deploy/` to remove ambiguity about the subnet model.
2. Extend `deploy/server/verify-egress.sh` or add a sibling script for endpoint policy readiness.
3. Document the exact prerequisites for Android/macOS clients to work against a server.
4. Add manual verification steps for idempotent deploy scripts.

## Automated Verification

- `dotnet test`
- Run shell verifier scripts on a Linux server or test container.

Suggested verification script checks:

- Server TUN address is `10.0.10.1/24`.
- NAT and FORWARD rules still exist.
- Routes for representative endpoint/mobile client IPs resolve through `tun0`.
- Any policy-drop assumptions needed by endpoint isolation are documented and testable.

## Manual Verification

1. Run the server pre-deploy script twice and confirm idempotent behavior.
2. Run the client pre-deploy script twice with a Linux client IP and confirm idempotent behavior.
3. Read the operator guide and confirm there is no ambiguity about which IPs belong to which class of client.

## Completion Criteria

- Deployment docs match runtime behavior.
- The unified subnet model is explicit everywhere.
- Operators have a clear manual IP assignment contract for V1.

## Out Of Scope

- Dynamic IP allocation.
- Authentication rollout.
- iOS deployment specifics.
