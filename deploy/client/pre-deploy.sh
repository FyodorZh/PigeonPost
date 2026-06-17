#!/usr/bin/env bash
#
# One-time host setup for the PigeonPost client machine.
# Run this once per boot (or integrate into /etc/rc.local / systemd).
#
# Configures a TUN device with a unique client IP from the VPN subnet,
# sets up policy routing for ingress traffic, and NATs it through the
# tunnel to the server.
#
# Environment variables (with defaults):
#   TUN_NAME  — TUN device name  (default: tun0)
#   TUN_CIDR  — client TUN address, e.g. 10.0.10.11/24  (required)
#   PEER_IP   — server TUN gateway IP, e.g. 10.0.10.1    (required)
#
# Usage: TUN_CIDR=10.0.10.11/24 PEER_IP=10.0.10.1 ./setup.sh
#
set -euo pipefail

: "${TUN_CIDR:?TUN_CIDR is required (e.g. 10.0.10.11/24)}"
: "${PEER_IP:?PEER_IP is required (e.g. 10.0.10.1)}"

TUN_NAME="${TUN_NAME:-tun0}"
TUN_IP="${TUN_CIDR%/*}"

echo "=== PigeonPost client setup ==="
echo "  TUN:   $TUN_NAME"
echo "  CIDR:  $TUN_CIDR"
echo "  Peer:  $PEER_IP"

# --- ipset for ingress traffic routing ---
if ! command -v ipset >/dev/null 2>&1; then
    echo "Installing ipset"
    apt-get update -qq && apt-get install -y -qq ipset
fi

if ! ipset list pp-ingress >/dev/null 2>&1; then
    echo "Creating ipset: pp-ingress"
    ipset create pp-ingress hash:net
fi

# --- TUN device ---
if ! ip link show "$TUN_NAME" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN_NAME"
    ip tuntap add dev "$TUN_NAME" mode tun
fi

if ! ip addr show dev "$TUN_NAME" | grep -qF "$TUN_IP"; then
    echo "Assigning $TUN_CIDR to $TUN_NAME"
    ip addr add "$TUN_CIDR" dev "$TUN_NAME"
fi

echo "Bringing $TUN_NAME up"
ip link set "$TUN_NAME" up

if ! ip route get "$PEER_IP" 2>/dev/null | grep -qF "dev $TUN_NAME"; then
    echo "Adding route to peer $PEER_IP via $TUN_NAME"
    ip route add "$PEER_IP/32" dev "$TUN_NAME"
fi

# --- IP forwarding ---
echo "Enabling IP forwarding"
sysctl -w net.ipv4.ip_forward=1

# --- NAT: tunnel -> internet ---
RULE="-t nat -A POSTROUTING -o $TUN_NAME -j MASQUERADE"
if ! iptables -t nat -C POSTROUTING -o "$TUN_NAME" -j MASQUERADE 2>/dev/null; then
    echo "Adding NAT rule: $RULE"
    iptables $RULE
else
    echo "NAT rule already exists"
fi

# --- Mark traffic from registered ingress sources ---
MARK_RULE="-t mangle -A PREROUTING -m set --match-set pp-ingress src -j MARK --set-mark 1"
if ! iptables -t mangle -C PREROUTING -m set --match-set pp-ingress src -j MARK --set-mark 1 2>/dev/null; then
    echo "Adding mangle rule: $MARK_RULE"
    iptables $MARK_RULE
else
    echo "Mangle rule already exists"
fi

# --- FORWARD: allow traffic through the tunnel ---
FORWARD_TUN_IN="-A FORWARD -o $TUN_NAME -j ACCEPT"
if ! iptables -C FORWARD -o "$TUN_NAME" -j ACCEPT 2>/dev/null; then
    echo "Adding FORWARD rule: $FORWARD_TUN_IN"
    iptables $FORWARD_TUN_IN
else
    echo "FORWARD outbound rule already exists"
fi

FORWARD_TUN_OUT="-A FORWARD -i $TUN_NAME -m state --state RELATED,ESTABLISHED -j ACCEPT"
if ! iptables -C FORWARD -i "$TUN_NAME" -m state --state RELATED,ESTABLISHED -j ACCEPT 2>/dev/null; then
    echo "Adding FORWARD rule: $FORWARD_TUN_OUT"
    iptables $FORWARD_TUN_OUT
else
    echo "FORWARD inbound rule already exists"
fi

# --- Policy routing table ---
TABLE_ID=234
mkdir -p /etc/iproute2
if ! grep -qxF "$TABLE_ID tunnel" /etc/iproute2/rt_tables 2>/dev/null; then
    echo "Adding table $TABLE_ID to /etc/iproute2/rt_tables"
    echo "$TABLE_ID tunnel" >> /etc/iproute2/rt_tables
fi

if ! ip route show table $TABLE_ID 2>/dev/null | grep -q "$TUN_NAME"; then
    echo "Adding default route via $PEER_IP dev $TUN_NAME to table $TABLE_ID"
    ip route add default via "$PEER_IP" dev "$TUN_NAME" table $TABLE_ID
else
    echo "Route already exists"
fi

if ! ip rule show 2>/dev/null | grep -q "lookup $TABLE_ID"; then
    echo "Adding policy rule for fwmark 1 -> table $TABLE_ID"
    ip rule add fwmark 1 table $TABLE_ID
else
    echo "Policy rule already exists"
fi

echo ""
echo "Setup complete."
echo "Client TUN: $TUN_CIDR (peer $PEER_IP)"
echo "Note: sysctl and iptables changes are not persistent across reboots."
echo "      Add sysctl to /etc/sysctl.conf and save iptables with: iptables-save > /etc/iptables/rules.v4"
