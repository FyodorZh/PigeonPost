# Stage 08 - Desktop macOS Connected Mode

## Goal

Turn the desktop host into the real macOS V1 connected-mode client: real Pontifex session, real selected client IP, no system VPN registration.

## Why This Stage Exists

- macOS V1 is intentionally a transport-connected client, not a system VPN.
- The UI state semantics must be locked before probe traffic is added.
- This gives a real cross-platform runtime path before Android-specific work begins.

## User-Visible Value

On macOS, the app can connect to a real PigeonPost server and show live connection lifecycle, even though it is not yet generating synthetic tunnel traffic.

## Create Or Modify

- Wire the desktop host to the real runtime only.
- Remove fake runtime registrations from production startup.
- Add clear UI wording or tooltip support where needed so `Connected` means transport/session connected.
- Map duplicate IP rejection and transport failures into user-visible log entries.

## Technical Decisions

- The desktop host is the macOS V1 delivery vehicle.
- No Apple VPN entitlements are required in this slice.
- Do not add any synthetic packet generation yet.

## Implementation Steps

1. Swap the desktop host DI registrations from fake runtime to real runtime.
2. Ensure connect/disconnect commands use the real profile and real client IP.
3. Surface typed transport failures cleanly in Logs and Dashboard state.
4. Confirm reconnect still works through the real runtime path.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Viewmodel state mapping for real runtime snapshots.
- Handshake rejection message mapping.
- Reconnect state visibility.

## Manual Verification

1. Run the desktop host.
2. Connect to a reachable debug or real server.
3. Confirm the UI reaches Connected and logs the session lifecycle.
4. Disconnect and confirm the state returns to Disconnected.

## Completion Criteria

- macOS V1 connected mode is real, not simulated.
- `Connected` means transport/session connected and nothing stronger.
- Real reconnect behavior is visible in the UI.

## Out Of Scope

- Synthetic ICMP probes.
- Android host work.
- System VPN registration on Apple platforms.
