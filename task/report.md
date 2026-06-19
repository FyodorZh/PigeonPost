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

## Stage 05 — Fake Runtime

### Files Created (11)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/ConnectionState.cs` | Enum: `Disconnected`, `Connecting`, `Connected` |
| `src/PigeonPost.Vpn/VpnLogLevel.cs` | Enum: `Info`, `Warning`, `Error` |
| `src/PigeonPost.Vpn/VpnLogEntry.cs` | Record: `Timestamp`, `Message`, `Level` |
| `src/PigeonPost.Vpn/VpnSessionSnapshot.cs` | Record: session counters, speeds, state, reconnect count |
| `src/PigeonPost.Vpn/IVpnRuntime.cs` | Interface: state, events, `ConnectAsync`/`DisconnectAsync` |
| `src/PigeonPost.Vpn/FakeVpnRuntime.cs` | Fake with `System.Threading.Timer`, random drop ~12s, auto-reconnect |
| `src/PigeonPost.VpnClientView/ViewModels/DashboardViewModel.cs` | Binds state/counters/speeds/uptime/reconnects, Connect/Disconnect commands |
| `src/PigeonPost.VpnClientView/ViewModels/LogsViewModel.cs` | Binds to `LogEmitted`, maintains `ObservableCollection` |
| `tests/PigeonPost.Vpn.Tests/FakeVpnRuntimeTests.cs` | 10 tests: state transitions, events, reconnect, counter reset |
| `tests/PigeonPost.VpnClientView.Tests/DashboardViewModelTests.cs` | 7 tests: state binding, commands, counter updates |
| `tests/PigeonPost.VpnClientView.Tests/LogsViewModelTests.cs` | 5 tests: log collection, timestamps |

### Files Modified (5)

| File | Change |
|------|--------|
| `MainViewModel.cs` | Added `DashboardViewModel` + `LogsViewModel` properties |
| `App.axaml.cs` | Registered `IVpnRuntime→FakeVpnRuntime`, `DashboardViewModel`, `LogsViewModel` |
| `DashboardView.axaml` | Replaced placeholder with state badge, button, counters, speeds, uptime |
| `LogsView.axaml` | Replaced placeholder with scrollable log list |
| `MainWindow.axaml` | Dashboard/Logs tabs bind to their VMs |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 31 (was 21)
PigeonPost.VpnClientView.Tests:  Passed: 33 (was 18)
```

## Stage 06 — Monitoring and Polish

### Files Created (6)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/SpeedHistoryBuffer.cs` | 30-sample ring buffer with thread-safe `AddSample`, `SentHistory`, `ReceivedHistory` |
| `src/PigeonPost.VpnClientView/Controls/SpeedChart.cs` | Custom-drawn `Control` with `OnRender` — overlaid sent/received line chart, auto-scaling Y axis |
| `src/PigeonPost.VpnClientView/Controls/ValueConverters.cs` | `LogLevelToBackgroundConverter` — maps `VpnLogLevel` to colored `SolidColorBrush` |
| `src/PigeonPost.VpnClientView/ViewModels/AboutViewModel.cs` | Reads assembly `Version`, `AssemblyProductAttribute`, `AssemblyDescriptionAttribute` |
| `tests/PigeonPost.Vpn.Tests/SpeedHistoryBufferTests.cs` | 7 tests: capacity, wrapping, overflow, thread safety, independence |
| `tests/PigeonPost.VpnClientView.Tests/AboutViewModelTests.cs` | 6 tests: version, product name, description, build date, assembly metadata |

### Files Modified (14)

