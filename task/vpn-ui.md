# PigeonPost VPN Client — UI/UX Technical Design Document

**Project:** PigeonPost VpnClientView
**Target Version:** V1 (Android + simplified macOS)
**UI Framework:** Avalonia (cross-platform: Android, iOS, macOS, Windows, Linux)
**Runtime:** .NET 10
**Status:** Draft — ready for review

---

## 1. Project Overview

### 1.1 Purpose

PigeonPost VpnClientView is a cross-platform VPN client application that connects to the existing PigeonPost server infrastructure. In V1 it targets Android and macOS simultaneously. Android establishes a real full-device VPN tunnel using the same protocol as the existing Linux TUN clients. macOS does not register a system VPN in V1, but still uses the same transport/session protocol, the same selected client IP identity, and automatic synthetic ICMP probe traffic through the tunnel.

### 1.2 V1 Scope

- **Android** (API 26+, real full-device VPN)
- **macOS** (simplified V1: no registered system VPN, real transport/session + synthetic probe traffic)
- Shared Avalonia UI that scales to desktop in future
- Shared VPN runtime (`PigeonPost.Vpn`) with no UI dependency
- One locally stored connection profile
- Manual client IP assignment from the unified VPN subnet (`10.0.10.0/24`)
- English only, dark theme only
- Immediate reconnect on disconnect

### 1.3 Target Platforms (Phased)

| Platform | V1 | V2+ |
|----------|----|-----|
| Android  | ✅ | ✅ |
| iOS      | —  | ✅ |
| macOS    | ✅ | ✅ |
| Windows  | —  | ✅ |
| Linux    | —  | ✅ |

---

## 2. User Personas & Scenarios

### 2.1 Persona: Remote Worker Alex

| Attribute | Detail |
|-----------|--------|
| **Role** | Remote employee connecting to a corporate network |
| **Devices** | Android phone + iPad (future) |
| **Goals** | Securely access company resources while travelling. Needs internet egress through the corporate VPN. |
| **Frustrations** | Complex setup, unreliable connections, unclear status. |
| **Technical level** | Moderate — can follow instructions but does not want to debug network config. |

### 2.2 Persona: Power User Dani

| Attribute | Detail |
|-----------|--------|
| **Role** | Sysadmin / DevOps engineer |
| **Devices** | Android phone + Linux laptop + macOS |
| **Goals** | Connect to test infrastructure, debug via logs, control verbosity. Wants visibility into what the tunnel is doing. |
| **Frustrations** | Opaque black-box VPNs. |
| **Technical level** | High — comfortable with IP addresses, ports, log analysis. |

### 2.3 Key Scenarios

1. **First launch:** Alex opens the app for the first time. No profile exists. He is taken directly to the Configuration tab to set up the server URL and choose a client IP.
2. **Daily connect:** Dani opens the app, sees the Dashboard with his saved config, taps the prominent Connect button, and the transport session establishes.
3. **Reconnect after outage:** The tunnel drops. The UI shows "Connecting…" state and the runtime reconnects immediately. No user action needed.
4. **Config change:** Alex wants to change his client IP. He modifies the Configuration tab while disconnected (or while connected, with a banner warning that the change requires a reconnect).
5. **Log inspection:** Dani sees unexpected behaviour. He sets verbosity to Full on the Configuration tab, switches to Logs, and reviews the event timeline.
6. **Version info:** Alex checks the About tab to confirm the app version when reporting an issue.
7. **macOS tunnel probe:** Dani connects from macOS. The app becomes Connected, starts periodic synthetic ICMP probe traffic automatically, and writes probe activity to logs to validate transport and server egress.

---

## 3. Functional Requirements

### 3.1 Connection Management

| ID | Requirement | Acceptance Criteria |
|----|-------------|-------------------|
| F1 | User can connect to the configured server | Tap Connect → state changes to "Connecting" → on success, "Connected" |
| F2 | User can disconnect from the server | Tap Disconnect → active platform session stops → state changes to "Disconnected" |
| F3 | App reconnects immediately after unexpected disconnect | Runtime retries connect; UI shows "Connecting…" during retry |
| F4 | Connection state is always visible | Dashboard shows current state: Disconnected, Connecting, Connected |
| F5 | Cannot connect without a valid URL and client IP | Connect button is disabled until config is valid |
| F5a | macOS V1 uses the same connection states as Android | `Connected` means transport/session connected even though no system VPN is registered |

