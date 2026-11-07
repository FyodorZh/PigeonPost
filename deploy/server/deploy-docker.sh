#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

# Unified subnet model: server TUN 10.0.10.1/24, clients from 10.0.10.0/24
# Linux clients: 10.0.10.2-10, endpoint clients: 10.0.10.11-254
PIGEON_URL="tcp|0.0.0.0:9000/30"
export PIGEON_URL

docker compose -f docker/docker-compose.yml build
docker compose -f docker/docker-compose.yml up -d

echo "Server deployed."
echo "  url: '$PIGEON_URL'"
echo "  logs: docker logs pigeonpost-server -f"
