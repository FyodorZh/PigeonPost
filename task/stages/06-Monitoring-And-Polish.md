# Stage 06 - Monitoring And Polish

## Goal

Finish the V1 shared UI behavior around monitoring, adaptive layout, About data, and accessibility while still using the fake runtime.

## Why This Stage Exists

- The UI document already defines most of the user-facing behavior.
- It is cheaper to polish the shared shell before platform-specific runtime integration.
- Avalonia compiled XAML can already verify most binding and view wiring.

## User-Visible Value

The app now looks and behaves like a real V1 client: status, counters, chart, logs, warning banner, and About screen all work.

## Create Or Modify

- Complete the Dashboard layout and state styling.
- Add the reconnect-required banner in Config.
- Add the log list UI and verbosity descriptions.
- Add the About view model from assembly metadata.
- Add the speed history ring buffer and chart control.
- Add adaptive layout for narrow and wide windows.

## Technical Decisions

- Keep the chart lightweight and custom-drawn; do not add a third-party chart package.
- Use a 30-sample ring buffer at 1 sample per second.
- On wide screens, move top-level navigation to a left-side layout.
- Add `AutomationProperties.Name` to all interactive controls.

## Implementation Steps

1. Finish the Dashboard bindings and button state text.
2. Add the reusable speed history buffer model and chart control.
3. Add the Config reconnect warning banner.
4. Add the Logs list rendering and verbosity explanations.
5. Add About content from assembly version info.
6. Add adaptive layout using container queries or a simple width breakpoint.

## Automated Verification

- `dotnet build`
- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Speed history buffer keeps the last 30 samples.
- About view model exposes version/build text.
- Config warning visibility depends on connection state.
- Dashboard text formatting is stable.

## Manual Verification

1. Launch the desktop host and connect through the fake runtime.
2. Confirm the chart moves, counters update, and logs grow.
3. Resize the window past the desktop breakpoint and confirm navigation adapts.
4. Confirm the About tab shows real assembly metadata.

## Completion Criteria

- The shared UI is feature-complete for V1, even though transport is still fake.
- Resize behavior works on macOS desktop.
- Accessibility names exist for all key controls.

## Out Of Scope

- Real network transport.
- Platform-specific service integration.
- Packaging and signing.
