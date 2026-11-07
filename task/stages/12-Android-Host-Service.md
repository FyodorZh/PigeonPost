# Stage 12 - Android Host Service

## Goal

Create the real Android app host flow: permission request, foreground `VpnService`, notification channel, service start/stop, and shared-state reporting back to the UI.

## Why This Stage Exists

- Android requires user consent before VPN creation.
- Android 8+ requires foreground-service behavior for background-started VPNs.
- The UI needs a stable way to observe Android service lifecycle independently of packet forwarding.

## User-Visible Value

The Android app can request VPN permission, start a foreground service, show a persistent notification, and stop cleanly.

## Create Or Modify

- Flesh out `src/PigeonPost.VpnClientView.Android/`.
- Add `MainActivity`, service command plumbing, notification channel setup, and a service state repository.
- Wire the shared UI to Android host events through DI.
- Handle `onRevoke()` by forcing disconnect.

## Technical Decisions

- Return `START_STICKY` from the service path if needed for resilience.
- Keep UI and service communication simple through a shared in-process state store for V1.
- Do not create the VPN interface yet; this slice is about lifecycle correctness.

## Implementation Steps

1. Add the manifest entries for the VPN service and required foreground-service permissions.
2. Implement the permission request flow using `VpnService.prepare(...)`.
3. Start the Android service and promote it to foreground with a notification.
4. Publish service state back to the shared UI.
5. Stop the service cleanly on user disconnect and `onRevoke()`.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Service state repository transitions.
- Connect command orchestration around permission and service start.
- Revoke maps to disconnect state.

## Manual Verification

1. Install and run the Android app.
2. Tap Connect and confirm the system permission dialog appears.
3. Approve it and confirm the foreground notification appears.
4. Tap Disconnect and confirm the notification disappears and UI state resets.

## Completion Criteria

- The Android host lifecycle is real and visible.
- Permission, service start, and revoke behavior are wired end-to-end.
- No packet forwarding is needed yet.

## Out Of Scope

- Building the VPN interface.
- Full-device traffic capture.
- Real server communication.