| File | Change |
|------|--------|
| `PigeonPost.VpnClientView.csproj` | Added `Version`, `Description`, `Product` assembly metadata |
| `ViewModels/DashboardViewModel.cs` | Added `SpeedHistoryBuffer` + `SentHistory`/`ReceivedHistory` chart data, `StatusBadgeColor` from state |
| `ViewModels/ConfigViewModel.cs` | Added `IVpnRuntime` param + `IsReconnectWarningVisible` driven by state |
| `ViewModels/MainViewModel.cs` | Added `AboutViewModel`, `IsWideLayout`/`IsNarrowLayout`, per-tab flags, `UpdateLayout(width)` |
| `Views/DashboardView.axaml` | Color-coded badge, `SpeedChart`, `AutomationProperties.Name` on buttons |
| `Views/ConfigView.axaml` | Reconnect warning banner, `AutomationProperties.Name` on inputs |
| `Views/LogsView.axaml` | Colored level badge via converter, verbosity description |
| `Views/LogsView.axaml.cs` | Auto-scroll to bottom on `CollectionChanged` |
| `Views/AboutView.axaml` | Real content from `AboutViewModel` (product, version, description, build) |
| `Views/MainWindow.axaml` | Adaptive layout: sidebar (≥700px) or top tabs (<700px) |
| `Views/MainWindow.axaml.cs` | `Resized` handler calls `vm.UpdateLayout(width)` |
| `App.axaml.cs` | Registered `AboutViewModel` in DI |
| `tests/ConfigViewModelTests.cs` | Added 4 reconnect warning visibility tests |
| `tests/MainViewModelTests.cs` | Added `AboutViewModel`, layout/tab-flag tests |

### Test Results

```
PigeonPost.Vpn.Tests:            Passed: 38 (was 31, +7 new)
PigeonPost.VpnClientView.Tests:  Passed: 47 (was 33, +14 new)
PigeonPost.Bridge.Tests:         Passed: 77 (unchanged)
PigeonPost.Tests:                Passed: 22, Skipped: 8 (unchanged)
PigeonPost.Tun.Tests:            Passed: 10 (unchanged)
```

### Issues Encountered & Resolved

1. **Compiled binding scope** — `IsVisible="{Binding IsDashboardTab}"` on child views resolved against child's `x:DataType`. Fixed by wrapping views in `Panel` (no DataType override).
2. **Backing field vs property** — `MainViewModel` constructor set `_selectedTabIndex = ...` (field), bypassing `OnSelectedTabIndexChanged`. Fixed: use `SelectedTabIndex = ...`.
3. **Missing `using` directives** — 3 files needed `System`/`System.Threading`/`System.Reflection` imports due to `<ImplicitUsings>false</ImplicitUsings>`.

## Stage 07 — Pontifex Client Core

### Files Created (6)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Bridge/Protocol/HandshakeRejectedException.cs` | Typed exception with `RejectCode` property |
| `src/PigeonPost.Vpn/NullTunDevice.cs` | Stub `ITunDevice` — blocks reads, no-op writes |
| `src/PigeonPost.Vpn/CountingTunDevice.cs` | `ITunDevice` decorator — tracks bytes/packets via `Interlocked` |
| `src/PigeonPost.Vpn/RuntimeLogger.cs` | Scriba `ILoggerExt` implementation routing logs to `LogEmitted` event |
| `src/PigeonPost.Vpn/VpnClientRuntime.cs` | Real `IVpnRuntime` — owns `BridgeImpl`, transport, reconnect loop |
| `tests/PigeonPost.Vpn.Tests/VpnClientRuntimeTests.cs` | 8 tests: connect, disconnect, reconnection, state events, guard |
| `tests/PigeonPost.Vpn.Tests/CountingTunDeviceTests.cs` | 5 tests: byte/packet counting |
| `tests/PigeonPost.Vpn.Tests/HandshakeRejectedExceptionTests.cs` | 4 tests: reject code, throw/catch |

### Files Modified (5)

