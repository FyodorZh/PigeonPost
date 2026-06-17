#!/bin/sh

: ${IPERF_PORT:=5201}

echo "Waiting for 10.0.10.1:$IPERF_PORT..."
start=$(date +%s)
while ! iperf3 -c 10.0.10.1 -p "$IPERF_PORT" -t 1 --connect-timeout 3000 >/dev/null 2>&1; do
  if [ "$(( $(date +%s) - start ))" -ge 60 ]; then
    echo "ERROR: could not reach 10.0.10.1:$IPERF_PORT after 60s"
    exit 1
  fi
  sleep 1
done

sleep 1

client_id=$(( IPERF_PORT - 5200 ))

: ${TEST_MODE:=tcp}

strip_val() {
  sed 's/.*:[[:space:]]*//'
}

if [ "$TEST_MODE" = "udp" ]; then
  result=$(iperf3 -c 10.0.10.1 -p "$IPERF_PORT" -u -b 100M -t 30 -J 2>/dev/null)

  speed=$(echo "$result" | grep -o '"bits_per_second":[[:space:]]*[0-9.]*' | tail -1 | strip_val)
  lost=$(echo "$result" | grep -o '"lost_packets":[[:space:]]*[0-9]*' | strip_val)
  total=$(echo "$result" | grep -o '"packets":[[:space:]]*[0-9]*' | tail -1 | strip_val)
  jitter=$(echo "$result" | grep -o '"jitter_ms":[[:space:]]*[0-9.]*' | strip_val)

  speed_val=$(awk -v v="$speed" 'BEGIN { printf "%d", v / 1000000 }')
  [ -z "$lost" ] && lost=0
  [ -z "$total" ] && total=0
  [ -z "$jitter" ] && jitter=0

  printf "%-9d %-14s %-6s %-6s %-8s\n" "$client_id" "${speed_val}*" "$lost" "$total" "$jitter"
else
  result=$(iperf3 -c 10.0.10.1 -p "$IPERF_PORT" -b 100M -t 30 -J 2>/dev/null)
  exit_code=$?

  if [ "$exit_code" -ne 0 ]; then
    printf "%-9d %-14s %-6s %-8s %-10s\n" "$client_id" "ERR" "-" "-" "-"
    exit "$exit_code"
  fi

  speed=$(echo "$result" | grep -o '"bits_per_second":[[:space:]]*[0-9.]*' | tail -1 | strip_val)
  retr=$(echo "$result" | grep -o '"retransmits":[[:space:]]*[0-9]*' | tail -1 | strip_val)
  rtt=$(echo "$result" | grep -o '"mean_rtt":[[:space:]]*[0-9]*' | strip_val)
  tx_bytes=$(echo "$result" | grep -o '"bytes":[[:space:]]*[0-9]*' | tail -1 | strip_val)

  speed_val=$(awk -v v="$speed" 'BEGIN { printf "%d", v / 1000000 }')
  rtt_val=$(awk -v u="$rtt" 'BEGIN { printf "%.2f", u / 1000 }')
  tx_val=$(awk -v b="$tx_bytes" 'BEGIN { printf "%.1f", b / 1000000 }')
  [ -z "$retr" ] && retr=0
  [ -z "$rtt_val" ] && rtt_val="-"

  printf "%-9d %-14d %-6d %-8s %-10.1f\n" "$client_id" "$speed_val" "$retr" "$rtt_val" "$tx_val"
fi
