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

echo "iperf-server: waiting for IP 10.0.10.1 on tun0..."
i=0
while [ "$i" -lt 30 ]; do
  if ip addr show dev tun0 2>/dev/null | grep -qF "10.0.10.1"; then
    echo "iperf-server: 10.0.10.1 assigned"
    break
  fi
  echo "iperf-server: 10.0.10.1 not yet assigned (attempt $i)"
  sleep 1
  i=$((i + 1))
done

if ! ip addr show dev tun0 2>/dev/null | grep -qF "10.0.10.1"; then
  echo "iperf-server: ERROR 10.0.10.1 never assigned to tun0"
  exit 1
fi

echo "iperf-server: starting 4 iperf3 daemons (ports 5201-5204)"
for port in 5201 5202 5203 5204; do
  iperf3 -s -B 10.0.10.1 -p "$port" -D
  echo "iperf-server: daemon on port $port started"
done
echo "iperf-server: sleeping forever"
sleep infinity
