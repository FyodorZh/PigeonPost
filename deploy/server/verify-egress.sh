#!/usr/bin/env bash
#
# verify-egress.sh — Verify server egress under the unified VPN subnet.
#
# Checks that the server TUN device has the unified subnet address,
# that NAT and FORWARD rules exist for the subnet, and that route
# resolution returns tun0 for any client IP in the subnet range.
#
# Usage:
#   ./verify-egress.sh [<wan_interface>]
#
# Exit code: 0 if all checks pass, 1 otherwise.
#
set -euo pipefail

TUN="tun0"
WAN_IF="${1:-}"
TUN_CIDR="10.0.10.1/24"
TUN_NET="10.0.10.0/24"
TUN_IP="${TUN_CIDR%/*}"

has_error=0

check() {
    local desc="$1"
    shift
    if "$@" >/dev/null 2>&1; then
        echo "  PASS: $desc"
    else
        echo "  FAIL: $desc"
        has_error=1
    fi
}

echo "=== Server egress verification ==="
echo ""

echo "--- TUN device ---"
check "tun0 exists" ip link show "$TUN"
check "address $TUN_CIDR on $TUN" ip addr show dev "$TUN" grep -qF "$TUN_IP"

echo ""
echo "--- Route resolution ---"
check "client 10.0.10.11 routes via tun0" ip route get 10.0.10.11 grep -qF "dev $TUN"
check "client 10.0.10.42 routes via tun0" ip route get 10.0.10.42 grep -qF "dev $TUN"
check "client 10.0.10.254 routes via tun0" ip route get 10.0.10.254 grep -qF "dev $TUN"

echo ""
echo "--- NAT rules ---"
if [ -n "$WAN_IF" ]; then
    check "NAT $TUN_NET -> $WAN_IF" iptables -t nat -C POSTROUTING -o "$WAN_IF" -s "$TUN_NET" -j MASQUERADE
else
    echo "  SKIP: NAT checks (no WAN interface specified)"
    echo "        re-run: $0 <wan_interface>"
fi

echo ""
echo "--- FORWARD rules ---"
if [ -n "$WAN_IF" ]; then
    check "FORWARD $TUN_NET $TUN -> $WAN_IF" iptables -C FORWARD -i "$TUN" -o "$WAN_IF" -s "$TUN_NET" -j ACCEPT
    check "FORWARD RELATED,ESTABLISHED $WAN_IF -> $TUN" iptables -C FORWARD -i "$WAN_IF" -o "$TUN" -m state --state RELATED,ESTABLISHED -j ACCEPT
else
    echo "  SKIP: FORWARD checks (no WAN interface specified)"
fi

echo ""
if [ "$has_error" -eq 0 ]; then
    echo "All checks passed."
else
    echo "One or more checks failed."
fi
exit "$has_error"
