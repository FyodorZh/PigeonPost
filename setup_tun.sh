#!/usr/bin/env bash
set -euo pipefail

MODE="${1:?Usage: $0 public|private <net_device>}"
NET_DEV="${2:?Usage: $0 public|private <net_device>}"

case "$MODE" in
    public)
        TUN="tun_public"
        IP="10.0.0.1"
        PEER="10.0.0.2"
        ;;
    private)
        TUN="tun_private"
        IP="10.0.0.2"
        PEER="10.0.0.1"
        ;;
    *)
        echo "Error: mode must be 'public' or 'private', got '$MODE'"
        exit 1
        ;;
esac

sysctl -w net.ipv4.ip_forward=1

if ! ip link show "$TUN" >/dev/null 2>&1; then
    echo "Creating TUN device: $TUN"
    ip tuntap add dev "$TUN" mode tun
fi

echo "Assigning $IP/30 to $TUN"
ip addr add "$IP/30" dev "$TUN"

echo "Bringing $TUN up"
ip link set "$TUN" up

ip route add "$PEER/32" dev "$TUN" 2>/dev/null || true

case "$MODE" in
    public)
        iptables -t nat -A POSTROUTING -o "$NET_DEV" -s 10.0.0.0/30 -j MASQUERADE
        ;;

    private)
        LOCAL_NET=$(ip -4 addr show dev "$NET_DEV" | awk '/inet / {print $2}')
        echo "Detected local subnet: $LOCAL_NET"

        iptables -t nat -A POSTROUTING -o "$TUN" -j MASQUERADE

        iptables -t mangle -A PREROUTING -i "$NET_DEV" -j MARK --set-mark 1
        grep -qxF "234 tunnel" /etc/iproute2/rt_tables 2>/dev/null || echo "234 tunnel" >> /etc/iproute2/rt_tables
        ip rule add fwmark 1 table 234 2>/dev/null || true
        ip route add default via "$PEER" dev "$TUN" table 234 2>/dev/null || true
        ;;
esac
