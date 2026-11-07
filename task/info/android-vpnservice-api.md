# Android VpnService API Reference

**Source URL:** https://developer.android.google.cn/reference/kotlin/android/net/VpnService

## Overview

`android.net.VpnService` is the base class for implementing VPN solutions on Android. It extends `android.app.Service` and provides the framework for building VPN tunnels at the application level.

## Key Concepts

- **VpnService** prepares a virtual network interface and routes traffic through it
- Runs as a standard Android `Service` — lifecycle managed by the system
- Requires `BIND_VPN_SERVICE` permission (system grants this automatically to the app that creates the service)
- User must explicitly consent via an intent dialog before the VPN starts

## Lifecycle

1. **`onCreate()`** — service is created
2. **`onStartCommand(Intent, flags, startId)`** — called when VPN is started/stopped via intent. Returns `START_STICKY` to keep the service alive
3. **`onDestroy()`** — service is torn down; VPN is automatically revoked
4. **`onRevoke()`** — called when the VPN is revoked by the system or user (e.g., user disconnects from system VPN settings)

## Core Methods

| Method | Description |
|--------|-------------|
| `establish()` | Creates the VPN interface and returns a `ParcelFileDescriptor` for reading/writing raw IP packets |
| `protect(int socket)` | Protects a socket from being routed through the VPN (avoids loopback) |
| `prepare(Context)` | Static method that returns a `PendingIntent` for user consent |
| `onRevoke()` | Callback when VPN is revoked |

## Builder (VpnService.Builder)

The inner `Builder` class is used to configure the VPN interface before calling `establish()`:

- `addAddress(InetAddress, prefixLength)` — assign an IP address to the VPN interface
- `addRoute(InetAddress, prefixLength)` — add a route for traffic to be sent through the VPN
- `addDnsServer(InetAddress)` — add a DNS server
- `addSearchDomain(String)` — add a search domain
- `setMtu(int)` — set the MTU
- `setSession(String)` — set the session name (shown in system VPN status)
- `setConfigureIntent(PendingIntent)` — set the configuration intent
- `establish()` — finalize and create the VPN interface

## Permissions

```xml
<uses-permission android:name="android.permission.BIND_VPN_SERVICE" />
```

The service must be declared in `AndroidManifest.xml`:

```xml
<service android:name=".MyVpnService"
         android:permission="android.permission.BIND_VPN_SERVICE">
    <intent-filter>
        <action android:name="android.net.VpnService" />
    </intent-filter>
</service>
```

## User Consent Flow

```kotlin
val intent = VpnService.prepare(context)
if (intent != null) {
    startActivityForResult(intent, VPN_REQUEST_CODE)
} else {
    // Already prepared, start VPN directly
    startVpn()
}
```

## Architecture Notes

- The VPN interface operates in **TUN mode** (layer 3) — raw IP packets only, no Ethernet headers
- Read/write happens via the `ParcelFileDescriptor` returned by `establish()`
- Packets must be written as complete IP datagrams (max MTU size)
- The service must protect the tunnel socket before connecting to avoid routing loops
- Android's VPN is per-user, not per-app (unless using per-app VPN rules)
- Always-on VPN and per-app VPN configurations are supported from Android 4.2+
