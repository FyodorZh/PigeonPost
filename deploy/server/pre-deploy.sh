#!/usr/bin/env bash
#
# One-time host setup for the PigeonPost server machine.
# Run this once per boot (or integrate into /etc/rc.local / systemd).
#
# Usage: ./setup.sh <wan_interface>
#
set -euo pipefail

WAN_IF="${1:-}"
ALL_IFS=$(ip -br link show | awk '{print $1}')

if [ -z "$WAN_IF" ]; then
    echo "ERROR: no interface specified."
    has_error=1
elif ! echo "$ALL_IFS" | grep -qxF "$WAN_IF"; then
    echo "ERROR: interface '$WAN_IF' not found."
    has_error=1
fi

if [ -n "${has_error:-}" ]; then
    DEFAULT_IF=$(ip -4 route show default 2>/dev/null | awk '{print $5}' || true)

    echo ""
    echo "Available interfaces:"

    while IFS= read -r iface; do
        if [ -n "$DEFAULT_IF" ] && [ "$iface" = "$DEFAULT_IF" ]; then
            echo "  $iface  ← (best guess: default route)"
        else
            echo "  $iface"
        fi
    done <<< "$ALL_IFS"

    echo ""
    echo "Choose the interface connected to your WAN (internet) and re-run:"
    echo "  $(basename "$0") <interface>"
    exit 1
fi

TUN="tun0"
TUN_NET="10.0.0.0/30"
TUN_IP="10.0.0.1"
PEER_TUN_IP="10.0.0.2"

echo "=== PigeonPost server setup ==="

# --- TUN device ---
if ! ip link show "$TUN" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN"
    ip tuntap add dev "$TUN" mode tun
fi

if ! ip addr show dev "$TUN" | grep -qF "$TUN_IP"; then
    echo "Assigning $TUN_IP/30 to $TUN"
    ip addr add "$TUN_IP/30" dev "$TUN"
fi

echo "Bringing $TUN up"
ip link set "$TUN" up

if ! ip route show dev "$TUN" | grep -qF "$PEER_TUN_IP"; then
    echo "Adding route to peer $PEER_TUN_IP via $TUN"
    ip route add "$PEER_TUN_IP/32" dev "$TUN"
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
