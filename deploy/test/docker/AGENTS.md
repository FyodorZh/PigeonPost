# PigeonPost Docker Integration Test — AGENTS.md

## Overview

`run-test.sh` orchestrates an integration test of PigeonPost's **1-to-many** server
model using Docker. It stands up one PigeonPost server, four PigeonPost clients (each
with unique IP identity), and five iperf3 sidecars — one server-side and
four client-side — then runs concurrent throughput tests on separate ports.

## Architecture

```
                     Docker bridge network "pigeon-net"

                     ┌──────────────────────────────┐
                     │  iperf-server                 │
                     │  cap_add: NET_ADMIN            │
                     │  waits for IP 10.0.10.1        │
                     │  starts 4 daemons (5201-5204)  │
                     │  network_mode: "service:server"│
                     └────────┬─────────────────────┘
                              │ shares netns
                     ┌────────┴────────────┐
                     │  server              │
                     │  TUN 10.0.10.1/24    │
                     │  PigeonPost --role   │
                     │  server, TCP :9000   │
                     └────────┬────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
  ┌───────┴───────┐   ┌──────┴───────┐   ┌───────┴───────┐
  │  client-1     │   │  client-2    │   │  client-3     │
  │  TUN          │   │  TUN         │   │  TUN          │  ...
  │  10.0.10.11/24│   │  10.0.10.12/24│   │ 10.0.10.13/24│
   │  (IP identity)│   │(IP identity) │   │ (IP identity) │
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
| `server` | 1 | PigeonPost server, TCP listener on :9000, TUN 10.0.10.1/24 |
| `client-1`–`client-4` | 4 | PigeonPost clients, TUNs 10.0.10.11/12/13/14, identified by TUN IP |
| `iperf-server` | 1 | 4 iperf3 daemons (ports 5201–5204), all bound to 10.0.10.1, `network_mode: "service:server"` |
| `iperf-client-1`–`iperf-client-4` | 4 | iperf3 clients, each on dedicated port, `network_mode: "service:client-N"` |

## Client mapping

| Client | TUN CIDR | iperf sidecar | iperf3 port |
|--------|----------|---------------|-------------|
| `client-1` | 10.0.10.11/24 | `iperf-client-1` | 5201 |
| `client-2` | 10.0.10.12/24 | `iperf-client-2` | 5202 |
| `client-3` | 10.0.10.13/24 | `iperf-client-3` | 5203 |
| `client-4` | 10.0.10.14/24 | `iperf-client-4` | 5204 |

Each iperf-client connects to its own port, enabling all four to run
concurrently. iperf3's `-s` mode is single-threaded — it handles one test at
a time per daemon instance.

## Traffic tests (per iperf-client)

Each iperf-client executes the iperf-client.sh script which:
1. Polls `iperf3 -c 10.0.10.1 -p $IPERF_PORT -t 1 --connect-timeout 3000` until
   reachable (up to 60 attempts with 1-second sleep between each)
2. Runs **UDP 100 Mbps, 30 seconds**
3. Runs **TCP 100 Mbps, 30 seconds**
4. Exits

All four run concurrently via separate containers on separate ports. The `--connect-timeout 3000` prevents the polling loop from hanging on slow tunnel startup (each attempt times out in 3 seconds instead of the default TCP connect timeout of 20+ seconds).

## iperf server lifecycle

The iperf-server.sh script:
1. Polls for `/sys/class/net/tun0` (up to 30s)
2. Polls for IP `10.0.10.1` to be assigned to `tun0` (up to 30s)
3. Starts 4 `iperf3 -s -D` daemons on ports 5201–5204, all bound to `10.0.10.1`
4. Sleeps forever

The iperf-server container has `cap_add: NET_ADMIN` because it modifies the
routing table in the server's network namespace. No manual return routes are
needed — the connected `/24` subnet on `tun0` provides automatic return-path
routing for all client IPs.

## Return routing

The server entrypoint assigns `10.0.10.1/24` to `tun0`, creating a connected
route for `10.0.10.0/24` via `tun0`. All client IPs (`10.0.10.11`–`10.0.10.14`)
are within this subnet, so server→client response traffic automatically routes
into `tun0` without any manual `/32` route injection.

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
