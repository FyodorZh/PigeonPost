#!/usr/bin/env bash
set -euo pipefail

TUN_A="${1:-tunA}"
TUN_B="${2:-tunB}"

for tun in "$TUN_A" "$TUN_B"; do
    if ! ip link show "$tun" >/dev/null 2>&1; then
        echo "Creating TUN device: $tun"
        ip tuntap add dev "$tun" mode tun
    fi
done

echo "Assigning 10.0.0.1/30 to $TUN_A"
ip addr add 10.0.0.1/30 dev "$TUN_A"
echo "Bringing $TUN_A up"
ip link set "$TUN_A" up
ip route add 10.0.0.2/32 dev "$TUN_A" 2>/dev/null || true

echo "Assigning 10.0.0.2/30 to $TUN_B"
ip addr add 10.0.0.2/30 dev "$TUN_B"
echo "Bringing $TUN_B up"
ip link set "$TUN_B" up
ip route add 10.0.0.1/32 dev "$TUN_B" 2>/dev/null || true
