# PigeonPost Docker Integration Test — AGENTS.md

## Overview

`run-test.sh` orchestrates an integration test of PigeonPost's **1-to-many** server
model using Docker. It stands up one PigeonPost server, four PigeonPost clients (each
with unique IP and client identity), and five iperf3 sidecars — one server-side and
four client-side — then runs concurrent throughput tests on separate ports.

## Architecture

```
                     Docker bridge network "pigeon-net"

                     ┌──────────────────────────────┐
                     │  iperf-server                 │
                     │  cap_add: NET_ADMIN            │
                     │  adds return routes +          │
                     │  waits for IP 10.0.0.1         │
                     │  starts 4 daemons (5201-5204)  │
                     │  network_mode: "service:server"│
                     └────────┬─────────────────────┘
                              │ shares netns
                     ┌────────┴────────────┐
                     │  server              │
                     │  TUN 10.0.0.1/30     │
                     │  PigeonPost --role   │
                     │  server, TCP :9000   │
                     └────────┬────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
  ┌───────┴───────┐   ┌──────┴───────┐   ┌───────┴───────┐
  │  client-1     │   │  client-2    │   │  client-3     │
  │  TUN          │   │  TUN         │   │  TUN          │  ...
  │  10.0.0.2/30  │   │  10.0.0.6/30│   │  10.0.0.9/30  │
  │  --client-id  │   │  --client-id │   │  --client-id  │
  │  pp-test-     │   │  pp-test-    │   │  pp-test-     │
  │  client-1     │   │  client-2    │   │  client-3     │
  └───────┬───────┘   └──────┬───────┘   └──────┬───────┘
          │  port 5201      │  port 5202       │  port 5203
  ┌───────┴───────┐   ┌──────┴───────┐   ┌───────┴───────┐
  │ iperf-client-1 │   │iperf-client-2│   │iperf-client-3 │
  │ network_mode:  │   │network_mode: │   │network_mode:  │
  │ "service:      │   │"service:     │   │"service:      │
  │  client-1"     │   │ client-2"    │   │ client-3"     │
  │ IPERF_PORT=5201│   │IPERF_PORT=5202│   │IPERF_PORT=5203│
  └───────────────┘   └──────────────┘   └───────────────┘
```

## Test Flow

### 1. Cleanup
```
docker compose down --remove-orphans
```
Removes any leftover containers, networks, or orphans from a prior run.

### 2. Build and start all services
```
docker compose up --build --force-recreate -d
```
Builds the PigeonPost image (multi-stage Dockerfile), creates all containers, and
starts them detached.

### 3. Wait for completion
```
docker compose ps -q iperf-client-1 iperf-client-2 iperf-client-3 iperf-client-4 | xargs docker wait
```
Collects the container IDs of all four iperf-clients and waits until every one
has exited.

### 4. Collect logs
```
for i in 1 2 3 4; do
  echo "=== iperf-client-$i ==="
  docker compose logs "iperf-client-$i"
done
```
Prints the sequential output of each iperf-client, labelled by container name.

### 5. Teardown
```
docker compose stop
```
Stops all containers but does **not** remove them, preserving logs for
inspection. To clean up fully, run `docker compose down` manually.

## Container roles

| Service | Count | Role |
|---------|-------|------|
| `server` | 1 | PigeonPost server, TCP listener on :9000, TUN 10.0.0.1 |
| `client-1`–`client-4` | 4 | PigeonPost clients, TUNs 10.0.0.2/6/9/13, client-ids `pp-test-client-1`–`4` |
| `iperf-server` | 1 | 4 iperf3 daemons (ports 5201–5204), all bound to 10.0.0.1, `network_mode: "service:server"` |
| `iperf-client-1`–`iperf-client-4` | 4 | iperf3 clients, each on dedicated port, `network_mode: "service:client-N"` |

## Client mapping

