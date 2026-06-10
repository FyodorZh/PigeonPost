#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

docker compose down --remove-orphans 2>/dev/null

echo "Starting PigeonPost + iperf..."
docker compose up --build --force-recreate -d

echo "Waiting for iperf-client to finish..."
container=$(docker compose ps -q iperf-client)
docker wait "$container" >/dev/null 2>&1

echo ""
docker compose logs iperf-client 2>/dev/null

echo ""
echo "Tests complete. Stopping all services..."
docker compose down