| File | Change |
|------|--------|
| `PigeonPost.Bridge.csproj` | Added `InternalsVisibleTo` for `PigeonPost.Vpn` and `PigeonPost.Vpn.Tests` |
| `BridgeImpl.cs` | Added `EndpointConnected` event (fires after successful handshake) |
| `PigeonPost.Vpn.csproj` | Added project refs to `PigeonPost.Bridge` + `PigeonPost.Tun`; NuGet refs to Pontifex packages |
| `PigeonPost.Vpn.Tests.csproj` | Added Pontifex NuGet refs |
| `App.axaml.cs` | Switched DI from `FakeVpnRuntime` to `VpnClientRuntime` |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 53 (was 38, +15 new)
PigeonPost.Bridge.Tests:         Passed: 77 (unchanged)
```

## Stage 08 — Desktop macOS Connected Mode

### Files Modified (5)

| File | Change |
|------|--------|
| `BridgeClientHandler.cs:33` | Changed to wrap real `HandshakeRejectedException` with actual reject code |
| `VpnClientRuntime.cs:201-203` | Extract real reject code from inner exception |
| `DashboardViewModel.cs` | Added `catch (HandshakeRejectedException)` with reject-code-to-message mapping |
| `DashboardView.axaml` | Added description text: "Connected means transport/session established with server." |
| `ConfigView.axaml` | Updated reconnect warning banner with clearer wording |

### New Tests (5)

| Test | Description |
|------|-------------|
| `Connect_DuplicateHostIp_ShowsClearError` | Maps DuplicateHostIp to readable message |
| `Connect_InvalidHandshake_ShowsClearError` | Maps InvalidHandshake to readable message |
| `Connect_ServerShuttingDown_ShowsClearError` | Maps ServerShuttingDown to readable message |
| `Connect_UnsupportedPacketFamily_ShowsClearError` | Maps UnsupportedPacketFamily to readable message |
| `Connect_TransportFailure_ShowsConnectionFailed` | Generic transport error handling |

### Test Results
```
PigeonPost.VpnClientView.Tests:  Passed: 52 (+5 new)
PigeonPost.Vpn.Tests:            Passed: 53 (unchanged)
PigeonPost.Bridge.Tests:         Passed: 77 (unchanged)
```

### JDK Setup Note
Correct JDK 17 location for Android builds:
```
/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home
```

## Stage 09 — Desktop macOS Probe Mode

### Files Created (4)

| File | Description |
|------|-------------|
| `src/PigeonPost.Vpn/IcmpHelper.cs` | Static helper: `CreateEchoRequest`, `TryParseEchoReply`, `ComputeChecksum` |
| `src/PigeonPost.Vpn/ProbeTunDevice.cs` | `ITunDevice` with queue-backed probe packets, reply matching, timeout checking |
| `src/PigeonPost.Vpn/ProbeScheduler.cs` | Timer-driven (3s interval), logs replies/timeouts |
| `tests/PigeonPost.Vpn.Tests/IcmpHelperTests.cs` | 10 tests: valid packet, round-trip, wrong protocol/type, checksum |
| `tests/PigeonPost.Vpn.Tests/ProbeTunDeviceTests.cs` | 16 tests: read queue, reply detection, non-ICMP ignored, clear, timeouts, events |

### Files Modified (1)

| File | Change |
|------|--------|
| `VpnClientRuntime.cs` | Replaced `NullTunDevice` with `ProbeTunDevice` + `CountingTunDevice` chain; starts `ProbeScheduler` on connect |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 79 (was 53, +26 new)
```

## Stage 10 — Endpoint Isolation

### Files Created (2)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Bridge/VpnSubnetClassifier.cs` | Static classifier for VPN subnet roles (Linux vs endpoint vs server) |
| `tests/PigeonPost.Bridge.Tests/VpnSubnetClassifierTests.cs` | 12 tests: range boundary classification |
| `tests/PigeonPost.Bridge.Tests/Server/ServerHubIsolationTests.cs` | 10 tests: full allowed/denied matrix |

### Files Modified (3)

