# PigeonPost V1 Network Contract

## Unified VPN Client Subnet
- **Subnet**: `10.0.10.0/24`
- **Server TUN IP**: `10.0.10.1`
- **Linux client range**: `10.0.10.2–10`
- **Endpoint/mobile client range**: `10.0.10.11–254`

## Allocation Rules
- Each client gets exactly one unique IP from its range.
- Duplicate IP claims are rejected by the server at handshake time.
- Endpoint clients cannot reach other VPN subnet peers (enforced by server).
- Linux clients can reach all peers.

## DNS
Primary: `1.1.1.1`, Secondary: `1.0.0.1`

## Server Requirements
- TUN device `tun0` with address `10.0.10.1/24`
- IP forwarding enabled
- NAT: `POSTROUTING -o <WAN_IF> -s 10.0.10.0/24 -j MASQUERADE`
- FORWARD: outbound (`-i tun0 -o <WAN_IF>`) and inbound (`-i <WAN_IF> -o tun0 --state RELATED,ESTABLISHED`)

## Client Requirements (Linux)
- TUN device with unique IP from `10.0.10.2–10`
- Route to server TUN IP (`10.0.10.1`) via TUN device
- NAT: `POSTROUTING -o <TUN_NAME> -j MASQUERADE`
- Optional: `pp-ingress` ipset + policy routing for selective tunnel
