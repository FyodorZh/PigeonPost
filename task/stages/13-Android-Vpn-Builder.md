# Stage 13 - Android VPN Builder

## Goal

Create the real Android VPN interface configuration with the selected client IP, full-route behavior, and fixed DNS servers, but without the final tunnel pump yet.

## Why This Stage Exists

- It locks the platform networking contract separately from the runtime data path.
- Full-route behavior is a product requirement.
- DNS and address configuration must be testable before real traffic forwarding is introduced.

## User-Visible Value

The Android app can establish a system-managed VPN interface and show the platform VPN indicator using the selected client identity.

## Create Or Modify

- Add an Android VPN configuration builder abstraction in `PigeonPost.Vpn` or the Android host.
- Add `AndroidTunDevice` scaffolding over `ParcelFileDescriptor`.
- Apply the profile-selected client IP and shared DNS constants.

## Technical Decisions

- Add address `10.0.10.x/24` using the selected client IP.
- Add route `0.0.0.0/0` for full-device VPN.
- Use the fixed V1 DNS list from the shared constants.
- Set a single MTU constant and keep it non-configurable in V1.
- If API level allows it, enable blocking mode; otherwise document the system always-on and lockdown expectations separately.

## Implementation Steps

1. Build a pure configuration object from the saved profile.
2. Translate it into `VpnService.Builder` calls.
3. Establish the `ParcelFileDescriptor` and keep it alive in the service.
4. Expose enough state to the shared runtime layer for the next stage.
5. Show a clear UI distinction between service-running and transport-connected if needed during this intermediate slice.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`

Suggested NUnit coverage:

- The generated Android VPN config contains the selected address.
- The generated config uses `0.0.0.0/0`.
- The generated config uses the fixed DNS list.
- MTU and session naming are stable.

## Manual Verification

1. Start the Android VPN flow.
2. Confirm the system VPN indicator appears.
3. Confirm the app reports that the VPN interface is established.
4. Disconnect and confirm the interface is torn down.

## Completion Criteria

- Android can establish the VPN interface with the right identity and routes.
- All networking constants come from shared code, not duplicated literals.
- The app is ready for the real packet pump slice.

## Out Of Scope

- Real packet movement to the server.
- Throughput verification.
- Final background resilience behavior.