### 3.2 Configuration

| ID | Requirement | Acceptance Criteria |
|----|-------------|-------------------|
| F6 | User can edit the server URL | Text field, validated on input |
| F7 | User can select their client IP (last octet from 10.0.10.x) | Numeric input for last octet (11–254); app displays full IP |
| F8 | Config changes are persisted locally | Changes survive app restart |
| F9 | Config can be modified while connected | Edits allowed; banner warns "Reconnect required for changes to take effect" |
| F10 | Verbosity level can be set (Low / Normal / Full) | Each level shows a description in the UI |

### 3.3 Monitoring

| ID | Requirement | Acceptance Criteria |
|----|-------------|-------------------|
| F11 | Dashboard shows current upload/download speed | Numeric values (e.g. ↑ 1.2 Mbps / ↓ 5.3 Mbps) + mini line chart |
| F12 | Dashboard shows session traffic totals | Sent bytes / Received bytes since connect |
| F13 | Dashboard shows read-only config summary | Server URL + client IP are visible but not editable |
| F13a | macOS V1 monitoring remains active | Probe traffic contributes to counters, speeds, and logs while connected |

### 3.4 Logging

| ID | Requirement | Acceptance Criteria |
|----|-------------|-------------------|
| F14 | Log screen displays connection lifecycle events | Timestamped entries visible in scrollable list |
| F15 | Log verbosity is controlled by the config setting | Low = state changes only; Normal = + errors; Full = + debug/packet events |
| F16 | Logs are cleared on new connection | Each session starts with fresh log |
| F16a | macOS V1 logs periodic probe activity | While connected, logs show ongoing synthetic probe send/result events at the selected verbosity |

### 3.5 About

| ID | Requirement | Acceptance Criteria |
|----|-------------|-------------------|
| F17 | About tab shows app name, version, build info | Visible text |
| F18 | License/legal information is accessible | Link or text display |

---

## 4. UI Component Catalogue

### 4.1 App Shell

```
+----------------------------------+
|         Status Bar               |  ← (platform, not app)
+----------------------------------+
|                                  |
|         [Content Area]           |  ← tab content rendered here
|                                  |
+----------------------------------+
|  [Dashboard] [Config] [Logs] [About] |  ← bottom tab bar
+----------------------------------+
```

**Desktop adaptation:** In V1, keep the same tab layout structure on Android and macOS. On wide screens (≥ 720px), the tab bar moves to a left sidebar.

**Data binding:** Tab selection bound to `SelectedTabIndex` in `MainViewModel`.

---

### 4.2 Dashboard (Home Tab)

```
+----------------------------------+
| 10.0.10.1:9000                  |  ← read-only server URL
| Client IP: 10.0.10.15           |  ← read-only client IP
+----------------------------------+
|                                  |
|       ╔══════════════════╗       |
|       ║     CONNECT      ║       |  ← big central button
|       ╚══════════════════╝       |     green when disconnected
|       ──── or ────               |     red "DISCONNECT" when connected
|       ╔══════════════════╗       |
|       ║   DISCONNECT     ║       |
|       ╚══════════════════╝       |
|                                  |
|        ● Disconnected            |  ← status indicator + label
|                                  |
+----------------------------------+
|  ↑  1.2 Mbps        ─╲           |
|  ↓  5.3 Mbps        ╱─╲         |  ← speed + mini chart
|                     ╱  ─╲       |
+----------------------------------+
|  Sent:    45.2 MB                |
|  Received: 128.7 MB             |  ← session traffic counters
+----------------------------------+
```

#### Interactive Elements

