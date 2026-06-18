# VpnService.Builder — Complete Method Reference

**Source URL:** https://developer.android.com/reference/android/net/VpnService.Builder

## Overview

`VpnService.Builder` is a helper class to create a VPN interface. It must always be used within the scope of the outer `VpnService`. The builder configures the virtual network interface before `establish()` finalizes it.

**Namespace:** Android.Net (Android.Net.VpnService.Builder in .NET binding)  
**Extends:** Java.Lang.Object / java.lang.Object

## Methods

### Address Configuration

| Method | Description |
|--------|-------------|
| `addAddress(InetAddress address, int prefixLength)` | Add an IP address to the VPN interface |
| `addAddress(String address, int prefixLength)` | Convenience: add address from string (e.g. `"10.0.0.2"`, `24`) |

Throws `IllegalArgumentException` if the address is invalid.

### Route Configuration

| Method | Description |
|--------|-------------|
| `addRoute(InetAddress address, int prefixLength)` | Add a network route through the VPN |
| `addRoute(String address, int prefixLength)` | Convenience: add route from string |
| `addRoute(IpPrefix prefix)` | Add route using IpPrefix (API 33+) |

The VPN will only route traffic matching these routes. Missing routes = split tunnel (traffic bypasses VPN). Add `0.0.0.0/0` for full tunnel.

Throws `IllegalArgumentException` if the route is invalid.

### DNS Configuration

| Method | Description |
|--------|-------------|
| `addDnsServer(InetAddress address)` | Add a DNS server address |
| `addDnsServer(String address)` | Convenience: add DNS server from string |
| `addSearchDomain(String domain)` | Add a DNS search domain |

Throws `IllegalArgumentException` if the address is invalid.

### MTU

| Method | Description |
|--------|-------------|
| `setMtu(int mtu)` | Set the MTU of the VPN interface |

Throws `IllegalArgumentException` if MTU is not positive.

### Session & Configuration

| Method | Description |
|--------|-------------|
| `setSession(String session)` | Set session name (shown in system VPN status notification) |
| `setConfigureIntent(PendingIntent intent)` | Set the configuration intent (user tap on VPN notification) |
| `setMetered(boolean isMetered)` | Mark VPN as metered (API 33+) |
| `setBlocking(boolean blocking)` | Enable/disable blocking mode (API 33+) — when true, no traffic passes until VPN is established |

### Per-App Rules

| Method | Description |
|--------|-------------|
| `addAllowedApplication(String packageName)` | Only allow this app to use the VPN |
| `addDisallowedApplication(String packageName)` | Exclude this app from using the VPN |
| `allowFamily(int family)` | Allow a specific address family (AF_INET or AF_INET6) |
| `allowBypass()` | Allow apps to bypass the VPN when they use `VpnService.protect()` |

Cannot mix `addAllowedApplication` and `addDisallowedApplication` in the same builder — choose one approach.

### Finalize

| Method | Description |
|--------|-------------|
| `establish()` | Creates the VPN interface and returns a `ParcelFileDescriptor` |

Returns `null` if the VPN is already established or if the user revoked the VPN.

## Builder Pattern Example

```java
VpnService.Builder builder = new VpnService.Builder();
builder.setMtu(1500);
builder.addAddress("10.0.0.2", 24);
builder.addRoute("0.0.0.0", 0);
builder.addDnsServer("8.8.8.8");
builder.addSearchDomain("example.com");
builder.setSession("MyVPN");
ParcelFileDescriptor vpnInterface = builder.establish();
```

## Important Notes

- All builder methods return the `Builder` instance (fluent interface)
- `establish()` can only be called once per builder instance
- The returned `ParcelFileDescriptor` must be kept alive; closing it tears down the VPN
- Protect the tunnel socket BEFORE calling `establish()` to avoid routing loops
- Builder configuration is only valid for a single `establish()` call
- On Android 12+ the system may display a warning if no DNS servers are configured
