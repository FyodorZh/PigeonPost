# Routing Your VPN Network Traffic — Apple NetworkExtension

**Source URL:** https://developer.apple.com/documentation/networkextension/routing-your-vpn-network-traffic

## Overview

This guide covers how to configure network routing, split tunneling, and exclusion rules for VPN extensions on Apple platforms (iOS/iPadOS/macOS).

## Routing Models

### Full Tunnel (All Traffic)

All network traffic is routed through the VPN tunnel. Achieved by not specifying any exclusion rules or by adding a default route `0.0.0.0/0` and `::/0` through the tunnel interface.

### Split Tunnel (Selective Routing)

Only specified traffic goes through the VPN; everything else uses the default network interface. Split tunneling is the default when routes are explicitly configured.

### Per-App VPN (iOS/iPadOS only)

Specific apps have their traffic routed through the VPN. Configured via `NEDomainRule` or `NEAppRule` in the VPN provider.

## NEVPNProtocol Configuration

The `NEVPNProtocol` properties that control routing:

| Property | Type | Description |
|----------|------|-------------|
| `includeAllNetworks` | Bool | Route all traffic through VPN (iOS 13+/macOS 10.15+) |
| `excludeLocalNetworks` | Bool | Don't route local network traffic through VPN (default: true) |
| `serverAddress` | String | VPN server hostname or IP |
| `username` | String | Auth username |

## NEPacketTunnelNetworkSettings (for custom VPN protocols)

When building a custom VPN (NEPacketTunnelProvider), use `NEPacketTunnelNetworkSettings`:

| Property | Description |
|----------|-------------|
| `ipv4Settings` | `NEIPv4Settings` — configure IPv4 addresses and routes |
| `ipv6Settings` | `NEIPv6Settings` — configure IPv6 addresses and routes |
| `dnsSettings` | `NEDNSSettings` — configure DNS servers and search domains |
| `tunnelOverheadBytes` | Extra bytes MTU overhead for the tunnel |
| `mtu` | Maximum Transmission Unit |

### NEIPv4Settings

| Property | Description |
|----------|-------------|
| `addresses` | Array of IPv4 addresses for the TUN interface |
| `subnetMasks` | Array of subnet masks |
| `includedRoutes` | Routes to include in the tunnel (if empty, all traffic) |
| `excludedRoutes` | Routes to exclude from the tunnel |

### NEIPv6Settings

Same structure as IPv4 but for IPv6 addresses and routes.

## Exclusion Rules

### Domain-based Exclusion

Use `NEDomainRule` to exclude/include domains from the VPN tunnel (iOS Per-App VPN):

| Rule | Description |
|------|-------------|
| `matchDomains` | Domains that trigger/use the VPN |
| `probeURL` | URL used to probe connectivity before enabling VPN |
| `matchDomainsNoSearch` | Don't append these domains to search list |

### Route-based Exclusion

Configure `excludedRoutes` in NE/IPv4Settings to bypass specific subnets:

```swift
let excluded = NEIPv4Route(destinationAddress: "10.0.0.0", subnetMask: "255.0.0.0")
settings.ipv4Settings?.excludedRoutes = [excluded]
```

## Packet Tunnel Provider Flow

1. Provider calls `setTunnelNetworkSettings()` with the configured `NEPacketTunnelNetworkSettings`
2. System applies the routes, addresses, and DNS
3. Provider receives packets matching included routes via `packetFlow.readPackets()`
4. Provider injects packets from the remote side via `packetFlow.writePackets()`

## Important Notes

- On iOS, Per-App VPN and split tunneling are mutually exclusive (cannot use both simultaneously)
- macOS supports both split tunneling and per-app VPN simultaneously
- `includeAllNetworks` defaults to `false` (split tunnel mode)
- `excludeLocalNetworks` defaults to `true` (local network access works normally)
- DNS settings are applied only when the VPN is connected
- Route changes take effect when tunnel network settings are applied
- Custom VPN protocols (NEPacketTunnelProvider) have full control over routing