| Element | Behaviour | Binding |
|---------|-----------|---------|
| Connect/Disconnect button | Toggles connection. Shows "Connecting…" text + spinner while connecting. Disabled if config invalid. On macOS V1, Connected means transport/session connected. | `ConnectCommand` / `DisconnectCommand` |
| Status indicator | Coloured dot: green = Connected, amber = Connecting, grey = Disconnected | `ConnectionState` enum |
| Speed display | Updated every ~1s. Mini chart shows last 30s of throughput. | `UploadSpeed`, `DownloadSpeed`, `SpeedHistory` |
| Traffic counters | Reset on each session start. On macOS V1, counters include synthetic probe traffic. | `BytesSent`, `BytesReceived` |

#### ViewModel (`DashboardViewModel`)

```csharp
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private string _serverUrlSummary;
    [ObservableProperty] private string _clientIpSummary;
    [ObservableProperty] private ConnectionState _state;
    [ObservableProperty] private string _uploadSpeed;
    [ObservableProperty] private string _downloadSpeed;
    [ObservableProperty] private IList<SpeedSample> _speedHistory;
    [ObservableProperty] private string _bytesSent;
    [ObservableProperty] private string _bytesReceived;

    public IRelayCommand ConnectCommand { get; }
    public IRelayCommand DisconnectCommand { get; }
}
```

---

### 4.3 Configuration Tab

```
+----------------------------------+
|  Configuration                   |
+----------------------------------+
|                                  |
|  Server URL                      |
|  ┌──────────────────────────┐    |
|  │ tcp|203.0.113.10:9000/30 │    |  ← editable text field
|  └──────────────────────────┘    |
|                                  |
|  My VPN IP                       |
|  ┌──────┐                        |
|  │  15  │  → 10.0.10.15         |  ← last-octet input + preview
|  └──────┘  (11–254)              |
|                                  |
|  Log verbosity                   |
|  ○ Low    — State changes only   |  ← radio buttons with descriptions
|  ● Normal — + Errors             |
|  ○ Full   — + All events         |
|                                  |
|  ⚠ Changes will take effect      |  ← banner shown ONLY when connected
|    after reconnecting             |
|                                  |
+----------------------------------+
```

#### Interactive Elements

| Element | Behaviour | Binding |
|---------|-----------|---------|
| Server URL text field | Validates URL format on input. Shows error if invalid. Auto-saves on change. | `ServerUrl` |
| Client IP last-octet input | Numeric field, range 11–254. Shows full preview `10.0.10.xx`. Auto-saves on change. | `ClientIpOctet` |
| Verbosity radio buttons | Three options with descriptions. Auto-saves on change. | `LogVerbosity` |
| Reconnect warning banner | Visible only when `ConnectionState == Connected`. | `IsConfigChangeWarningVisible` |

#### Auto-save Behaviour

All config fields save immediately on change (no explicit save button). Storage: local file or platform preferences via `IConfigStore` abstraction.

#### ViewModel (`ConfigViewModel`)

```csharp
public partial class ConfigViewModel : ObservableObject
{
    [ObservableProperty] private string _serverUrl;
    [ObservableProperty] private int _clientIpOctet;
    [ObservableProperty] private LogVerbosity _logVerbosity;
    [ObservableProperty] private bool _isConfigChangeWarningVisible;

    // Computed
    public string FullClientIp => $"10.0.10.{ClientIpOctet}";
    public bool IsServerUrlValid => Uri.TryCreate(ServerUrl, UriKind.Absolute, out _);
    public bool IsClientIpValid => ClientIpOctet is >= 11 and <= 254;
}
```

---

### 4.4 Logs Tab

```
+----------------------------------+
|  Logs                            |
+----------------------------------+
|                                  |
│ [10:23:45] ● Connected           |
│ [10:23:44] Connected to server   |
│ [10:23:43] ● Connecting…         |
│ [10:22:10] ✕ Disconnected        |
│ [10:22:08] ● Connected           |
│ [10:22:07] Connected to server   |
│ [10:22:07] ● Connecting…         |
|                                  |
+----------------------------------+
```

- Entries scroll from bottom (newest at top is also acceptable — design choice).
- Timestamps in local time.
- Each entry has an icon/prefix per log level: `●` = info, `⚠` = warning, `✕` = error.
- Filtered by the current verbosity setting at the time of logging.

#### ViewModel (`LogsViewModel`)

```csharp
public partial class LogsViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<LogEntry> _entries;
}

public record LogEntry(DateTime Timestamp, LogLevel Level, string Message);

public enum LogLevel { Info, Warning, Error }
```

