#!/usr/bin/env bash
#
# One-time host setup for the PigeonPost server machine.
# Run this once per boot (or integrate into /etc/rc.local / systemd).
#
# Usage: ./setup.sh [wan_interface]
#
set -euo pipefail

WAN_IF="${1:-eth0}"
TUN="tun0"
TUN_NET="10.0.0.0/30"

echo "=== PigeonPost server setup ==="

# --- TUN device ---
if ! ip link show "$TUN" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN"
    ip tuntap add dev "$TUN" mode tun
fi

# --- IP forwarding ---
echo "Enabling IP forwarding"
sysctl -w net.ipv4.ip_forward=1

# --- NAT: tunnel -> internet ---
RULE="-t nat -A POSTROUTING -o $WAN_IF -s $TUN_NET -j MASQUERADE"
if ! iptables -t nat -C POSTROUTING -o "$WAN_IF" -s "$TUN_NET" -j MASQUERADE 2>/dev/null; then
    echo "Adding NAT rule: $RULE"
    iptables $RULE
else
    echo "NAT rule already exists"
fi

echo ""
echo "Setup complete."
echo "Note: sysctl and iptables changes are not persistent across reboots."
echo "      Add sysctl to /etc/sysctl.conf and save iptables with: iptables-save > /etc/iptables/rules.v4"
