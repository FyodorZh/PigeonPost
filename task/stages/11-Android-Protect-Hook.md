# Stage 11 - Android Protect Hook

## Goal

Prove and implement a safe way to protect the outbound Pontifex transport socket from being routed through the Android VPN itself.

## Why This Stage Exists

- Real Android VPN clients must call `VpnService.protect(...)` on the control socket.
- The current Pontifex TCP transport comes from a NuGet package and does not obviously expose the raw socket at the right time.
- This is the highest-risk Android integration point and must be resolved before the real Android tunnel slice.

## User-Visible Value

An Android diagnostic build can start the VPN service path and report that the tunnel control connection is protected correctly, instead of silently looping traffic.

## Create Or Modify

- Add an Android-specific transport seam such as `ISocketProtector`, `IProtectedTransportFactory`, or an equivalent abstraction.
- If the existing Pontifex package cannot support this cleanly, patch or fork the TCP client into the local `./nugets` feed with the smallest possible callback surface.
- Add a small diagnostic UI or log path that reports whether the protect callback actually ran.

## Technical Decisions

- Prefer a tiny transport callback patch over rewriting Pontifex in the app layer.
- Keep the PigeonPost handshake and payload protocol unchanged.
- Treat this stage as a hard gate: do not proceed to full Android packet pumping until protectability is proven.

## Implementation Steps

1. Inspect the current Pontifex TCP client construction path.
2. Introduce the smallest seam that exposes the raw socket or file descriptor before data flows.
3. Call `VpnService.protect(...)` from Android code through that seam.
4. Emit an explicit success/failure diagnostic event visible in the app logs.
5. If a package patch is required, update the local NuGet feed and verify offline restore again.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet restore --packages /tmp/nuget-verify`

Suggested NUnit coverage:

- The protect callback is invoked exactly once per transport instance.
- A missing protector causes a clear fail-fast error.
- The Android-specific adapter does not affect desktop runtime behavior.

## Manual Verification

1. Install the Android host on an emulator or device.
2. Start the diagnostic connection path.
3. Approve VPN permission if requested.
4. Confirm logs show that the transport socket was protected before the session is considered connected.

## Completion Criteria

- The Android control socket protection path is proven, not assumed.
- Any Pontifex patch is minimal, intentional, and locally restorable from `./nugets`.
- There is a clear red/green verification signal for this risk.

## Out Of Scope

- Full Android service lifecycle.
- Real packet pumping.
- Final Android UX polish.
