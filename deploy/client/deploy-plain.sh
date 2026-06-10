#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

PIGEON_URL="tcp|203.0.113.10:9000/30"
export PIGEON_URL

TUN_NAME="tun0"
TUN_IP="10.0.0.2"
PEER_IP="10.0.0.1"

dotnet publish src/PigeonPost/PigeonPost.csproj -c Release -o /app

if ! ip link show "$TUN_NAME" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN_NAME"
    ip tuntap add dev "$TUN_NAME" mode tun
fi

if ! ip addr show dev "$TUN_NAME" | grep -qF "$TUN_IP"; then
    echo "Assigning $TUN_IP/30 to $TUN_NAME"
    ip addr add "$TUN_IP/30" dev "$TUN_NAME"
fi

echo "Bringing $TUN_NAME up"
ip link set "$TUN_NAME" up

if ! ip route show dev "$TUN_NAME" | grep -qF "$PEER_IP"; then
    echo "Adding route to peer $PEER_IP via $TUN_NAME"
    ip route add "$PEER_IP/32" dev "$TUN_NAME"
fi

exec dotnet /app/PigeonPost.dll --role client --tun "$TUN_NAME" --url "$PIGEON_URL"
