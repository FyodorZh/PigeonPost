# Stage 16 - Packaging And Acceptance

## Goal

Produce installable artifacts and a final acceptance matrix for the new multiplatform client.

## Why This Stage Exists

- A finished runtime and UI are not enough without repeatable artifact production.
- Android and macOS packaging rules differ sharply.
- This stage converts the development slices into a releasable V1 workflow.

## User-Visible Value

The project can generate a debug-installable Android package and a launchable macOS `.app` bundle, and there is a clear final checklist proving V1 behavior.

## Create Or Modify

- Add Android publish instructions or scripts.
- Add desktop-to-macOS bundle packaging scripts.
- Add signing placeholders and documentation, not hardcoded secrets.
- Add a final acceptance checklist covering shared runtime, macOS probe mode, Android real tunnel, deployment assumptions, and duplicate-IP behavior.

## Technical Decisions

- Keep secrets out of the repo; use environment variables or external files for signing material.
- Use `UseAppHost=true` for the macOS bundle.
- Treat notarization and release signing as optional for local development but required for external distribution.
- Keep the final acceptance checklist in-repo so future changes can rerun it.

## Implementation Steps

1. Add Android debug publish guidance and a release-signing template.
2. Add macOS bundle packaging with `Info.plist`, icon placement, and app host wiring.
3. Add codesign and notarization notes as documented steps, not embedded credentials.
4. Add a single acceptance checklist document that points to exact commands and manual checks.

## Automated Verification

- `dotnet build`
- `dotnet test`
- `dotnet publish` for the desktop host
- `dotnet publish -f net10.0-android` for the Android host

Suggested acceptance matrix items:

- Shared runtime unit and integration tests pass.
- Desktop/macOS host launches.
- Android package builds.
- Offline restore still succeeds.

## Manual Verification

1. Produce a desktop/macOS publish output and package it into a `.app` bundle.
2. Produce an Android debug package and install it on a device or emulator.
3. Run the full V1 checklist and confirm desktop/macOS real connect.
4. Confirm desktop/macOS periodic probe logs.
5. Confirm Android full-device VPN connect.
6. Confirm duplicate-IP rejection is shown clearly.
7. Confirm endpoint isolation.
8. Confirm deployment verifier scripts pass.

## Completion Criteria

- Installable artifacts can be produced for Android and macOS development use.
- The repo contains a repeatable V1 acceptance checklist.
- There is no undocumented manual knowledge required to validate the release.

## Out Of Scope

- App Store submission.
- iOS entitlement rollout.
- V2 features such as multiple profiles or IPv6.
