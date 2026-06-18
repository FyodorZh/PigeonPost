#!/usr/bin/env bash
#
# Debug helper: creates an isolated network namespace that sends all traffic
# through the PigeonPost tunnel. Useful for testing tunnel connectivity without
# Docker and without affecting host routing.
#
# How it works:
#   1. Creates a 'pp-debug' network namespace (like a mini isolated computer).
#   2. Connects it to the host via a veth pair (a virtual Ethernet cable):
#        pp-debug [veth1] ===== [veth0] host
#   3. Assigns link-local IPs (169.254.x.x) — safe, never conflicts with real
#      networks because these addresses are non-routable on the public internet.
#   4. Registers veth0's subnet into pp-ingress. Any packet arriving on veth0
#      with a matching source IP gets MARK 1 → policy routing → tun0 → tunnel.
#   5. Starts a continuous ping to 8.8.8.8 from inside the namespace.
#
# Traffic flow (outbound):
#   ping 8.8.8.8 (src 169.254.1.2)
#   → veth1 → veth0 → PREROUTING matches pp-ingress
#   → MARK 1 → table 234 → route via tun0
#   → MASQUERADE (src becomes client TUN IP, e.g. 10.0.10.11)
#   → PigeonPost tunnel → server → internet
#
# Return flow is the reverse: internet → server → tunnel → MASQUERADE undo
# → veth0 → veth1 → namespace sees the reply.
#
# Cleanup (manual, after Ctrl+C):
#   ip netns del pp-debug
#   ip link del veth0
#   ipset del pp-ingress 169.254.1.0/30
#
# Usage: ./ingress-debug.sh [command]
#   command — command to run inside the namespace (default: ping 8.8.8.8)
# Run after pre-deploy.sh. Press Ctrl+C to stop.
#

set -euo pipefail

NS="pp-debug"
VETH_SUBNET="169.254.1.0/30"
HOST_IP="169.254.1.1"
NS_IP="169.254.1.2"
VETH_PEER="veth1"

# --- 1. Create network namespace ---

NS_BIND="/run/netns/$NS"
if [ -f "$NS_BIND" ]; then
    echo "Network namespace '$NS' already exists. Skipping creation."
elif ip netns list 2>/dev/null | grep -qxF "$NS"; then
    echo "Network namespace '$NS' already exists. Skipping creation."
else
    echo "Creating network namespace: $NS"
    ip netns add "$NS"
fi

# --- 2. Create veth pair (virtual Ethernet cable) ---

if ip link show veth0 >/dev/null 2>&1; then
    echo "veth0 already exists. Skipping creation."
else
    echo "Creating veth pair: veth0 <-> $VETH_PEER"
    ip link add veth0 type veth peer name "$VETH_PEER"

    echo "Moving $VETH_PEER into namespace $NS"
    ip link set "$VETH_PEER" netns "$NS"

    # Enable IPv4 forwarding between veth0 and the host
    # (the netns sends packets to veth0, which must be forwarded to tun0)
    echo 1 > /proc/sys/net/ipv4/ip_forward 2>/dev/null || true
fi

# --- 3. Assign IP addresses ---

HAS_HOST_IP=$(ip addr show dev veth0 2>/dev/null | grep -F "$HOST_IP" || true)
if [ -z "$HAS_HOST_IP" ]; then
    echo "Assigning $HOST_IP/30 to veth0"
    ip addr add "$HOST_IP/30" dev veth0
fi

HAS_NS_IP=$(ip netns exec "$NS" ip addr show dev "$VETH_PEER" 2>/dev/null | grep -F "$NS_IP" || true)
if [ -z "$HAS_NS_IP" ]; then
    echo "Assigning $NS_IP/30 to $VETH_PEER (inside $NS)"
    ip netns exec "$NS" ip addr add "$NS_IP/30" dev "$VETH_PEER"
fi

# --- 4. Bring interfaces up ---

echo "Bringing veth0 up"
ip link set veth0 up

echo "Bringing $VETH_PEER up (inside $NS)"
ip netns exec "$NS" ip link set "$VETH_PEER" up

# Required for IPv6 statelessness — the loopback inside the netns needs to be up too
ip netns exec "$NS" ip link set lo up 2>/dev/null || true

# --- 5. Default route inside namespace (via host) ---

HAS_ROUTE=$(ip netns exec "$NS" ip route show default 2>/dev/null | grep -F "$HOST_IP" || true)
if [ -z "$HAS_ROUTE" ]; then
    echo "Adding default route inside $NS via $HOST_IP"
    ip netns exec "$NS" ip route add default via "$HOST_IP"
fi

# --- 6. Register veth subnet to pp-ingress ---

# Packets arriving on veth0 with source in VETH_SUBNET will now get MARK 1
# and be routed through the tunnel (via policy routing table 234).
echo "Registering $VETH_SUBNET to pp-ingress"

if ! ipset list pp-ingress >/dev/null 2>&1; then
    echo "ERROR: pp-ingress not found. Run pre-deploy.sh first."
    exit 1
fi

ipset add pp-ingress "$VETH_SUBNET" -exist

# --- 7. Execute command inside namespace ---

if [ $# -eq 0 ]; then
    CMD=(env PS1='(pp-debug) \w\$ ' bash)
else
    CMD=("$@")
fi

echo ""
echo "=== Debug mode active ==="
echo "  Namespace:       $NS"
echo "  Namespace IP:    $NS_IP"
echo "  Host-side IP:    $HOST_IP"
echo "  Command:         ${CMD[*]}"
echo "  Press Ctrl+C to stop."
echo ""

ip netns exec "$NS" "${CMD[@]}"
