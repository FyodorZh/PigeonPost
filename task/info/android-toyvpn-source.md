# ToyVpnService — Google's Official ToyVpn Source Code

**Source URL:** https://android.googlesource.com/platform/development/+/1345214bfcb1110ddb42748af4292083d6bf0c46/samples/ToyVpn/src/com/example/android/toyvpn/ToyVpnService.java

## Overview

ToyVpn is Google's official minimal VpnService implementation demonstrating the complete lifecycle of an Android VPN service. It connects to a remote server via UDP DatagramChannel and forwards packets bidirectionally.

## Architecture

### Class Structure

```
ToyVpnService extends VpnService implements Handler.Callback, Runnable
```

- **Handler**: Used to display Toast messages to the UI
- **Runnable**: The VPN thread that handles packet forwarding

### Key Components

| Component | Purpose |
|-----------|---------|
| `mServerAddress` | VPN server IP address |
| `mServerPort` | VPN server UDP port |
| `mSharedSecret` | Pre-shared secret for simple auth |
| `mConfigureIntent` | PendingIntent for VPN configuration |
| `mHandler` | UI message handler |
| `mThread` | Background VPN thread |
| `mInterface` | ParcelFileDescriptor for the TUN interface |
| `mParameters` | Cached interface parameters string |

## Lifecycle

### onStartCommand()

Called when the service starts:
1. Creates the Handler for UI messages
2. Extracts server address, port, and shared secret from the Intent
3. Interrupts any existing thread
4. Starts a new `Thread` named "ToyVpnThread"
5. Returns `START_STICKY`

### onDestroy()

Interrupts the VPN thread when the service is destroyed.

### run() — Main Loop

The main thread tries to connect to the server in a loop (up to 10 attempts):

1. Resolves the server `InetSocketAddress`
2. Calls `run(server)` which creates the tunnel and handles packet forwarding
3. On disconnect, sleeps 3 seconds before reconnecting
4. After 10 failed attempts, gives up

### run(InetSocketAddress) — Tunnel Logic

1. Opens a UDP `DatagramChannel` for the tunnel
2. **Protects the socket** using `protect(tunnel.socket())` — critical to avoid loopback
3. Connects to the server
4. Sets the channel to non-blocking mode
5. Calls `handshake(tunnel)` for authentication
6. Creates input/output streams from `mInterface` (the TUN fd)
7. Allocates a 32767-byte packet buffer
8. Enters the main forwarding loop

### Forwarding Loop

Uses a `timer` variable (positive = sending, negative = receiving):

```
while (true):
    // Read from TUN (outgoing packets)
    length = in.read(packet.array())
    if (length > 0):
        packet.limit(length)
        tunnel.write(packet)
        packet.clear()
        timer = 1  // switch to sending

    // Read from tunnel (incoming packets)
    length = tunnel.read(packet)
    if (length > 0):
        if (packet.get(0) != 0):  // skip control messages
            out.write(packet.array(), 0, length)
        packet.clear()
        timer = 0  // switch to receiving

    // Idle handling
    if (idle):
        sleep(100ms)
        timer += (timer > 0) ? 100 : -100
        // Keepalive: send empty control packet after 15s receiving
        if (timer < -15000):
            send keepalive (byte 0)
            timer = 1
        // Timeout detection
        if (timer > 20000):
            throw timeout
```

### handshake(DatagramChannel) — Simple Authentication

1. Allocates a 1024-byte buffer
2. Prepends `0x00` (control message marker) + shared secret
3. Sends the secret 3 times (in case of packet loss)
4. Waits for server response (up to 50 * 100ms = 5 seconds)
5. Response starting with `0x00` contains configuration parameters
6. Calls `configure(params)` to set up the TUN interface

### configure(String parameters) — Interface Setup

Parses a space-delimited parameter string where each parameter starts with a single letter:

| Prefix | Method | Example |
|--------|--------|---------|
| `m` | `setMtu(Short)` | `m,1500` |
| `a` | `addAddress(addr, prefixLen)` | `a,10.0.0.2,24` |
| `r` | `addRoute(addr, prefixLen)` | `r,0.0.0.0,0` |
| `d` | `addDnsServer(addr)` | `d,8.8.8.8` |
| `s` | `addSearchDomain(domain)` | `s,example.com` |

**Key optimizations:**
- If new parameters match existing `mInterface` parameters, the old interface is reused
- Otherwise the old interface is closed and a new one is created via `builder.setSession().setConfigureIntent().establish()`

## Important Design Patterns

1. **Socket protection** must happen before connection
2. **Non-blocking I/O** with sleep-based polling avoids busy-waiting
3. **Keepalive mechanism** sends control messages every ~15s of inactivity
4. **Timeout** triggers reconnection after ~20s without any received packets
5. **Control messages** (byte 0 prefix) are used for auth and keepalive
6. The same thread handles both read and write directions

## See Also

- ToyVpn project overview: https://android.googlesource.com/platform/development/+/master/samples/ToyVpn/
- VpnService API: https://developer.android.google.cn/reference/kotlin/android/net/VpnService
