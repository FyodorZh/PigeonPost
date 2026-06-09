#!/usr/bin/env bash
set -euo pipefail

TUN_A="${1:-tunA}"
TUN_B="${2:-tunB}"
CLIENT_NS="ns_client"
SERVER_NS="ns_server"

# --- Enable routing ---
sysctl -w net.ipv4.ip_forward=1
sysctl -w net.ipv4.conf.all.rp_filter=0

# --- Create TUN devices ---
for tun in "$TUN_A" "$TUN_B"; do
    if ! ip link show "$tun" >/dev/null 2>&1; then
        echo "Creating TUN device: $tun"
        ip tuntap add dev "$tun" mode tun
    fi
done

echo "Assigning 10.0.0.1/30 to $TUN_A"
ip addr add 10.0.0.1/30 dev "$TUN_A"
echo "Bringing $TUN_A up"
ip link set "$TUN_A" up
ip route add 10.0.0.2/32 dev "$TUN_A" 2>/dev/null || true

echo "Assigning 10.0.0.2/30 to $TUN_B"
ip addr add 10.0.0.2/30 dev "$TUN_B"
echo "Bringing $TUN_B up"
ip link set "$TUN_B" up
ip route add 10.0.0.1/32 dev "$TUN_B" 2>/dev/null || true

# --- Create network namespaces and veth pairs ---
ip netns add "$CLIENT_NS" 2>/dev/null || true
ip netns add "$SERVER_NS" 2>/dev/null || true

ip link add veth0 type veth peer name veth0_ns
ip link add veth1 type veth peer name veth1_ns

ip link set veth0_ns netns "$CLIENT_NS"
ip link set veth1_ns netns "$SERVER_NS"

# Root side of veths
ip addr add 172.16.0.1/24 dev veth0
ip addr add 172.16.1.1/24 dev veth1
ip link set veth0 up
ip link set veth1 up

# Namespace side of veths
ip netns exec "$CLIENT_NS" ip addr add 172.16.0.2/24 dev veth0_ns
ip netns exec "$SERVER_NS" ip addr add 172.16.1.2/24 dev veth1_ns
ip netns exec "$CLIENT_NS" ip link set veth0_ns up
ip netns exec "$SERVER_NS" ip link set veth1_ns up
ip netns exec "$CLIENT_NS" ip link set lo up
ip netns exec "$SERVER_NS" ip link set lo up

# Default gateways in namespaces (point to root)
ip netns exec "$CLIENT_NS" ip route add default via 172.16.0.1
ip netns exec "$SERVER_NS" ip route add default via 172.16.1.1

# --- Policy routing in root to force traffic through TUNs ---
mkdir -p /etc/iproute2
touch /etc/iproute2/rt_tables

# Client → Server: go via tunA → PigeonPost
grep -qxF "100 debug_client" /etc/iproute2/rt_tables 2>/dev/null || echo "100 debug_client" >> /etc/iproute2/rt_tables
ip rule add iif veth0 table 100 2>/dev/null || true
ip route add 172.16.1.0/24 dev "$TUN_A" table 100 2>/dev/null || true

# Server → Client: go via tunB → PigeonPost
grep -qxF "101 debug_server" /etc/iproute2/rt_tables 2>/dev/null || echo "101 debug_server" >> /etc/iproute2/rt_tables
ip rule add iif veth1 table 101 2>/dev/null || true
ip route add 172.16.0.0/24 dev "$TUN_B" table 101 2>/dev/null || true

# After app (from tunB → server ns directly)
grep -qxF "102 debug_tunB" /etc/iproute2/rt_tables 2>/dev/null || echo "102 debug_tunB" >> /etc/iproute2/rt_tables
ip rule add iif "$TUN_B" table 102 2>/dev/null || true
ip route add 172.16.1.0/24 dev veth1 table 102 2>/dev/null || true

# After app (from tunA → client ns directly)
grep -qxF "103 debug_tunA" /etc/iproute2/rt_tables 2>/dev/null || echo "103 debug_tunA" >> /etc/iproute2/rt_tables
ip rule add iif "$TUN_A" table 103 2>/dev/null || true
ip route add 172.16.0.0/24 dev veth0 table 103 2>/dev/null || true

echo ""
echo "Done. Run apps in namespaces:"
echo "  Server: ip netns exec $SERVER_NS <cmd>"
echo "  Client: ip netns exec $CLIENT_NS <cmd>"
echo ""
echo "Example:"
echo "  ip netns exec $SERVER_NS iperf3 -s -p 7777"
echo "  ip netns exec $CLIENT_NS iperf3 -c 172.16.1.2 -p 7777"
