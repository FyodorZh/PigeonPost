# Stage 10 - Endpoint Isolation

## Goal

Enforce the V1 rule that endpoint/mobile clients get internet egress but must not reach Linux TUN peers or other VPN peers.

## Why This Stage Exists

- The current server runtime routes by exact host inside the unified `10.0.10.0/24` subnet.
- Without an explicit policy, endpoint clients could talk to other VPN clients.
- This is a product rule, not just a deployment detail.

## User-Visible Value

macOS probe-mode clients keep internet-bound behavior, but peer-to-peer traffic inside the VPN client subnet is blocked according to V1 rules.

## Create Or Modify

- Add explicit address classification for Linux-reserved and endpoint/mobile client ranges.
- Add isolation checks in `ServerHub` or the smallest equivalent shared server path.
- Add logs and counters for isolation drops.
- Update operator-facing docs describing the range contract.

## Technical Decisions

- Do not change the handshake format.
- Infer client class from the already agreed IP ranges.
- Linux clients use `10.0.10.2-10`.
- Endpoint/mobile clients use `10.0.10.11-254`.
- Keep existing Linux client behavior unless a packet involves the endpoint/mobile policy boundary.

## Implementation Steps

1. Add a shared classifier for VPN-subnet host roles.
2. In the server receive path, drop client-originated packets that target forbidden VPN-subnet peers.
3. Log the reason clearly so UI and operators can understand the drop.
4. Add tests for allowed and denied matrices.
5. Keep internet-bound traffic untouched.

## Automated Verification

- `dotnet test tests/PigeonPost.Bridge.Tests/`
- `dotnet test tests/PigeonPost.Vpn.Tests/`

Suggested NUnit coverage:

- Endpoint to endpoint denied.
- Endpoint to Linux denied.
- Linux to endpoint denied if that is the chosen V1 rule.
- Endpoint to internet allowed.
- Linux to internet unaffected.

## Manual Verification

1. Run the macOS probe client.
2. Inject or simulate a packet toward another VPN client IP.
3. Confirm the server logs a policy drop.
4. Confirm `1.1.1.1` probe traffic still proceeds.

## Completion Criteria

- V1 endpoint isolation is enforced in code, not left implicit.
- The policy is covered by tests.
- The unified subnet model remains intact.

## Out Of Scope

- Dynamic IP allocation.
- Authentication.
- IPv6 policy.
