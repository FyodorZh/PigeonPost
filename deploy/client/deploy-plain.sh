#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

PIGEON_URL="tcp|203.0.113.10:9000/30"
export PIGEON_URL

TUN_NAME="${TUN_NAME:-tun0}"
TUN_CIDR="${TUN_CIDR:-10.0.10.11/24}"
PEER_IP="${PEER_IP:-10.0.10.1}"

dotnet publish src/PigeonPost/PigeonPost.csproj -c Release -o /app

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

if ! ip route get "$PEER_IP" 2>/dev/null | grep -qF "dev $TUN_NAME"; then
    echo "Adding route to peer $PEER_IP via $TUN_NAME"
    ip route add "$PEER_IP/32" dev "$TUN_NAME"
fi

exec dotnet /app/PigeonPost.dll --role client --tun "$TUN_NAME" --url "$PIGEON_URL"
