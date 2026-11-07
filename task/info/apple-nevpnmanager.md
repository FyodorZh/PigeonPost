# NEVPNManager — Apple Personal VPN Management

**Source URL:** https://developer.apple.com/documentation/networkextension/nevpnmanager

## Overview

`NEVPNManager` is the primary class in Apple's NetworkExtension framework for managing VPN configurations and connections on iOS and macOS. It provides a unified interface for configuring, loading, saving, and controlling VPN tunnels.

**Framework:** NetworkExtension  
**Platforms:** iOS, macOS, tvOS  
**Availability:** iOS 8.0+, macOS 10.11+

## Key Properties

| Property | Description |
|----------|-------------|
| `connection` | The current VPN connection (`NEVPNConnection`). Status monitoring via KVO. |
| `localizedDescription` | User-visible name for the VPN configuration |
| `protocol` | The VPN protocol configuration (`NEVPNProtocol` — IPsec or IKEv2) |
| `onDemandEnabled` | Enable connect-on-demand rules |
| `onDemandRules` | Array of `NEOnDemandRule` objects controlling automatic connection |
| `enabled` | Whether this configuration is enabled |

## Key Methods

| Method | Description |
|--------|-------------|
| `loadFromPreferences(completionHandler:)` | Load saved VPN configuration from system preferences |
| `saveToPreferences(completionHandler:)` | Save configuration to system preferences (requires user approval) |
| `removeFromPreferences(completionHandler:)` | Remove the VPN configuration |

## Shared Manager

`NEVPNManager.shared()` returns the singleton manager instance. All VPN operations use this shared instance.

## Connection Lifecycle

1. **Load**: Call `loadFromPreferences` to read existing configuration
2. **Configure**: Set protocol, on-demand rules, description
3. **Save**: Call `saveToPreferences` (triggers user auth dialog)
4. **Connect**: Start the VPN via `connection.startVPNTunnel()`
5. **Monitor**: Observe `connection.status` via KVO
6. **Disconnect**: Call `connection.stopVPNTunnel()`

## Connection Status (NEVPNStatus)

| Status | Description |
|--------|-------------|
| `invalid` | Configuration is invalid or disabled |
| `disconnected` | VPN is disconnected |
| `connecting` | VPN is connecting |
| `connected` | VPN is connected |
| `reasserting` | VPN is reconnecting |
| `disconnecting` | VPN is disconnecting |

## Protocol Types

- **`NEVPNProtocolIPSec`**: Legacy IPsec (IKEv1) with shared secret or certificate auth
- **`NEVPNProtocolIKEv2`**: Modern IKEv2 with EAP, certificate, or shared secret auth

Both inherit from `NEVPNProtocol` which provides common properties:
- `serverAddress` — VPN server hostname/IP
- `username` — authentication username
- `passwordReference` — keychain reference for the password
- `identityReference` — keychain reference for the certificate
- `disconnectOnSleep` — auto-disconnect on system sleep

## Usage Example (Swift)

```swift
let manager = NEVPNManager.shared()
manager.loadFromPreferences { error in
    let protocolConfig = NEVPNProtocolIKEv2()
    protocolConfig.serverAddress = "vpn.example.com"
    protocolConfig.remoteIdentifier = "vpn.example.com"
    protocolConfig.authenticationMethod = .none
    
    manager.protocol = protocolConfig
    manager.localizedDescription = "My VPN"
    manager.isEnabled = true
    
    manager.saveToPreferences { error in
        try? manager.connection.startVPNTunnel()
    }
}
```

## Important Notes

- **User consent required**: Both save and connect operations may prompt the user
- **Keychain storage**: Passwords and certificates must be stored in the keychain
- **Entitlement required**: The app must have the `com.apple.developer.networking.vpn.api` entitlement
- **Provisioning profile**: VPN configurations are tied to the provisioning profile
- **On-demand rules**: Can trigger VPN based on SSID, domain, or other criteria
- **KVO monitoring**: Observe `status` and `connection` properties for state changes
