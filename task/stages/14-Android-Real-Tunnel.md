# Stage 14 - Android Real Tunnel

## Goal

Wire the Android VPN interface into the shared `PigeonPost.Vpn` runtime so the device runs a real full-device VPN over the existing PigeonPost protocol.

## Why This Stage Exists

- This is the main Android V1 product slice.
- Earlier stages de-risked the socket protect hook, service lifecycle, and VPN interface separately.
- The final step is to move real raw IPv4 packets between Android `VpnService` and Pontifex.

## User-Visible Value

The Android app becomes a real full-device VPN client with counters, logs, reconnect behavior, and internet egress through the PigeonPost server.

## Create Or Modify

- Finish `AndroidTunDevice` so it fully implements `ITunDevice` over the `ParcelFileDescriptor`.
- Reuse the real `VpnClientRuntime` from `PigeonPost.Vpn`.
- Connect the Android host service to the shared runtime start/stop flow.
- Publish live counters and logs back into the shared UI.

## Technical Decisions

- Keep the shared reconnect loop in `PigeonPost.Vpn`, not in Android-specific service code.
- Use the counting `ITunDevice` decorator here too so Android and macOS counters behave identically.
- Treat `onRevoke()` and service teardown as hard stops for the runtime.

## Implementation Steps

1. Implement blocking read/write over the Android VPN file descriptor.
2. Start the shared runtime from the foreground service using the selected profile.
3. Pass the protected transport factory from Stage 11 into the runtime.
4. Forward runtime snapshots, counters, and logs to the UI state store.
5. Verify reconnect after server disconnects or temporary network loss.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Android tunnel adapter contract tests through abstractions.
- Runtime stop behavior on revoke.
- Reconnect logic with a mocked protected transport factory.

## Manual Verification

1. Connect the Android app to a reachable server.
2. Browse to a public website or issue another network request from the device.
3. Confirm the Dashboard counters and speeds increase.
4. Stop the server or temporarily drop connectivity and confirm the app shows reconnecting.
5. Disconnect and confirm device traffic stops using the VPN.

## Completion Criteria

- Android V1 is a real full-device VPN client.
- The shared runtime is truly shared between Android and macOS V1 paths.
- Immediate reconnect, counters, and logs all work through the real tunnel.

## Out Of Scope

- iOS extension work.
- MTU tuning experiments.
- Multi-profile support.
