#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

mode="tcp"
if [ "${1:-}" = "--mode" ] && [ -n "${2:-}" ]; then
  mode="$2"
fi
export TEST_MODE="$mode"

docker compose down --remove-orphans 2>/dev/null

echo "Starting PigeonPost + iperf ($mode)..."
docker compose up --build --force-recreate -d

echo "Waiting for iperf-clients to finish..."
docker compose ps -q iperf-client-1 iperf-client-2 iperf-client-3 iperf-client-4 | xargs docker wait >/dev/null 2>&1

echo ""
echo "ClientID  Speed(Mbit/s)  Retr  Ping(ms)  Transfer(MB)"
for i in 1 2 3 4; do
  docker compose logs "iperf-client-$i" 2>/dev/null | tail -1
done

echo ""
echo "Tests complete. Stopping all services..."
docker compose stop
