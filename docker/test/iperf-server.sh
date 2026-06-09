#!/bin/sh

echo "iperf-server: waiting for tun0..."
i=0
while [ "$i" -lt 30 ]; do
  if [ -d /sys/class/net/tun0 ]; then
    echo "iperf-server: tun0 found"
    break
  fi
  echo "iperf-server: tun0 not found (attempt $i)"
  sleep 1
  i=$((i + 1))
done

if [ ! -d /sys/class/net/tun0 ]; then
  echo "iperf-server: ERROR tun0 never appeared"
  ls /sys/class/net/ 2>/dev/null
  exit 1
fi

echo "iperf-server: starting iperf3 daemon"
iperf3 -s -B 10.0.0.1 -D
echo "iperf-server: iperf3 daemon started, sleeping forever"
sleep infinity
