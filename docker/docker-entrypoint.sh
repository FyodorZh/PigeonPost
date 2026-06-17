#!/usr/bin/env bash
set -euo pipefail

if ! ip link show "$TUN_NAME" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN_NAME"
    ip tuntap add dev "$TUN_NAME" mode tun
fi

if ! ip addr show dev "$TUN_NAME" | grep -qF "${TUN_CIDR%/*}"; then
    echo "Assigning $TUN_CIDR to $TUN_NAME"
    ip addr add "$TUN_CIDR" dev "$TUN_NAME"
fi

echo "Bringing $TUN_NAME up"
ip link set "$TUN_NAME" up

if [ -n "${PEER_IP:-}" ]; then
    if ! ip route get "$PEER_IP" 2>/dev/null | grep -qF "dev $TUN_NAME"; then
        echo "Adding route to peer $PEER_IP via $TUN_NAME"
        ip route add "$PEER_IP/32" dev "$TUN_NAME"
    fi
fi

exec dotnet /app/PigeonPost.dll "$@"
