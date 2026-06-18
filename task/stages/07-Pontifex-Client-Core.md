# Stage 07 - Pontifex Client Core

## Goal

Replace the fake runtime with a real shared Pontifex client runtime that reuses the existing PigeonPost packet bridge shape but not the Linux-specific client app.

## Why This Stage Exists

- The new client must use the same protocol and raw IPv4 transport as current PigeonPost clients.
- `ClientApp` and `ClientSideLogic` are too Linux-TUN-centric to reuse directly.
- `BridgeImpl` plus `ITunDevice` is the smallest existing reusable data-plane shape.

## User-Visible Value

The app can establish a real PigeonPost client session and show actual transport state instead of simulation.

## Create Or Modify

- Add the real `VpnClientRuntime` in `PigeonPost.Vpn`.
- Add a `TransportFactory` setup parallel to the current `BaseApp`.
- Add a UI-facing diagnostics adapter for Scriba/Pontifex logs.
- Add a counting `ITunDevice` decorator so counters work without changing `BridgeImpl`.
- Make the smallest necessary `PigeonPost.Bridge` surface public for reuse.

## Technical Decisions

- Reuse `BridgeImpl`, `ClientHandshake`, `HandshakeCodec`, and `ITunDevice`.
- Do not reuse `ClientApp` or `ClientSideLogic`.
- Prefer one tiny bridge extraction over copying bridge logic into the new project.
- If needed, add a typed handshake rejection exception so UI mapping is not string-based.
- Add a debug-only in-process `direct|` harness for local manual verification.

## Implementation Steps

1. Build a new runtime that owns `BridgeImpl`, `ClientHandshake`, transport factory, reconnect loop, and shutdown flow.
2. Wrap the selected `ITunDevice` in a counting decorator to produce sent/received/session totals.
3. Forward Pontifex and runtime logs into the UI log collector.
4. Add the smallest bridge-library change required for client handler reuse and typed handshake failures.
5. Wire the desktop host to the real runtime through DI, keeping the fake runtime available only for tests if helpful.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.Bridge.Tests/`

Suggested NUnit coverage:

- Direct transport connect/disconnect.
- Duplicate IP rejection surfaces as a typed failure.
- Unexpected disconnect triggers reconnect.
- Counter decorator measures bytes correctly.

## Manual Verification

1. Run the desktop host in a debug configuration with the in-process direct harness enabled.
2. Connect using a `direct|...` profile or an equivalent debug toggle.
3. Confirm the UI reaches real Connected state and disconnects cleanly.
4. Force a duplicate-IP scenario and confirm a clear rejection appears in logs or UI state.

## Completion Criteria

- The fake runtime is no longer the production path.
- The new runtime speaks the real PigeonPost handshake and payload protocol.
- Linux runtime behavior is unchanged except for minimal reusable bridge-surface extraction.

## Out Of Scope

- Android `VpnService` integration.
- macOS synthetic probes.
- Endpoint isolation policy.
