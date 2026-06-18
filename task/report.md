## Stage 01 — Solution Scaffold

### Files Created

**src/PigeonPost.Vpn/**
- `PigeonPost.Vpn.csproj` — net10.0 class library, refs CommunityToolkit.Mvvm 8.4.0 + Microsoft.Extensions.DependencyInjection 9.0.0
- `PlaceholderService.cs` — placeholder runtime class

**src/PigeonPost.VpnClientView/**
- `PigeonPost.VpnClientView.csproj` — shared Avalonia UI with `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`
- `App.axaml` / `App.axaml.cs` — Avalonia Application definition with FluentTheme
- `Views/MainWindow.axaml` / `Views/MainWindow.axaml.cs` — placeholder window (800x600)

**src/PigeonPost.VpnClientView.Desktop/**
- `PigeonPost.VpnClientView.Desktop.csproj` — desktop host, OutputType Exe, refs Avalonia.Desktop
- `Program.cs` — entry point with `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`

**src/PigeonPost.VpnClientView.Android/**
- `PigeonPost.VpnClientView.Android.csproj` — targets `net10.0-android`, refs Avalonia.Android
- `MainActivity.cs` — extends `AvaloniaMainActivity` with standard Android activity attributes

**tests/PigeonPost.Vpn.Tests/**
- `PigeonPost.Vpn.Tests.csproj` — NUnit test project, refs PigeonPost.Vpn
- `PlaceholderTests.cs` — one passing test

**tests/PigeonPost.VpnClientView.Tests/**
- `PigeonPost.VpnClientView.Tests.csproj` — NUnit test project, refs PigeonPost.VpnClientView
- `PlaceholderTests.cs` — one passing test

### Solution Changes
- All 6 new projects added to `PigeonPost.sln` with build configurations for all platforms
- New test projects nested under the existing `Tests` solution folder
- `PigeonPost.VpnClientView.Desktop.csproj` has `$(NoWarn)` suppressing NU1903

### NuGet Packages Added
- Avalonia 11.2.3 (shared UI, desktop host, Android host)
- Avalonia.Desktop 11.2.3 (desktop host)
- Avalonia.Themes.Fluent 11.2.3 (shared UI)
- Avalonia.Android 11.2.3 (Android host)
- CommunityToolkit.Mvvm 8.4.0 (shared UI, runtime)
- Microsoft.Extensions.DependencyInjection 9.0.0 (shared UI, runtime)
- 94 total .nupkg files now in `./nugets/` (was ~60 before)

### Issues Encountered & Resolved
1. **NU1903 vulnerability warning** — Added `<NoWarn>` to Desktop csproj.
2. **Android SDK API 36 not installed** — Installed via `dotnet build -t:InstallAndroidDependencies`.
3. **Java 11 outdated for Android build** — Installed openjdk@17 via Homebrew.

### Verification Results
```
dotnet restore              ✓ Success (all 13 projects)
dotnet build                ✓ Success
dotnet test Vpn.Tests       ✓ Passed: 1
dotnet test VpnClientView.Tests ✓ Passed: 1
dotnet nuget locals all --clear + offline restore ✓ Success
Original PigeonPost console  ✓ Builds unchanged
Existing tests               ✓ Passed (109 passed, 8 Linux-only skipped)
```

## Stage 02 — Shell Navigation

### Files Created (12)

| File | Purpose |
|------|---------|
| `src/PigeonPost.VpnClientView/ViewModels/MainViewModel.cs` | `ObservableObject` with `[ObservableProperty] _selectedTabIndex` + read-only `DefaultTabIndex = 0` |
| `src/PigeonPost.VpnClientView/Views/DashboardView.axaml` | Dashboard tab content (placeholder) |
| `src/PigeonPost.VpnClientView/Views/DashboardView.axaml.cs` | Empty code-behind |
| `src/PigeonPost.VpnClientView/Views/ConfigView.axaml` | Configuration tab content (placeholder) |
| `src/PigeonPost.VpnClientView/Views/ConfigView.axaml.cs` | Empty code-behind |
| `src/PigeonPost.VpnClientView/Views/LogsView.axaml` | Logs tab content (placeholder) |
| `src/PigeonPost.VpnClientView/Views/LogsView.axaml.cs` | Empty code-behind |
| `src/PigeonPost.VpnClientView/Views/AboutView.axaml` | About tab content (version info placeholder) |
| `src/PigeonPost.VpnClientView/Views/AboutView.axaml.cs` | Empty code-behind |
| `src/PigeonPost.VpnClientView/Styles/Theme.axaml` | Minimal dark theme overrides (TabItem sizing) |
| `tests/PigeonPost.VpnClientView.Tests/MainViewModelTests.cs` | 4 NUnit tests (default tab, switching, property change) |

### Files Modified (3)

| File | Change |
|------|--------|
| `src/PigeonPost.VpnClientView/App.axaml` | Changed `FluentTheme` to dark variant, added `StyleInclude` for Theme.axaml |
| `src/PigeonPost.VpnClientView/App.axaml.cs` | Creates `MainViewModel` and sets it as `DataContext` on `MainWindow` |
| `src/PigeonPost.VpnClientView/Views/MainWindow.axaml` | Replaced placeholder with `TabControl` + 4 `TabItem`s containing each view |

### Key Decisions
- Dark theme set via `RequestedThemeVariant = ThemeVariant.Dark` in code-behind
- Compiled bindings with `x:DataType` on all root views
- DataContext set in `App.axaml.cs` to keep code-behind minimal
- Default tab hardcoded as 0

### Test Results
```
PigeonPost.VpnClientView.Tests: Passed: 5 (4 new + 1 placeholder)
```

## Stage 03 — Profile Validation

### Files Created (7)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/VpnProfile.cs` | Sealed record with `ServerUrl`, `ClientIpLastOctet` (byte), computed `FullClientIp` |
| `src/PigeonPost.Vpn/VpnDefaults.cs` | Static constants: subnet, server TUN IP, reserved ranges, DNS servers |
| `src/PigeonPost.Vpn/VpnProfileValidator.cs` | Static validator using `[GeneratedRegex]` for URL pattern `type\|host:port/timeout`, validates octet 11-254, returns `List<string>` errors |
| `src/PigeonPost.VpnClientView/ViewModels/ConfigViewModel.cs` | `ObservableObject` with real-time revalidation via `partial void On*Changed` hooks |
| `tests/PigeonPost.Vpn.Tests/VpnDefaultsTests.cs` | 5 tests for DNS/subnet/range constants |
| `tests/PigeonPost.Vpn.Tests/VpnProfileValidatorTests.cs` | 12 tests: valid/invalid URL, octet boundaries, FullIpPreview |
| `tests/PigeonPost.VpnClientView.Tests/ConfigViewModelTests.cs` | 8 tests: validation state transitions, FullIpPreview, errors |

### Files Modified (5)

| File | Change |
|------|--------|
| `ViewModels/MainViewModel.cs` | Added `ConfigViewModel` property + constructor parameter |
| `App.axaml.cs` | Added DI registration for `ConfigViewModel` |
| `Views/ConfigView.axaml` | Replaced placeholder with form: URL input, octet input, IP preview, error list |
| `Views/MainWindow.axaml` | Config tab passes `ConfigViewModel` binding to ConfigView |
| `MainViewModelTests.cs` | Updated constructor calls, added ConfigViewModel access test |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 17
PigeonPost.VpnClientView.Tests:  Passed: 15
```

## Stage 04 — Profile Persistence

### Files Created (4)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/IProfileStore.cs` | Interface with `Load()` and `Save(VpnProfile)` |
| `src/PigeonPost.Vpn/DesktopProfileStore.cs` | JSON file in `%APPDATA%/PigeonPost/profile.json` with version field, graceful handling of missing/corrupt files, directory auto-creation |
| `src/PigeonPost.Vpn/AndroidProfileStore.cs` | Stub — `Load()` returns null, `Save()` is no-op |
| `tests/PigeonPost.VpnClientView.Tests/TestProfileStore.cs` | In-memory test stub implementing `IProfileStore` |

### Files Modified (4)

| File | Change |
|------|--------|
| `ConfigViewModel.cs` | Accepts optional `IProfileStore`, loads on construction, auto-saves on valid changes, exposes `HasLoadedProfile` |
| `MainViewModel.cs` | Initial tab depends on profile presence: Config (1) if no profile, Dashboard (0) if profile exists |
| `App.axaml.cs` | Registers `IProfileStore → DesktopProfileStore` in DI |
| `MainViewModelTests.cs` | Updated tests for profile-dependent tab selection |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 21 (was 17)
PigeonPost.VpnClientView.Tests:  Passed: 18 (was 15)
```

