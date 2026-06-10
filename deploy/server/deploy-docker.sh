#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PIGEON_URL="tcp|0.0.0.0:9000/30"
export PIGEON_URL

docker compose -f docker/docker-compose.yml build
docker compose -f docker/docker-compose.yml up -d

echo "Server deployed."
echo "  url: $PIGEON_URL"
echo "  logs: docker logs pigeonpost-server -f"
