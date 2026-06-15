#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PIGEON_URL="tcp|203.0.113.10:9000/30"
export PIGEON_URL

export CLIENT_ID="${CLIENT_ID:-pp-client-1}"

docker compose -f docker/docker-compose.yml build
docker compose -f docker/docker-compose.yml up -d

echo "Client deployed."
echo "  clientId: '$CLIENT_ID'"
echo "  url: '$PIGEON_URL'"
echo "  logs: docker logs pigeonpost-client -f"
