#!/usr/bin/env bash
#
# Register the local LAN subnet as an ingress source for the PigeonPost tunnel.
# Run after pre-deploy.sh to route LAN traffic through the tunnel.
#
# Usage: ./ingress-lan.sh <lan_interface>
#
set -euo pipefail

LAN_IF="${1:-}"
ALL_IFS=$(ip -br link show | awk '{print $1}')

if [ -z "$LAN_IF" ]; then
    echo "ERROR: no interface specified."
    has_error=1
elif ! echo "$ALL_IFS" | grep -qxF "$LAN_IF"; then
    echo "ERROR: interface '$LAN_IF' not found."
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
    echo "Choose the interface connected to your local LAN and re-run:"
    echo "  $(basename "$0") <interface>"
    exit 1
fi

LOCAL_NET=$(ip -4 addr show dev "$LAN_IF" | awk '/inet / {print $2}')
if [ -z "$LOCAL_NET" ]; then
    echo "ERROR: no IPv4 address found on $LAN_IF"
    exit 1
fi

echo "Registering $LOCAL_NET on $LAN_IF to pp-ingress"

ipset add pp-ingress "$LOCAL_NET" -exist
