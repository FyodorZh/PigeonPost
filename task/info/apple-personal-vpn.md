# Personal VPN — Apple NetworkExtension Architecture

**Source URL:** https://developer.apple.com/documentation/networkextension/personal-vpn

## Overview

Personal VPN is Apple's built-in VPN architecture for iOS and macOS that allows apps to configure and manage VPN connections using the NetworkExtension framework. It supports industry-standard protocols (IKEv2, IPsec) managed through `NEVPNManager`.

## Architecture

```
App → NEVPNManager → System VPN Daemon → kernel (utun interface)
```

- **NEVPNManager**: Public API that apps use to configure VPN settings
- **System VPN Daemon**: System process that handles the actual VPN connection
- **utun interface**: Kernel virtual network interface for tunnel traffic

## Supported Protocols

### IKEv2 (Internet Key Exchange v2)
- **Class**: `NEVPNProtocolIKEv2`
- Modern, secure protocol (RFC 7296)
- Supports multiple authentication methods:
  - Shared secret (PSK)
  - Certificate-based (X.509)
  - EAP (Extensible Authentication Protocol)
  - Password (username + password)
- MOBIKE support (seamless network transition)
- NAT traversal built-in
- Perfect Forward Secrecy (PFS)

### IPsec (IKEv1)
- **Class**: `NEVPNProtocolIPSec`
- Older protocol (RFC 2409)
- Authentication methods:
  - Shared secret
  - Certificate-based
  - Password (XAUTH)
- Limited compared to IKEv2

## Configuration Model

The VPN configuration is stored in the system's Network Extension preferences:

1. **Load** existing config via `loadFromPreferences`
2. **Modify** protocol, routing, on-demand rules
3. **Save** to preferences (user must approve)
4. **Connect** to start the VPN

## On-Demand VPN

Rules that automatically start/stop the VPN based on conditions:

| `NEOnDemandRule` Subclass | Trigger |
|---------------------------|---------|
| `NEOnDemandRuleConnect` | Always connect when rules match |
| `NEOnDemandRuleDisconnect` | Always disconnect when rules match |
| `NEOnDemandRuleIgnore` | Do nothing when rules match |

Conditions evaluated per rule:
- **SSID matching** (Wi-Fi network name)
- **DNS domain matching**
- **Interface type matching** (Wi-Fi, Cellular, Ethernet)

## Entitlements Required

```xml
<!-- Xcode Capabilities > Network Extensions > Personal VPN -->
com.apple.developer.networking.vpn.api
```

Without this entitlement, the app cannot access `NEVPNManager`.

## User Experience

1. App requests VPN configuration via `NEVPNManager`
2. System shows a consent dialog (first time only per configuration)
3. User approves → VPN configuration is added to Settings
4. User can manage the VPN from Settings → General → VPN (iOS) or System Settings → Network → VPN (macOS)
5. On-demand rules can automatically connect/disconnect without user interaction
6. VPN status is visible in the status bar (iOS) or menu bar (macOS)

## Limitations

- Maximum one Personal VPN configuration per device (though MDM can add more)
- User can manually override or delete the configuration
- Per-App VPN on iOS is mutually exclusive with split tunneling
- Requires a paid Apple Developer account for distribution
- No support for custom protocol definitions (only IKEv2/IPsec)
- For custom protocols, use `NEPacketTunnelProvider` instead