| Client | TUN IP | client-id | iperf sidecar | iperf3 port |
|--------|--------|-----------|---------------|-------------|
| `client-1` | 10.0.0.2 | `pp-test-client-1` | `iperf-client-1` | 5201 |
| `client-2` | 10.0.0.6 | `pp-test-client-2` | `iperf-client-2` | 5202 |
| `client-3` | 10.0.0.9 | `pp-test-client-3` | `iperf-client-3` | 5203 |
| `client-4` | 10.0.0.13 | `pp-test-client-4` | `iperf-client-4` | 5204 |

Each iperf-client connects to its own port, enabling all four to run
concurrently. iperf3's `-s` mode is single-threaded — it handles one test at
a time per daemon instance.

## Traffic tests (per iperf-client)

Each iperf-client executes the iperf-client.sh script which:
1. Polls `iperf3 -c 10.0.0.1 -p $IPERF_PORT -t 1 --connect-timeout 3000` until
   reachable (up to 60 attempts with 1-second sleep between each)
2. Runs **UDP 100 Mbps, 30 seconds**
3. Runs **TCP 100 Mbps, 30 seconds**
4. Exits

All four run concurrently via separate containers on separate ports. The `--connect-timeout 3000` prevents the polling loop from hanging on slow tunnel startup (each attempt times out in 3 seconds instead of the default TCP connect timeout of 20+ seconds).

## iperf server lifecycle

The iperf-server.sh script:
1. Installs `iproute2` (needed for the `ip` command — not present in the
   `networkstatic/iperf3` image)
2. Polls for `/sys/class/net/tun0` (up to 30s)
3. Adds `/32` host routes via `tun0` for non-default client TUN IPs
   (`10.0.0.6`, `10.0.0.9`, `10.0.0.13`) — return path for server→client
   traffic through the tunnel
4. Polls for IP `10.0.0.1` to be assigned to `tun0` (up to 30s)
5. Starts 4 `iperf3 -s -D` daemons on ports 5201–5204, all bound to `10.0.0.1`
6. Sleeps forever

The iperf-server container has `cap_add: NET_ADMIN` because it modifies the
routing table in the server's network namespace.

## Why non-contiguous client IPs?

The shared `docker-entrypoint.sh` assigns TUN IPs with a hardcoded `/30`
netmask (`ip addr add "$TUN_IP/30"`). In a `/30` subnet, only addresses where
`X % 4 == 1` or `X % 4 == 2` are valid host addresses — the others are the
network and broadcast addresses. Additionally, `10.0.0.3` (the broadcast of
`10.0.0.0/30`) causes connectivity issues because the kernel treats it as a
broadcast address rather than a unicast host. Therefore the four client TUN
IPs must be `10.0.0.2`, `10.0.0.6`, `10.0.0.9`, `10.0.0.13`.

## Return routing

Since the server's entrypoint only adds a single `PEER_IP` route (`10.0.0.2`
for client-1), the iperf-server sidecar injects additional `/32` host routes
for the remaining client TUN IPs. Without these, server→client response
traffic would exit via the default bridge gateway instead of through `tun0`.

## Entrypoint guard fix

The original `docker-entrypoint.sh` used `ip route show dev "$TUN_NAME" | grep -qF "$PEER_IP"` to check if the peer route already exists. This is flawed
because `grep -F` does a substring match: a connected route like
`10.0.0.12/30` contains the substring `10.0.0.1`, causing the guard to
skip adding the peer route. Fixed to use `ip route get "$PEER_IP" | grep -qF "dev $TUN_NAME"` which checks the actual routing decision.

## Portability

- Linux-only (TUN devices, NET_ADMIN capability)
- Requires Docker with Compose plugin (v2+)
- No internet access needed at runtime (nugets bundled, images pulled at build)
- All four clients share the same TUN device name (`tun0`) — no conflict because each
  lives in its own network namespace

## Enforcement

Any change to the Docker-based integration test behaviour (docker-compose.yml,
run-test.sh, iperf-server.sh, iperf-client.sh, or any supporting file in this
directory) MUST be reflected in this AGENTS.md. The file serves as the single source
of truth for the test architecture and flow. Keeping it accurate ensures that both
agents and human maintainers can understand the test without re-reading every script.
