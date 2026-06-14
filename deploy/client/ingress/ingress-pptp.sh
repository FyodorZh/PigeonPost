#!/usr/bin/env bash
#
# Set up a PPTP VPN server as an ingress source for the PigeonPost tunnel.
# PPTP clients receive IPs from the configured pool; traffic from those IPs
# is marked and routed through the tunnel via the pp-ingress ipset.
#
# Idempotent — safe to run multiple times.
#
# Usage: ./ingress-pptp.sh [<pptp_pool_subnet>]
#
# Default pool:   10.0.3.0/24   (localip .1, remoteip .100-.200)
#
# Environment variables:
#   PPTP_USER     PPTP username  (default: pigeon)
#   PPTP_PASSWORD PPTP password  (default: pigeonpass)
#
set -euo pipefail

PPTP_POOL_SUBNET="${1:-10.0.3.0/24}"
PPTP_USER="${PPTP_USER:-pigeon}"
PPTP_PASSWORD="${PPTP_PASSWORD:-pigeonpass}"

# ---------- Validation ----------
if [[ ! "$PPTP_POOL_SUBNET" =~ ^([0-9]+\.[0-9]+\.[0-9]+)\.0/[0-9]+$ ]]; then
    echo "ERROR: invalid subnet format. Use CIDR, e.g. 10.0.3.0/24"
    exit 1
fi

POOL_BASE="${BASH_REMATCH[1]}"
PPTP_LOCAL_IP="${POOL_BASE}.1"
PPTP_REMOTE_RANGE="${POOL_BASE}.100-${POOL_BASE}.200"

if [ "$PPTP_PASSWORD" = "pigeonpass" ]; then
    echo "WARNING: using default PPTP password. Set PPTP_PASSWORD to change it."
fi

# ---------- Packages ----------
if ! dpkg -s pptpd >/dev/null 2>&1; then
    echo "Installing pptpd"
    apt-get update -qq && apt-get install -y -qq pptpd
fi

# ---------- Kernel modules ----------
for mod in ppp_mppe ip_gre nf_conntrack_proto_gre; do
    modprobe -q "$mod" 2>/dev/null || true
done

# ---------- /etc/pptpd.conf ----------
SENTINEL="# PigeonPost PPTP ingress"
if ! grep -qF "$SENTINEL" /etc/pptpd.conf 2>/dev/null; then
    echo "Configuring /etc/pptpd.conf"
    cat >> /etc/pptpd.conf <<EOF

$SENTINEL
option /etc/ppp/pptpd-options
logwtmp
localip $PPTP_LOCAL_IP
remoteip $PPTP_REMOTE_RANGE
EOF
fi

# ---------- /etc/ppp/pptpd-options ----------
if ! grep -qF "$SENTINEL" /etc/ppp/pptpd-options 2>/dev/null; then
    echo "Configuring /etc/ppp/pptpd-options"
    cat >> /etc/ppp/pptpd-options <<'EOF'

# PigeonPost PPTP ingress
name pptpd
refuse-pap
refuse-chap
refuse-mschap
require-mschap-v2
require-mppe-128
ms-dns 1.1.1.1
ms-dns 8.8.8.8
proxyarp
nodefaultroute
lock
nobsdcomp
noipx
EOF
fi

# ---------- /etc/ppp/chap-secrets ----------
if ! grep -qF "$PPTP_USER pptpd" /etc/ppp/chap-secrets 2>/dev/null; then
    echo "Adding PPTP user: $PPTP_USER"
    echo "$PPTP_USER pptpd $PPTP_PASSWORD *" >> /etc/ppp/chap-secrets
fi

# ---------- Firewall ----------
echo "Opening PPTP firewall ports"

if ! iptables -C INPUT -p gre -j ACCEPT 2>/dev/null; then
    iptables -A INPUT -p gre -j ACCEPT
fi

if ! iptables -C INPUT -p tcp --dport 1723 -j ACCEPT 2>/dev/null; then
    iptables -A INPUT -p tcp --dport 1723 -j ACCEPT
fi

# ---------- IP forwarding ----------
sysctl -w net.ipv4.ip_forward=1 >/dev/null

# ---------- Register PPTP pool into pp-ingress ----------
echo "Registering $PPTP_POOL_SUBNET into pp-ingress"
ipset add pp-ingress "$PPTP_POOL_SUBNET" -exist

# ---------- Service ----------
if ! systemctl is-active --quiet pptpd; then
    echo "Starting pptpd"
    systemctl start pptpd
fi

if ! systemctl is-enabled --quiet pptpd 2>/dev/null; then
    echo "Enabling pptpd"
    systemctl enable pptpd
fi

echo ""
echo "PPTP ingress setup complete."
echo "Clients in $PPTP_POOL_SUBNET are routed through the tunnel."
echo "Connect to this machine (port 1723) with:"
echo "  username = $PPTP_USER"
echo "  password = $PPTP_PASSWORD"
