#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PIGEON_URL="tcp|203.0.113.10:9000/30"
export PIGEON_URL

TUN_CIDR="${TUN_CIDR:-10.0.10.11/24}"
PEER_IP="${PEER_IP:-10.0.10.1}"
TUN_IP="${TUN_CIDR%/*}"
export TUN_CIDR PEER_IP

CLIENT_ID="${CLIENT_ID:-pp-client-1}"

docker compose -f docker/docker-compose.yml build
docker compose -f docker/docker-compose.yml up -d

echo "Client deployed."
echo "  tunIp:   '$TUN_IP'"
echo "  cidr:    '$TUN_CIDR'"
echo "  peer:    '$PEER_IP'"
echo "  url:     '$PIGEON_URL'"
echo "  label:   '$CLIENT_ID' (cosmetic only)"
echo "  logs:    docker logs pigeonpost-client -f"