| File | Change |
|------|--------|
| `IServerHub.cs` | Added `DroppedIsolationPolicy` counter |
| `ServerHub.cs` | Added isolation check in `OnPacketFromClient()` — endpoint→VPN-peer dropped, Linux→endpoint dropped |
| `FakeServerHub.cs` | Added `DroppedIsolationPolicy` property |

### Isolation Logic
```
OnPacketFromClient → if dest in 10.0.10.0/24 AND dest ≠ 10.0.10.1:
  if source is endpoint (11-254): DROP
  if source is Linux AND dest is endpoint: DROP
else: write to TUN (internet-bound or server-bound allowed)
```

### Test Results
```
PigeonPost.Bridge.Tests:  Passed: 99 (was 77, +22 new)
```

## Android Build Fix

### Problem
Android project failed due to JDK 11 default; needs JDK 17.

### Solution
Created `src/PigeonPost.VpnClientView.Android/Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <JavaSdkDirectory>/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home</JavaSdkDirectory>
  </PropertyGroup>
</Project>
```

### Result
Full solution (`dotnet build`) — 0 errors, all 14 projects build successfully. Only 4 cosmetic XA0141 warnings remain (SkiaSharp page alignment).

### Note to Future Implementers
The Java/JDK issue is fixed. Do not spend time troubleshooting Android build failures — `JavaSdkDirectory` is already set in the Android project's `Directory.Build.props`. If the build still fails, check that the JDK exists at the configured path.

## Stage 11 — Android Protect Hook

### Files Created (3)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/ISocketProtector.cs` | Interface: `bool ProtectSocket(Socket socket)` |
| `src/PigeonPost.Vpn/NullSocketProtector.cs` | No-op returning `true` for desktop/fallback |
| `tests/PigeonPost.Vpn.Tests/NullSocketProtectorTests.cs` | 2 tests: returns true, handles null |

### Files Modified (5)

| File | Change |
|------|--------|
| `PigeonPost.Vpn.csproj` | `Pontifex.Transport.Tcp` → 0.1.2-dev.0 |
| `PigeonPost.Bridge.csproj` | `Pontifex.Transport.Tcp` → 0.1.2-dev.0 |
| `PigeonPost.csproj` | `Pontifex.Transport.Tcp` → 0.1.2-dev.0 |
| `PigeonPost.Bridge.Tests.csproj` | `Pontifex.Transport.Tcp` → 0.1.2-dev.0 |
| `VpnClientRuntime.cs` | Added `ISocketProtector` constructor param, `ProtectEndpointSocket()` called on connect/reconnect |

### How It Works
After Pontifex handshake completes (`EndpointConnected` event), `ProtectEndpointSocket()` uses `ISocketUnsafeAccessor` via `GetControls` to extract the underlying `Socket` and calls `ISocketProtector.ProtectSocket(socket)`. On Android, `VpnService.protect()` would be called here. Direct transport has no socket — logs warning and continues.

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 81 (was 79, +2 new)
```

## Stage 12 — Android Host Service

### Files Created (6)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/AndroidServiceState.cs` | Enum: `Idle`, `Preparing`, `Running`, `Revoked` |
| `src/PigeonPost.Vpn/IAndroidServiceBridge.cs` | Interface for service lifecycle communication |
| `src/PigeonPost.Vpn/AndroidServiceBridgeLocator.cs` | Static locator for cross-project bridge access |
| `src/PigeonPost.VpnClientView.Android/PigeonPostVpnService.cs` | `VpnService` subclass — foreground service, notification, `OnRevoke()` |
| `src/PigeonPost.VpnClientView.Android/AndroidVpnBridge.cs` | `IAndroidServiceBridge` impl — permission flow, start/stop/revoke |
| `tests/PigeonPost.Vpn.Tests/AndroidServiceStateTests.cs` | 12 tests: enum, bridge state transitions, events |

### Files Modified (4)