---

### 4.5 About Tab

```
+----------------------------------+
|  About PigeonPost                |
|                                  |
|  Version 1.0.0                   |
|  Build 2025.06.18.001            |
|                                  |
|  Runtime: .NET 10                |
|  Platform: Android API 26+ / macOS V1 |
|                                  |
|  © 2025 PigeonPost               |
|  [License information]           |
|                                  |
|  ---                             |
|  Built with Avalonia UI          |
+----------------------------------+
```

Simple static text. No interactive elements.

---

## 5. Navigation & State Flow

### 5.1 Tab Navigation

```
┌─────────── App Launch ───────────┐
│                                   │
│  Profile exists? ──yes──→ Dashboard tab (default)
│       │                          │
│       no                          │
│       ↓                          │
│  Configuration tab (first launch) │
└───────────────────────────────────┘

Tab bar always visible:
  [Dashboard] [Config] [Logs] [About]
```

### 5.2 Connection State Machine

```
                    ┌─────────┐
       ┌───────────→│Connected│
       │            └────┬────┘
       │                 │ user taps Disconnect
       │                 │ or server rejects
       │                 ▼
  ┌────┴────┐      ┌──────────┐
  │Discon-  │←─────│Connecting│
  │nected   │      └──────────┘
  └────┬────┘           │
       │                │ runtime retry
       │ user taps      │ (immediate)
       │ Connect        ▼
       │            ┌──────────┐
       └───────────→│Connecting│ (recursive via reconnect)
                    └──────────┘
```

| State | UI Representation | Transitions |
|-------|-------------------|-------------|
| `Disconnected` | Grey dot, button says "Connect", big green button enabled | → Connecting (user taps Connect) |
| `Connecting` | Amber dot, button says "Connecting…" with spinner, button disabled | → Connected (success) → Connecting (retry) → Disconnected (shutdown) |
| `Connected` | Green dot, button says "Disconnect" (red), config warning banner visible | → Disconnected (user taps Disconnect or error stops) |

### 5.3 Cross-tab State Coupling

| Event | Dashboard | Configuration | Logs |
|-------|-----------|--------------|------|
| Config changed | Summary updates | — | — |
| Connection established | State → Connected, counters reset | Banner shown | Cleared, new entries begin |
| Connection lost | State → Connecting | Banner hidden | "Disconnected" entry added |
| Disconnected by user | State → Disconnected | Banner hidden | "Disconnected" entry added |

macOS-specific note:

- in V1, entering `Connected` should also start the automatic synthetic probe loop

---

## 6. Architecture Notes

### 6.1 Project Structure

```
PigeonPost.sln
├── src/
│   ├── PigeonPost/                   (existing Linux console app, unchanged)
│   ├── PigeonPost.Bridge/            (existing protocol/transport, unchanged)
│   ├── PigeonPost.Tun/               (existing Linux TUN library, unchanged)
│   ├── PigeonPost.Tun.Virtual/       (existing virtual TUN, unchanged)
│   ├── PigeonPost.Vpn/               NEW — shared VPN runtime
│   │   ├── IVpnPlatform.cs           Platform VPN abstraction
│   │   ├── VpnRuntime.cs             Connection lifecycle, reconnect
│   │   ├── PontifexTransport.cs      Transport integration
│   │   ├── HandshakeProvider.cs      Client identity / handshake
│   │   ├── ConnectionState.cs        State model
│   │   ├── TrafficCounters.cs        Speed + byte counters
│   │   ├── LogCollector.cs           Structured log collector
│   │   ├── ConfigModel.cs            Profile model
│   │   ├── ProbeLoop.cs              macOS V1 synthetic ICMP probe driver
│   │   └── IConfigStore.cs           Persistence abstraction
│   │
│   └── PigeonPost.VpnClientView/     NEW — shared Avalonia UI
│       ├── App.axaml / App.axaml.cs  App entry, DI setup
│       ├── ViewModels/
│       │   ├── MainViewModel.cs      Tab selection, app-wide state
│       │   ├── DashboardViewModel.cs
│       │   ├── ConfigViewModel.cs
│       │   ├── LogsViewModel.cs
│       │   └── AboutViewModel.cs
│       ├── Views/
│       │   ├── MainWindow.axaml      (desktop) / MainView.axaml (mobile)
│       │   ├── DashboardView.axaml
│       │   ├── ConfigView.axaml
│       │   ├── LogsView.axaml
│       │   └── AboutView.axaml
│       ├── Controls/
│       │   └── SpeedChart.cs         Custom-drawn mini chart
│       ├── Styles/
│       │   ├── AppTheme.axaml        Dark theme palette
│       │   └── Controls.axaml        Reusable control styles
│       └── Converters/
│           └── ConnectionStateConverter.cs
│
└── platform/                         NEW — platform host projects
    ├── PigeonPost.VpnClientView.Android/  Android app host
    │   ├── MainActivity.cs
    │   ├── AndroidVpnService.cs      VpnService subclass
    │   └── AndroidManifest.xml
    ├── PigeonPost.VpnClientView.macOS/    macOS app host (V1 simplified mode)
    ├── PigeonPost.VpnClientView.iOS/      (future)
    └── PigeonPost.VpnClientView.iOS.Extension/ (future)
```

