#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

# Unified subnet model: server TUN 10.0.10.1/24, clients from 10.0.10.0/24
# Linux clients: 10.0.10.2-10, endpoint clients: 10.0.10.11-254
PIGEON_URL="tcp|0.0.0.0:9000/30"
export PIGEON_URL

TUN_NAME="${TUN_NAME:-tun0}"
TUN_CIDR="${TUN_CIDR:-10.0.10.1/24}"

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

exec dotnet /app/PigeonPost.dll --role server --tun "$TUN_NAME" --url "$PIGEON_URL"
