# Stage 03 - Profile Validation

## Goal

Define the single V1 profile model and validate the server URL, client IP selection, and fixed network defaults in one place.

## Why This Stage Exists

- V1 allows only one local profile.
- The client IP contract is central to both UI and server behavior.
- Validation must be deterministic before persistence or runtime work begins.

## User-Visible Value

The Config tab becomes useful: the user can enter a URL, pick a client IP, and immediately see whether the profile is valid.

## Create Or Modify

- Add `VpnProfile` or `VpnClientProfile` in `PigeonPost.Vpn`.
- Add `VpnProfileValidator`.
- Add a constants class for V1 network defaults.
- Add `ConfigViewModel` with validation state and full IP preview.

## Technical Decisions

- Keep the UI input as the last octet, but store the full decision in the profile model clearly.
- Freeze these shared constants in code now:
- VPN subnet: `10.0.10.0/24`
- Server TUN IP: `10.0.10.1`
- Linux reserved client range: `10.0.10.2-10`
- Endpoint/mobile allowed range: `10.0.10.11-254`
- Treat the DNS list as a stage-local decision point.
- If product input is still absent, use provisional V1 DNS servers `1.1.1.1` and `1.0.0.1` and keep them in one constants file.

## Implementation Steps

1. Add the profile record and a validator that returns explicit error codes or messages.
2. Validate URL format against the current Pontifex address shape: `type|host:port/timeout`.
3. Validate the client IP octet range `11-254`.
4. Surface validation state through `ConfigViewModel` and inline UI bindings.
5. Show the computed full IP preview `10.0.10.x` in the Config tab.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Valid URL accepted.
- Malformed URL rejected.
- Octets `< 11` and `> 254` rejected.
- Full IP preview renders correctly.
- Provisional DNS constants are exposed from one place.

## Manual Verification

1. Open the Config tab.
2. Enter invalid values and confirm the UI marks them invalid.
3. Enter `tcp|203.0.113.10:9000/30` and octet `15`.
4. Confirm the full IP preview shows `10.0.10.15` and the profile becomes valid.

## Completion Criteria

- The profile contract is encoded in shared code, not duplicated in views.
- Invalid config blocks future connect actions through viewmodel state.
- The allowed endpoint IP range matches the V1 planning decisions.

## Out Of Scope

- Saving the profile.
- Real connection logic.
- Duplicate-IP detection against the server.