### 6.2 MVVM Pattern

- **Framework:** CommunityToolkit.Mvvm
- **ViewModels** use `ObservableObject` base class with `[ObservableProperty]` and `[RelayCommand]` source generators.
- **Compiled bindings** with `x:DataType` throughout.
- **Views** are pure XAML — no code-behind logic beyond constructor DI wiring.

```xml
<!-- Example compiled binding -->
<UserControl xmlns="..."
             x:Class="PigeonPost.VpnClientView.Views.DashboardView"
             x:DataType="vm:DashboardViewModel">
    <Button Content="{Binding ConnectButtonText}"
            Command="{Binding ConnectCommand}"
            IsEnabled="{Binding IsConnectEnabled}" />
</UserControl>
```

### 6.3 Dependency Injection

Using `Microsoft.Extensions.DependencyInjection` registered in `App.axaml.cs`:

```csharp
// App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    var services = new ServiceCollection();
    services.AddSingleton<IConfigStore, PlatformConfigStore>();
    services.AddSingleton<IVpnPlatform, AndroidVpnPlatform>();
    services.AddSingleton<VpnRuntime>();
    services.AddTransient<DashboardViewModel>();
    services.AddTransient<ConfigViewModel>();
    services.AddTransient<LogsViewModel>();
    services.AddTransient<MainViewModel>();

    var provider = services.BuildServiceProvider();
    // ...
}
```

### 6.4 Key Abstractions

```csharp
// PigeonPost.Vpn/IVpnPlatform.cs
public interface IVpnPlatform
{
    Task StartVpnAsync(ConfigModel config, CancellationToken ct);
    Task StopVpnAsync();
    IObservable<ConnectionState> StateChanges { get; }
    IObservable<TrafficSample> TrafficUpdates { get; }
}

// PigeonPost.Vpn/IConfigStore.cs
public interface IConfigStore
{
    ConfigModel? Load();
    void Save(ConfigModel config);
}

// PigeonPost.Vpn/ConfigModel.cs
public record ConfigModel
{
    public string ServerUrl { get; init; }
    public int ClientIpOctet { get; init; }
    public LogVerbosity Verbosity { get; init; }
}

public enum LogVerbosity { Low, Normal, Full }
```

### 6.5 Data Flow

```
User Input (View)
    ↓ Command
ViewModel (observable state)
    ↓ call method
VpnRuntime
    ↓
IVpnPlatform.StartVpnAsync()
    ↓
Pontifex handshake + tunnel established
    ↓
TrafficUpdates / StateChanges (IObservable)
    ↓
ViewModel updates observable properties
    ↓
View auto-updates via bindings
```

### 6.6 Mini Speed Chart Implementation

The speed chart is a lightweight custom control that draws a polyline of the last 30 data points:

