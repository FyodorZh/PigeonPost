#!/usr/bin/env bash
#
# One-time host setup for the PigeonPost client machine.
# Run this once per boot (or integrate into /etc/rc.local / systemd).
#
# Usage: ./setup.sh [lan_interface]
#
set -euo pipefail

LAN_IF="${1:-eth0}"
TUN="tun0"
PEER_TUN_IP="10.0.0.1"

echo "=== PigeonPost client setup ==="

# --- TUN device ---
if ! ip link show "$TUN" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN"
    ip tuntap add dev "$TUN" mode tun
fi

# --- IP forwarding ---
echo "Enabling IP forwarding"
sysctl -w net.ipv4.ip_forward=1

# --- Detect local subnet ---
LOCAL_NET=$(ip -4 addr show dev "$LAN_IF" | awk '/inet / {print $2}')
if [ -z "$LOCAL_NET" ]; then
    echo "ERROR: cannot detect subnet on $LAN_IF"
    exit 1
fi
echo "Detected local subnet: $LOCAL_NET on $LAN_IF"

# --- NAT: local subnet -> tunnel ---
RULE="-t nat -A POSTROUTING -o $TUN -j MASQUERADE"
if ! iptables -t nat -C POSTROUTING -o "$TUN" -j MASQUERADE 2>/dev/null; then
    echo "Adding NAT rule: $RULE"
    iptables $RULE
else
    echo "NAT rule already exists"
fi

# --- Mark traffic from LAN ---
MARK_RULE="-t mangle -A PREROUTING -i $LAN_IF -j MARK --set-mark 1"
if ! iptables -t mangle -C PREROUTING -i "$LAN_IF" -j MARK --set-mark 1 2>/dev/null; then
    echo "Adding mangle rule: $MARK_RULE"
    iptables $MARK_RULE
else
    echo "Mangle rule already exists"
fi

# --- Policy routing table ---
TABLE_ID=234
mkdir -p /etc/iproute2
if ! grep -qxF "$TABLE_ID tunnel" /etc/iproute2/rt_tables 2>/dev/null; then
    echo "Adding table $TABLE_ID to /etc/iproute2/rt_tables"
    echo "$TABLE_ID tunnel" >> /etc/iproute2/rt_tables
fi

if ! ip rule show | grep -q "lookup $TABLE_ID"; then
    echo "Adding policy rule for fwmark 1 -> table $TABLE_ID"
    ip rule add fwmark 1 table $TABLE_ID
else
    echo "Policy rule already exists"
fi

if ! ip route show table $TABLE_ID | grep -q "$TUN"; then
    echo "Adding default route via $PEER_TUN_IP dev $TUN to table $TABLE_ID"
    ip route add default via "$PEER_TUN_IP" dev "$TUN" table $TABLE_ID
else
    echo "Route already exists"
fi

echo ""
echo "Setup complete."
echo "Note: sysctl and iptables changes are not persistent across reboots."
echo "      Add sysctl to /etc/sysctl.conf and save iptables with: iptables-save > /etc/iptables/rules.v4"
