#!/usr/bin/env bash
#
# One-time host setup for the PigeonPost server machine.
# Run this once per boot (or integrate into /etc/rc.local / systemd).
#
# Usage: ./setup.sh <wan_interface>
#
# Unified subnet model: 10.0.10.0/24
#   Server TUN: 10.0.10.1/24 on tun0
#   Linux clients:    10.0.10.2-10
#   Endpoint clients: 10.0.10.11-254
#
# Provisions the TUN device, NATs the subnet to the WAN,
# and allows forwarding through the tunnel.
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
TUN_CIDR="10.0.10.1/24"
TUN_NET="10.0.10.0/24"

echo "=== PigeonPost server setup ==="

# --- TUN device ---
if ! ip link show "$TUN" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN"
    ip tuntap add dev "$TUN" mode tun
fi

if ! ip addr show dev "$TUN" | grep -qF "${TUN_CIDR%/*}"; then
    echo "Assigning $TUN_CIDR to $TUN"
    ip addr add "$TUN_CIDR" dev "$TUN"
fi

echo "Bringing $TUN up"
ip link set "$TUN" up

# --- IP forwarding ---
echo "Enabling IP forwarding"
sysctl -w net.ipv4.ip_forward=1

# --- NAT: tunnel -> internet ---
NAT_RULE="-t nat -A POSTROUTING -o $WAN_IF -s $TUN_NET -j MASQUERADE"
if ! iptables -t nat -C POSTROUTING -o "$WAN_IF" -s "$TUN_NET" -j MASQUERADE 2>/dev/null; then
    echo "Adding NAT rule: $NAT_RULE"
    iptables $NAT_RULE
else
    echo "NAT rule already exists"
fi

# --- FORWARD: allow tunnel traffic through ---
FWD_OUT="-A FORWARD -i $TUN -o $WAN_IF -s $TUN_NET -j ACCEPT"
if ! iptables -C FORWARD -i "$TUN" -o "$WAN_IF" -s "$TUN_NET" -j ACCEPT 2>/dev/null; then
    echo "Adding FORWARD rule: $FWD_OUT"
    iptables $FWD_OUT
else
    echo "FORWARD outbound rule already exists"
fi

FWD_IN="-A FORWARD -i $WAN_IF -o $TUN -m state --state RELATED,ESTABLISHED -j ACCEPT"
if ! iptables -C FORWARD -i "$WAN_IF" -o "$TUN" -m state --state RELATED,ESTABLISHED -j ACCEPT 2>/dev/null; then
    echo "Adding FORWARD rule: $FWD_IN"
    iptables $FWD_IN
else
    echo "FORWARD inbound rule already exists"
fi

echo ""
echo "Setup complete."
echo "Server TUN: $TUN_CIDR (subnet $TUN_NET)"
echo "Note: sysctl and iptables changes are not persistent across reboots."
echo "      Add sysctl to /etc/sysctl.conf and save iptables with: iptables-save > /etc/iptables/rules.v4"
