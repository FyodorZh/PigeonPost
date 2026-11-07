# Stage 05 - Fake Runtime

## Goal

Build the shared runtime state machine, counters model, and UI log flow using a fake transport so the product becomes interactive before real networking work starts.

## Why This Stage Exists

- It unblocks the Dashboard, Logs, and reconnect UX without waiting for Pontifex integration.
- It forces the connection state contract to become explicit.
- Later stages can replace the fake runtime through DI instead of rewriting the UI.

## User-Visible Value

The app can connect, disconnect, auto-reconnect, and show changing counters and logs inside the real UI.

## Create Or Modify

- Add `IVpnRuntime` or `IVpnRuntimeController`.
- Add `ConnectionState` enum and a snapshot/state publication model.
- Add a fake runtime implementation in `PigeonPost.Vpn`.
- Add `DashboardViewModel`, `LogsViewModel`, and runtime-facing models.

## Technical Decisions

- Keep the runtime API UI-framework agnostic.
- Reset logs and session counters on each new session.
- Compute current speeds from byte deltas on a 1-second cadence.
- Implement immediate reconnect in the fake runtime so the UX contract is locked early.

## Implementation Steps

1. Define the runtime state model and command surface.
2. Implement a fake runtime that transitions `Disconnected -> Connecting -> Connected`.
3. Add a controlled simulated drop that sends the state back through reconnect.
4. Emit synthetic traffic counters and logs while connected.
5. Bind the shared UI to the fake runtime through DI.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Connect state transition.
- User disconnect transition.
- Unexpected disconnect triggers reconnect.
- Session counters reset on a new connection.
- Log filtering by verbosity.

## Manual Verification

1. Launch the desktop host.
2. Configure a valid profile.
3. Tap Connect and confirm the state becomes Connecting, then Connected.
4. Wait for counters and speeds to change.
5. Trigger the simulated failure and confirm the UI shows reconnecting automatically.

## Completion Criteria

- The app is interactive and stateful without any real server.
- Dashboard and Logs are driven by shared runtime abstractions.
- Reconnect behavior is visible and deterministic.

## Out Of Scope

- Real Pontifex transport.
- Real packet movement.
- Android or macOS platform VPN APIs.
