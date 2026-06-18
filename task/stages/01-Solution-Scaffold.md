# Stage 01 - Solution Scaffold

## Goal

Create the new projects, solution wiring, and package baselines for the multiplatform VPN client without changing existing Linux runtime behavior.

## Why This Stage Exists

- Every later slice depends on stable project boundaries.
- This repo already uses `src/` and `tests/`; keeping the new work in the same layout reduces churn.
- The local `./nugets` feed must be updated the first time new packages appear.

## User-Visible Value

The future client can be launched as a desktop Avalonia app on macOS with a placeholder window, so the new product exists as a runnable artifact from the first slice.

## Create Or Modify

- Add `src/PigeonPost.Vpn/` as a `net10.0` class library.
- Add `src/PigeonPost.VpnClientView/` as the shared Avalonia UI project.
- Add `src/PigeonPost.VpnClientView.Desktop/` as the desktop host used for macOS V1 development and manual verification.
- Add `src/PigeonPost.VpnClientView.Android/` as the Android host project skeleton.
- Add `tests/PigeonPost.Vpn.Tests/` for shared runtime tests.
- Add `tests/PigeonPost.VpnClientView.Tests/` for viewmodel and UI-adjacent tests.
- Add all new projects to `PigeonPost.sln`.

## Technical Decisions

- Keep host projects under `src/`, not `platform/`, to match the existing repository structure.
- Enable compiled bindings in the shared UI project with `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`.
- Use `CommunityToolkit.Mvvm` and `Microsoft.Extensions.DependencyInjection`.
- Do not introduce `ReactiveUI`.
- Do not reuse `ClientApp` or `ClientSideLogic` in the new runtime.

## Implementation Steps

1. Create the new csproj files and add minimal references only.
2. Add a placeholder `App.axaml`, `App.axaml.cs`, and a single placeholder view in `PigeonPost.VpnClientView`.
3. Add the desktop host entry point and make it open the shared app on macOS.
4. Add an empty Android host project that compiles, even if it does not run any VPN logic yet.
5. Add empty NUnit test projects with one trivial passing test each.
6. Update the local NuGet feed after adding packages, then verify offline restore.

## Automated Verification

- `dotnet restore`
- `dotnet build`
- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`
- `dotnet nuget locals all --clear`
- `dotnet restore --packages /tmp/nuget-verify`

## Manual Verification

1. Run `dotnet run --project src/PigeonPost.VpnClientView.Desktop/`.
2. Confirm a window opens with a visible placeholder app shell.
3. Confirm the original `PigeonPost` console app still builds unchanged.

## Completion Criteria

- The solution contains the new runtime, shared UI, desktop host, Android host, and test projects.
- The desktop host launches on macOS.
- Offline restore still works with the local `./nugets` feed.
- No existing Linux runtime files are behaviorally changed.

## Out Of Scope

- Real views beyond a placeholder shell.
- Any real transport, VPN, or persistence logic.
- Packaging, signing, or deployment scripts.
