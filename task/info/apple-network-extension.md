# Network Extension Framework — Apple

**Source URL:** https://developer.apple.com/documentation/networkextension

## Overview

NetworkExtension is Apple's framework for customizing and extending the network capabilities of iOS and macOS. It provides APIs for building VPN clients, network proxies, content filters, and DNS proxies that integrate deeply with the operating system.

**Platforms:** iOS, macOS, tvOS (subset)  
**Framework:** `NetworkExtension.framework`

## Provider Types

### 1. Personal VPN

Managed via `NEVPNManager`. Supports IKEv2 and IPsec protocols. Configuration and control are handled by the system daemon.

### 2. Packet Tunnel Provider (`NEPacketTunnelProvider`)

Custom VPN protocol implementation. App-controlled virtual network interface:
- Full control over protocol (TCP, UDP, custom)
- Virtual TUN interface via `packetFlow`
- Configure IP addresses, routes, DNS via `NEPacketTunnelNetworkSettings`
- Extends `NEProvider` with tunnel lifecycle callbacks

### 3. App Proxy Provider (`NEAppProxyProvider`)

Transparent proxy for per-app TCP/UDP traffic:
- Intercept traffic from specific apps
- Redirect through custom proxy server or tunnel
- Extends `NETunnelProvider` (which extends `NEProvider`)

### 4. Filter Data Provider (`NEFilterDataProvider`)

Content filtering at the network level:
- Inspect and modify network data
- Block or allow connections based on content
- Used for parental controls, security filters

### 5. Filter Control Provider (`NEFilterControlProvider`)

Manages filter rules and settings remotely:
- Push rule updates to the filter data provider
- Handle user consent for filter actions

### 6. DNS Proxy Provider (`NEDNSProxyProvider`)

Custom DNS resolution:
- Intercept and redirect DNS queries
- Implement custom DNS protocols (DNS-over-HTTPS, DNS-over-TLS)
- Configure DNS settings via `NEDNSSettings`

### 7. Transparent Proxy Provider (`NETransparentProxyProvider`)

System-wide transparent proxy (macOS only):
- No per-app configuration needed
- All traffic passes through the proxy

## Common Classes

| Class | Purpose |
|-------|---------|
| `NEProvider` | Base class for all providers |
| `NEPacketTunnelProvider` | Custom VPN tunnel |
| `NETunnelProvider` | Base for tunnel-based providers |
| `NEAppProxyProvider` | Per-app proxy |
| `NEFilterDataProvider` | Content filter |
| `NEDNSProxyProvider` | Custom DNS |
| `NEVPNManager` | Personal VPN management |
| `NEVPNConnection` | VPN connection monitoring |
| `NEVPNProtocol` | Protocol configuration base |
| `NEPacketTunnelNetworkSettings` | Tunnel IP/route/DNS settings |
| `NEDNSSettings` | DNS configuration |
| `NEProxySettings` | Proxy configuration |

## Provider Lifecycle

```
Provider registered (system launches extension)
    └─ startTunnelWithOptions (setup)
        ├─ setTunnelNetworkSettings (configure interface)
        └─ Packet flow starts
            ├─ handle packets bi-directionally
            └─ sleep/wake notifications
        └─ stopTunnelWithReason (teardown)
    └─ Provider terminated
```

## System Integration

- **Entitlements**: Each provider type requires specific entitlements
- **App Extensions**: Providers run as separate processes (extensions) in their own sandbox
- **Background execution**: System keeps provider running as needed
- **Memory limits**: Providers have memory caps (~50MB for network extensions)
- **Signing**: All extensions require proper code signing and provisioning profiles

## Key APIs

### NEPacketTunnelProvider

```swift
class NEPacketTunnelProvider: NEProvider {
    func startTunnel(options: [String: NSObject]?) async throws
    func stopTunnel(with reason: NEProviderStopReason)
    func setTunnelNetworkSettings(_:) async throws
    var packetFlow: NEPacketTunnelFlow { get }
}
```

### NEPacketTunnelFlow

```swift
class NEPacketTunnelFlow {
    func readPackets() -> [(Data, [NSNumber])]  // [Data, protocolFamily]
    func writePackets(_ packets: [Data], withProtocols: [NSNumber])
}
```

## Security & Permissions

- All provider extensions run in a sandbox
- User must approve VPN/proxy configuration changes
- Filter providers require user consent for content filtering
- System enforces memory and CPU usage limits
- Network extensions can be disabled by MDM policies
- On macOS, VPN configurations can be managed per-interface
