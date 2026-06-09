#!/usr/bin/env bash
set -euo pipefail

if ! ip link show "$TUN_NAME" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN_NAME"
    ip tuntap add dev "$TUN_NAME" mode tun
fi

echo "Assigning $TUN_IP/30 to $TUN_NAME"
ip addr add "$TUN_IP/30" dev "$TUN_NAME"
echo "Bringing $TUN_NAME up"
ip link set "$TUN_NAME" up
echo "Adding route to peer $PEER_IP via $TUN_NAME"
ip route add "$PEER_IP/32" dev "$TUN_NAME" 2>/dev/null || true

exec dotnet /app/PigeonPost.dll "$@"
