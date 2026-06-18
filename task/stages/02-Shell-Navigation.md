# Stage 02 - Shell Navigation

## Goal

Build the shared Avalonia shell with the four top-level screens: Dashboard, Config, Logs, and About.

## Why This Stage Exists

- The product requirements are tab-oriented from the start.
- A stable shell lets later slices swap fake services for real ones without relayout churn.
- It gives a human-verifiable app structure immediately.

## User-Visible Value

The macOS desktop host becomes a real multi-screen app instead of a placeholder window.

## Create Or Modify

- Add `MainViewModel` with tab selection state.
- Add `DashboardView`, `ConfigView`, `LogsView`, and `AboutView` as separate views.
- Add the shared `MainWindow` or `MainView` composition for the desktop host.
- Add theme resources for dark-only V1 styling placeholders.

## Technical Decisions

- Use compiled bindings with `x:DataType` on every root view.
- Keep code-behind empty except `InitializeComponent()`.
- Use a single top-level shell with four persistent tabs.
- Start simple with fixed tab placement; adaptive layout comes later.

## Implementation Steps

1. Add a shared shell view with four tabs and placeholder content regions.
2. Bind selected tab index to `MainViewModel`.
3. Make the first-launch default tab configurable through the viewmodel, even if it is hardcoded for now.
4. Add minimal dark resources so the app already matches the V1 visual direction.

## Automated Verification

- `dotnet build`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- `MainViewModel` default tab selection.
- `MainViewModel` tab switching behavior.

## Manual Verification

1. Run `dotnet run --project src/PigeonPost.VpnClientView.Desktop/`.
2. Switch between Dashboard, Config, Logs, and About.
3. Confirm each tab renders distinct placeholder content.

## Completion Criteria

- The shared shell exists and compiles cleanly.
- All four tabs are reachable.
- No business logic is embedded in code-behind.

## Out Of Scope

- Real data binding for config, logs, or runtime state.
- Adaptive sidebar layout.
- Accessibility polish.
