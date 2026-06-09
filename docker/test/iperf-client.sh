#!/bin/sh

echo "Waiting for 10.0.0.1..."
count=0
while ! iperf3 -c 10.0.0.1 -t 1 >/dev/null 2>&1; do
  count=$((count + 1))
  if [ "$count" -ge 60 ]; then
    echo "ERROR: could not reach 10.0.0.1 after 60s"
    exit 1
  fi
  sleep 1
done

echo ""
echo "=== UDP 100M 30s ==="
iperf3 -c 10.0.0.1 -u -b 100M -t 30

echo ""
echo "=== TCP 100M 30s ==="
iperf3 -c 10.0.0.1 -b 100M -t 30

echo ""
echo "=== Done ==="
