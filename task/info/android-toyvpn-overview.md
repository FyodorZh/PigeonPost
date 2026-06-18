# ToyVpn Sample — Project Overview

**Source URL:** https://android.googlesource.com/platform/development/+/master/samples/ToyVpn/

## Overview

ToyVpn is the official sample VPN application provided by the Android Open Source Project (AOSP). It demonstrates a minimal but complete implementation of `android.net.VpnService` for building a VPN tunnel.

## Project Structure

```
ToyVpn/
├── Android.bp             # Build configuration
├── AndroidManifest.xml    # Service declaration + permissions
├── _index.html            # Project index page
├── res/                   # Resources (layouts, strings, etc.)
│   └── ...
├── server/                # Simple server implementation
│   ├── ToyVpnServer.java  # UDP server that bridges to a TUN device
│   └── ...
└── src/
    └── com/example/android/toyvpn/
        ├── ToyVpnService.java     # Main VpnService implementation
        ├── ToyVpnClient.java      # Client activity / launcher
        └── ...
```

## How It Works

### Client Side (Android App)

1. User launches the ToyVpn app and enters server address, port, and shared secret
2. The app calls `VpnService.prepare()` to get user consent
3. After consent, it starts `ToyVpnService` with the connection parameters
4. ToyVpnService creates a **UDP DatagramChannel** to the remote server
5. Protects the socket from routing through the VPN (avoids loopback)
6. Performs a simple handshake — sends the shared secret, receives TUN config
7. Enters the forwarding loop: reads packets from TUN → sends to server, receives from server → writes to TUN
8. Reconnects automatically on disconnect (up to 10 attempts)

### Server Side (Java Application)

The server (`ToyVpnServer.java`) runs on the remote machine:
1. Opens a UDP socket and a local TUN device
2. Authenticates clients using the shared secret
3. Sends TUN configuration parameters (IP, routes, DNS)
4. Forwards packets between the UDP socket and the TUN device
5. Handles keepalive and timeout

## Key Features Demonstrated

- **VpnService lifecycle**: `onStartCommand`, `onDestroy`, `onRevoke`
- **Socket protection**: `VpnService.protect()` to avoid routing loops
- **TUN interface configuration**: Address, routes, MTU, DNS
- **Non-blocking I/O**: Single thread for bidirectional forwarding
- **Control messages**: Protocol for authentication and keepalive
- **Auto-reconnection**: Retry logic with exponential backoff
- **Keepalive**: Empty control messages to detect connection drops

## Running the Sample

1. Import into Android Studio from the `samples/ToyVpn` directory
2. Deploy the server to a Linux machine (requires TUN support + root)
3. Enter the server's IP/port and shared secret on the Android app
4. Accept the VPN consent dialog
5. Traffic is now routed through the tunnel

## Limitations (by Design)

- Uses **UDP** (unreliable) — packets may be lost
- Simple **plaintext authentication** — not production-ready
- Single-threaded — one direction may block the other
- No encryption — purely a demonstration of the VpnService API
- The server requires root for TUN device access