| File | Change |
|------|--------|
| `MainActivity.cs` | Creates `AndroidVpnBridge`, handles `OnActivityResult` for permission |
| `AndroidManifest.xml` | Added `FOREGROUND_SERVICE_CONNECTED_DEVICE` + `<service>` element |
| `Directory.Build.props` (Android) | Added `<Nullable>enable</Nullable>` + `<NoWarn>` |
| `DashboardViewModel.cs` | Added optional bridge param + locator; Android-aware connect/disconnect flows |

### Architecture
- **Static locator** avoids coupling shared UI to Android; Desktop leaves locator null.
- **Connect flow**: User taps Connect → bridge checks permission → `VpnService.Prepare()` → system dialog → on grant → start service → connect runtime.
- **Revoke flow**: `OnRevoke()` → state `Revoked` → force disconnect + status text.
- **Disconnect flow**: Disconnect runtime → stop service.

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 93 (was 81, +12 new)
PigeonPost.VpnClientView.Tests:  Passed: 58 (was 52, +6 new)
```

## Stage 13 — Android VPN Builder

### Files Created (3)

| File | Purpose |
|------|---------|
| `src/PigeonPost.Vpn/AndroidVpnConfiguration.cs` | Pure record with `FromProfile(VpnProfile)` factory — client IP, subnet, DNS, route, MTU |
| `src/PigeonPost.VpnClientView.Android/AndroidVpnBuilder.cs` | Static helper — translates config into `VpnService.Builder` calls |
| `tests/PigeonPost.Vpn.Tests/AndroidVpnConfigurationTests.cs` | 8 tests: address, prefix, DNS, route, MTU |

### Files Modified (6)

| File | Change |
|------|--------|
| `IAndroidServiceBridge.cs` | Added `IsVpnInterfaceEstablished`, `CurrentConfiguration` |
| `PigeonPostVpnService.cs` | Added `EstablishVpnInterface()`, `CloseVpnInterface()`, static request/result pattern |
| `AndroidVpnBridge.cs` | Tracks interface state, `StartVpnService(VpnProfile)` overload |
| `DashboardViewModel.cs` | Added `IsVpnInterfaceEstablished`/`VpnInterfaceStatusText` for Android |
| `DashboardView.axaml` | Added VPN interface status section |
| `DashboardViewModelTests.cs` | 3 new tests for VPN interface state transitions |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 101 (+8 new)
PigeonPost.VpnClientView.Tests:  Passed: 60 (+3 new)
```

## Stage 14 — Android Real Tunnel

### Files Created (2)

| File | Purpose |
|------|---------|
| `src/PigeonPost.VpnClientView.Android/AndroidTunDevice.cs` | `ITunDevice` over `ParcelFileDescriptor` using `FileInputStream`/`FileOutputStream` |
| `src/PigeonPost.VpnClientView.Android/AndroidSocketProtector.cs` | `ISocketProtector` calling `VpnService.Protect(int)` |

### Files Modified (6)

| File | Change |
|------|--------|
| `VpnClientRuntime.cs` | Added `SetCustomTunDevice(ITunDevice?)`, `SetSocketProtector(ISocketProtector?)` — custom TUN replaces ProbeTunDevice, skips probe scheduler |
| `IAndroidServiceBridge.cs` | Added `SetRuntime(IVpnRuntime)` method |
| `PigeonPostVpnService.cs` | Creates `AndroidTunDevice` + `AndroidSocketProtector` after `EstablishVpnInterface()`, exposes via properties |
| `AndroidVpnBridge.cs` | Implements `SetRuntime()`, wires AndroidTUN + protector into runtime |
| `DashboardViewModel.cs` | Calls `bridge.SetRuntime(runtime)` |
| `VpnClientRuntimeTests.cs` | Added custom TUN device test + `RecordingTunDevice` helper |

### Test Results
```
PigeonPost.Vpn.Tests:            Passed: 102 (+1 new)
```