```csharp
// SpeedChart.cs — custom drawn control
public class SpeedChart : Control
{
    public static readonly StyledProperty<IList<SpeedSample>> DataProperty =
        AvaloniaProperty.Register<SpeedChart, IList<SpeedSample>>(nameof(Data));

    public IList<SpeedSample> Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        // Draw grid lines, then polyline through data points
        // Scale Y axis to max value, X axis to fixed width
    }
}
```

Sample data is collected every 1 second from the `TrafficUpdates` observable and maintained as a fixed-size circular buffer of 30 entries in the ViewModel.

---

## 7. Styling & Theming

### 7.1 Colour Palette (Dark Theme)

| Token | Hex | Usage |
|-------|-----|-------|
| `Background` | `#0D1117` | Page background |
| `Surface` | `#161B22` | Card/section background |
| `SurfaceElevated` | `#21262D` | Input fields, tab bar background |
| `Primary` | `#58A6FF` | Accent, selected tab, links |
| `Success` | `#3FB950` | Connected indicator, positive |
| `Warning` | `#D29922` | Connecting indicator |
| `Danger` | `#F85149` | Disconnect button, errors |
| `TextPrimary` | `#C9D1D9` | Primary text |
| `TextSecondary` | `#8B949E` | Secondary/label text |
| `Border` | `#30363D` | Dividers, input borders |
| `ConnectBg` | `#238636` | Connect button background |
| `DisconnectBg` | `#DA3633` | Disconnect button background |

### 7.2 Typography

| Element | Font | Size | Weight |
|---------|------|------|--------|
| Body text | System default | 14pt | Normal |
| Speed values | System default | 24pt | Bold |
| Status label | System default | 16pt | SemiBold |
| Tab labels | System default | 12pt | Normal |
| Chart axis | System default | 10pt | Normal |

### 7.3 Theme Implementation

```xml
<!-- Styles/AppTheme.axaml -->
<ResourceDictionary xmlns="...">
    <Color x:Key="Background">#0D1117</Color>
    <Color x:Key="Surface">#161B22</Color>
    <Color x:Key="Primary">#58A6FF</Color>
    <!-- ... -->
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource Background}" />
    <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource Primary}" />
</ResourceDictionary>
```

Applied in `App.axaml`:

```xml
<Application.Styles>
    <ResourceDictionary Source="avares://PigeonPost.VpnClientView/Styles/AppTheme.axaml" />
    <!-- Avalonia's default SimpleTheme or FluentTheme can also be included -->
</Application.Styles>
```

### 7.4 Tab Bar Styling

- Bottom tab bar on mobile (standard `TabControl` with custom template).
- On desktop (width > 720px): `TabStrip` with vertical layout on the left side.
- Use `VisualStateManager` or a `LayoutTransform` to switch orientation based on `Bounds.Width`.
- Selected tab uses `Primary` colour; inactive tabs use `TextSecondary`.

---

## 8. Accessibility Implementation Notes

### 8.1 WCAG 2.1 AA Compliance

| Requirement | Implementation |
|-------------|---------------|
| Contrast ratio ≥ 4.5:1 | Dark theme palette meets this for all text-on-background combinations |
| Keyboard navigation | Tab order follows visual order. Connect button is tab-index 0 (first focusable) |
| Screen reader support | `AutomationProperties.Name` on all interactive elements |
| Touch targets ≥ 48x48 dp | Tab bar items, connect button, and input fields meet this |

### 8.2 Specific Controls

| Control | Automation |
|---------|------------|
| Connect button | `AutomationProperties.Name="Connect to VPN"` / `"Disconnect from VPN"` depending on state |
| Status indicator | `AutomationProperties.Name="Connection status: Connected"` (dynamic) |
| Speed values | `AutomationProperties.Name="Upload speed 1.2 megabits per second"` |
| Tab bar items | `AutomationProperties.Name="Dashboard tab"`, etc. |

### 8.3 Notes

- V1 is English-only; no i18n infrastructure needed yet, but strings should be extracted to a resource file (`Strings.resx`) to simplify future localisation.
- Text scaling: use relative font sizes (`FontSize="14"`) and test with system font scale changes.
- Focus indicators are visible by default in Avalonia; custom styles should not remove them.

---

## 9. Platform-Specific Notes

### 9.1 Android

