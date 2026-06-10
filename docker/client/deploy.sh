#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PIGEON_URL="tcp|203.0.113.10:9000/30"
export PIGEON_URL

docker compose build
docker compose up -d

echo "Client deployed."
echo "  url: $PIGEON_URL"
echo "  logs: docker logs pigeonpost-client -f"
