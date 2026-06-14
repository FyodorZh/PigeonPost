#!/usr/bin/env bash
#
# Register the local LAN subnet as an ingress source for the PigeonPost tunnel.
# Run after pre-deploy.sh to route LAN traffic through the tunnel.
#
# Usage: ./ingress-lan.sh [lan_interface]
#
set -euo pipefail

LAN_IF="${1:-eth0}"

LOCAL_NET=$(ip -4 addr show dev "$LAN_IF" | awk '/inet / {print $2}')
if [ -z "$LOCAL_NET" ]; then
    echo "ERROR: cannot detect subnet on $LAN_IF"
    exit 1
fi

echo "Registering $LOCAL_NET on $LAN_IF to pp-ingress"

ipset add pp-ingress "$LOCAL_NET" -exist