| Concern | Approach |
|---------|----------|
| VPN approval | `VpnService.prepare()` → user consent dialog on first connect |
| Foreground service | On API 26+, `startForegroundService()` with notification |
| Always-on VPN | Android system setting; app must handle `VpnService` callback |
| Kill-switch | Block traffic if VPN drops: `VpnService.Builder.setBlocking(true)` |
| Wake lock | Acquire during `Connecting` / `Connected` states |
| Config storage | `SharedPreferences` via `IConfigStore` implementation |
| Revocation handling | `VpnService.onRevoke()` → trigger disconnect flow |

### 9.2 iOS (Future)

| Concern | Approach |
|---------|----------|
| Packet tunnel | `NEPacketTunnelProvider` extension (separate process) |
| Shared state | App Group `UserDefaults` for config/status |
| UI ↔ Extension IPC | `NWConnection` or `CFMessagePort` for live status |
| Entitlements | `com.apple.developer.networking.vpn.api` entitlement required |

### 9.3 macOS V1

| Concern | Approach |
|---------|----------|
| System VPN | None in V1; do not register a macOS VPN service |
| Transport/session | Real Pontifex connection and real handshake using selected client IP |
| Traffic generation | Automatic synthetic ICMP probe loop while connected |
| Probe target | `1.1.1.1` |
| Logging | Periodic probe send/result entries written while connected |
| UI state meaning | `Connected` means transport/session connected, not system VPN active |

### 9.4 Desktop (Future)

| Concern | Approach |
|---------|----------|
| Windowed mode | `MainWindow` replaces `MainView`; tab bar → left sidebar |
| Resize behaviour | Adaptive layout: min 360px width for mobile, 720px+ for sidebar mode |
| Menu bar | macOS-style menu bar with File/Edit/Help menus (future) |

---

## 10. Risks & Open Questions

### 10.1 Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Android VPN service lifecycle complexity | Connection instability | Test foreground service, always-on, and revocation paths thoroughly |
| `VpnRuntime` abstraction leaks platform details | Shared runtime becomes non-portable | Rigorous interface design; keep platform code behind `IVpnPlatform` |
| Speed chart performance on low-end Android | UI jank | Limit to 30 samples, use simple polyline rendering |
| Client IP collision | User picks an IP already taken by a Linux TUN client | Server rejects during handshake; UI shows clear "IP already in use" error |
| macOS probe mode diverges too far from Android runtime path | False confidence in V1 cross-platform behaviour | Keep handshake, identity, counters, logs, and reconnect logic shared in `PigeonPost.Vpn` |

### 10.2 Open Questions

| Question | Status |
|----------|--------|
| Exact fixed DNS resolver list for V1 | Not yet frozen |
| Whether `10.0.10.0/24` address allocation contract should be explicitly shared with end users | Not yet frozen |
| macOS: future real VPN platform after Apple capability work? | Deferred to later planning |
| Whether shared protocol logic from `PigeonPost.Bridge` should be extracted into `PigeonPost.Vpn` | Deferred |
| MTU and tunnel tuning | Deferred to V2 |

---

## Appendix A: User-Facing Verbosity Descriptions

| Level | Label | Description shown in UI |
|-------|-------|------------------------|
| `Low` | Low | Only connection state changes (Connected, Disconnected, Connecting) |
| `Normal` | Normal | Connection events plus warnings and errors |
| `Full` | Full | All events including packet-level debug information |

## Appendix B: Config Validation Rules

| Field | Rule | Error Display |
|-------|------|--------------|
| Server URL | Must match `transport\|host:port/timeout` format (e.g. `tcp\|10.0.0.1:9000/30`) | Red border + error text below field |
| Client IP octet | Integer 11–254 | Red border if out of range; invalid IP shown in grey |
| Both fields | Must be valid for Connect button to be enabled | Button disabled + greyed out |

## Appendix C: Keyboard Shortcuts (Desktop Future)

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` | Dashboard tab |
| `Ctrl+2` | Configuration tab |
| `Ctrl+3` | Logs tab |
| `Ctrl+4` | About tab |
| `Ctrl+Enter` | Connect / Disconnect |
| `Escape` | Dismiss dialogs / banners |

---

*End of document. Ready for review.*
