# Stage 09 - Desktop macOS Probe Mode

## Goal

Add synthetic ICMP probe traffic on the desktop host so macOS V1 exercises real packet flow and server egress without using a system VPN.

## Why This Stage Exists

- This is the core macOS V1 product behavior.
- It validates the shared runtime with real packets instead of just a handshake.
- It gives an early end-to-end proof of transport, routing, and return traffic.

## User-Visible Value

While connected on macOS, the app automatically emits probe traffic to `1.1.1.1`, updates counters, and writes periodic probe logs.

## Create Or Modify

- Add `ProbeTunDevice` implementing `ITunDevice`.
- Add ICMP packet creation and parsing helpers in `PigeonPost.Vpn`.
- Add a probe scheduler and reply matcher.
- Feed probe bytes through the same real runtime path used by future Android traffic.

## Technical Decisions

- Use `1.1.1.1` as the fixed V1 probe target.
- Keep the probe loop inside `PigeonPost.Vpn`, not the UI project.
- Model probe traffic as raw IPv4 packets so the runtime path stays honest.
- Count probe traffic in the same counters shown on the Dashboard.

## Implementation Steps

1. Implement a queue-backed `ProbeTunDevice` whose `Read()` produces outgoing ICMP echo requests.
2. Implement `Write()` handling that parses returning packets and records successes or timeouts.
3. Add periodic logging of send, reply, and timeout activity.
4. Start the probe loop only when the runtime enters Connected.
5. Stop and clear probe state on disconnect.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`

Suggested NUnit coverage:

- ICMP packet creation produces valid IPv4 layout.
- Probe cadence and timeout behavior.
- Returning packets are matched to the correct outstanding probe.
- Probe bytes affect counters and speeds.

## Manual Verification

1. Connect the desktop host to a reachable server.
2. Wait for the periodic probe interval.
3. Confirm logs show probe send activity.
4. Confirm sent counters increase.
5. If upstream ICMP replies are allowed, confirm receive counters and reply logs also increase.

## Completion Criteria

- macOS V1 now moves real raw packets through the shared runtime.
- Probe activity is visible in logs and counters.
- Disconnect stops the probe loop cleanly.

## Out Of Scope

- Android-specific packet capture.
- Endpoint isolation policy.
- Apple entitlement-based VPN work.
