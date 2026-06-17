# Server Egress Slice

## Objective

Enable the existing PigeonPost server to act as reliable internet egress for future full-device Android endpoint clients while preserving the current protocol and without casually breaking the current Linux TUN-based deployment model.

The endpoint client will:

- use the same Pontifex transport and handshake model as the current TUN client
- advertise exactly one IPv4 host address
- send raw IPv4 packets
- expect full-device default-route VPN behavior on Android

This slice is server-side and deployment-side only. It does not implement Android, `PigeonPost.EndPoint`, or new authentication.

## Why This Slice Exists

The current server can already forward and NAT traffic from the tunnel to the internet, but the deployment scheme is hard-coded for the current Linux site-to-site model:

- one server TUN address: `10.0.0.1/30`
- one default client peer route: `10.0.0.2/32`
- NAT only for source subnet `10.0.0.0/30`

That is sufficient for the current deployment scripts and the current Linux client pre-deploy flow, but it is not a correct foundation for endpoint clients that will each own one host IP inside a larger pool.

## Current Baseline

This section is intentionally precise. The implementation plan below must preserve the important properties of this baseline.

### Runtime Behavior

Current server runtime:

- `src/PigeonPost/App/ServerApp.cs` opens exactly one Linux TUN device and passes it to `ServerSideLogic`.
- `src/PigeonPost.Bridge/Logic/ServerSideLogic.cs` creates a `ServerHub`, wires `BridgeImpl` so that packets read from the server TUN are routed by `ServerHub.OnPacketFromTun`, and accepts clients over Pontifex.
- `src/PigeonPost.Bridge/Server/ServerHub.cs` stores sessions keyed by advertised IPv4 host address.
- Outbound server-to-client routing is exact-host only: packet destination IP must equal one registered client host IP.
- Inbound client-to-server validation is strict: packet source IP must equal the session's advertised host IP.
- There is no fallback route and no subnet ownership. Routing is one host, one session.

Relevant invariants already enforced in code and tests:

- `ServerHub.OnPacketFromClient` only accepts packets whose source equals the advertised host IP.
- `ServerHub.OnPacketFromTun` only forwards packets whose destination exactly matches a registered client host IP.
- `tests/PigeonPost.Bridge.Tests/Server/InboundSourceValidationTests.cs` and `ServerHubRealValidationTests.cs` confirm the strict source-IP rule.
- `tests/PigeonPost.Bridge.Tests/Server/ServerHubRealRoutingTests.cs` confirms the exact-host routing rule.

### Production Server Deploy Scheme Today

Current host pre-deploy on the server machine (`deploy/server/pre-deploy.sh`):

1. Expects a WAN interface argument.
2. Creates `tun0` if missing.
3. Assigns `10.0.0.1/30` to `tun0`.
4. Brings `tun0` up.
5. Adds a host route for `10.0.0.2/32` via `tun0`.
6. Enables `net.ipv4.ip_forward=1`.
7. Adds `iptables -t nat POSTROUTING -o $WAN_IF -s 10.0.0.0/30 -j MASQUERADE`.
8. Adds `FORWARD` allow rules for source subnet `10.0.0.0/30` from `tun0` to WAN and return `RELATED,ESTABLISHED` traffic back from WAN to `tun0`.

Current plain server deploy (`deploy/server/deploy-plain.sh`):

1. Publishes the console app.
2. Creates `tun0` if needed.
3. Assigns `10.0.0.1/30`.
4. Adds `10.0.0.2/32` via `tun0`.
5. Runs `PigeonPost.dll --role server --tun tun0 --url 'tcp|0.0.0.0:9000/30'`.

Current Docker server deploy:

- `deploy/server/deploy-docker.sh` builds and runs `deploy/server/docker/docker-compose.yml`.
- Compose uses `network_mode: host`, `NET_ADMIN`, and `/dev/net/tun`.
- Environment passed to the shared entrypoint:
  - `TUN_NAME=tun0`
  - `TUN_IP=10.0.0.1`
  - `PEER_IP=10.0.0.2`
- The shared `docker/docker-entrypoint.sh`:
  - creates the TUN device if needed
  - assigns `$TUN_IP/30`
  - brings the TUN up
  - adds one route to `$PEER_IP/32`
  - executes the app

### Production Client Deploy Scheme Today

This matters because the server changes must not accidentally destroy the current Linux client model.

Current client host pre-deploy (`deploy/client/pre-deploy.sh`):

1. Creates `tun0` if missing.
2. Assigns `10.0.0.2/30`.
3. Adds route to `10.0.0.1/32` via `tun0`.
4. Enables IP forwarding.
5. Adds `POSTROUTING -o tun0 -j MASQUERADE` so marked ingress traffic exits through the tunnel with source rewritten to `10.0.0.2`.
6. Creates `pp-ingress` ipset.
7. Adds mangle `PREROUTING` rule marking packets whose source is in `pp-ingress` with `fwmark 1`.
8. Adds policy routing table `234` with default route via `10.0.0.1 dev tun0`.
9. Adds `ip rule fwmark 1 table 234`.

Client ingress helper scripts register source subnets into `pp-ingress`, not destination routes.

Important consequence:

- The current Linux client does not expose arbitrary original source IPs to the server.
- Marked traffic is source-NATed to the client's own TUN IP before it reaches PigeonPost.
- That is why the current source-IP validation rule works.

### Test Harness Evidence of the Limitation

The Docker integration test explicitly demonstrates that the shared deployment scheme is still centered on one peer route and `/30` assumptions:

- `deploy/test/docker/docker-compose.yml` starts one server and four clients.
- Server still gets `TUN_IP=10.0.0.1` and `PEER_IP=10.0.0.2`.
- Additional client identities are `10.0.0.6`, `10.0.0.9`, and `10.0.0.13`.
- `deploy/test/docker/iperf-server.sh` manually injects extra `/32` host routes for those addresses via `tun0` because the shared entrypoint only adds one peer route.

This is important evidence:

- the runtime can handle many host identities
- the current deployment scripts cannot represent a real multi-host tunnel design cleanly

## Problem Statement

For Android full-device VPN in V1, each endpoint client will own one advertised IPv4 host address, and the Android VPN will default-route device traffic through the tunnel.

Server-side egress will only work if all of the following are true:

1. The source IP used by endpoint packets is accepted by `ServerHub` as the client's advertised host IP.
2. The server host forwards those packets to the WAN.
3. The server host NATs those packets when leaving the WAN interface.
4. Return traffic is de-NATed back to the endpoint client's private IPv4.
5. The Linux routing table sends the de-NATed return packet into `tun0`.
6. PigeonPost reads that packet from `tun0` and exact-host routes it to the matching client session.

The current deployment fails this as a general endpoint design because:

- NAT is hard-coded to `10.0.0.0/30`.
- The shared entrypoint only knows how to assign one `/30` address and one peer route.
- The server host only automatically routes `10.0.0.2/32` plus whatever connected route the `/30` creates.
- There is no dedicated endpoint address pool.

## Design Direction For This Slice

Do not replace the current Linux site-to-site `/30` scheme outright.

Instead:

1. Keep the current legacy server addressing path intact for existing Linux TUN deployments.
2. Add a second, dedicated connected subnet on the server TUN for endpoint clients.
3. NAT and forward that endpoint subnet to the WAN.
4. Keep the protocol unchanged: one endpoint client still advertises exactly one host IP.

### Recommended Topology

Primary legacy server TUN address remains:

- `10.0.0.1/30`

Add a secondary connected endpoint subnet on the same server TUN device:

- example only: `10.0.10.1/24` on `tun0`
- endpoint clients later receive one host address from `10.0.10.0/24`, for example `10.0.10.23`

Why this design is preferred:

- it does not force immediate changes to the current Linux client pre-deploy flow
- it keeps server runtime protocol unchanged
- it gives Linux a connected route for the endpoint pool via `tun0`
- replies de-NATed to endpoint client IPs automatically route back into `tun0`
- it avoids continuing the brittle "one explicit peer route per client" pattern for endpoint clients

## Non-Goals

This slice does not:

- implement Android `VpnService`
- implement `PigeonPost.EndPoint`
- solve client IP allocation policy in the endpoint client
- add authentication
- change the handshake format
- broaden runtime routing from exact-host to subnet ownership

## Ordered Implementation Plan

### Phase 1: Freeze The Configuration Contract

#### Step 1.1: Define the endpoint egress subnet contract

Decide and document the new server-side endpoint subnet variables.

Minimum required values:

- endpoint subnet CIDR, for example `10.0.10.0/24`
- server endpoint TUN IP, for example `10.0.10.1`

Requirements:

- must not overlap the current legacy `/30`
- must not overlap common LAN ranges used around deployment targets
- must be large enough for multiple endpoint clients

Why:

- all later shell, Docker, and verification logic depends on a stable subnet contract
- Android endpoint work later must know which address space is valid

Deliverable:

- a documented server-side endpoint subnet choice or a documented configurable variable set

#### Step 1.2: Decide how configuration is represented in scripts

Do not keep relying on the current `TUN_IP + PEER_IP` only model.

Recommended approach:

- keep existing primary variables for the legacy path
- add optional variables for extra connected TUN addresses and optional extra host routes

One acceptable shape:

- existing:
  - `TUN_NAME`
  - `TUN_IP`
  - `PEER_IP`
- new optional:
  - `EXTRA_TUN_CIDRS`
  - `EXTRA_TUN_HOST_ROUTES`

Example server meaning:

- `TUN_IP=10.0.0.1`
- `PEER_IP=10.0.0.2`
- `EXTRA_TUN_CIDRS=10.0.10.1/24`

Why:

- preserves current behavior when extras are absent
- allows the shared entrypoint to remain shared
- avoids forcing the Linux client scripts into endpoint-specific complexity

### Phase 2: Generalize TUN Provisioning Helpers

#### Step 2.1: Update `docker/docker-entrypoint.sh`

Current limitations:

- always assigns `$TUN_IP/30`
- adds only `$PEER_IP/32`

Required changes:

1. Keep current default behavior when only `TUN_IP` and `PEER_IP` are provided.
2. Add support for one or more extra TUN CIDRs.
3. Add support for zero or more extra host routes.
4. Keep every operation idempotent.

Implementation expectations:

- do not remove the current primary `/30` path
- add guarded loops for extra addresses and routes
- make route checks exact enough to avoid substring bugs

Why this must happen before deploy script updates:

- both Docker and host flows must stay behaviorally equivalent
- the entrypoint is the shared runtime path for container deployments

Acceptance for this step:

- current Docker deploy still provisions the legacy `/30`
- new server Docker deploy can additionally provision the endpoint subnet on `tun0`

#### Step 2.2: Mirror the same capability in host-side deploy scripts

Update host-side plain deploy logic so it can provision the same addresses and routes as the Docker entrypoint.

Files affected conceptually:

- `deploy/server/deploy-plain.sh`
- possibly shared helper extraction if you decide to factor shell logic later

Why:

- repo rules require Docker and plain deployment methods to remain equivalent

### Phase 3: Expand Server Host Networking For Endpoint Egress

#### Step 3.1: Update `deploy/server/pre-deploy.sh`

Current script only supports:

- `10.0.0.1/30`
- one peer route `10.0.0.2/32`
- NAT from `10.0.0.0/30`

Required additions:

1. Keep the current legacy `/30` provisioning intact.
2. Add the endpoint subnet address to `tun0` as a second connected address.
3. Add NAT for the endpoint subnet.
4. Add outbound `FORWARD` allow rule for endpoint-subnet source traffic.
5. Keep the existing inbound `RELATED,ESTABLISHED` rule.
6. Keep everything idempotent.

Important detail:

- do not rely on explicit per-endpoint `/32` routes for the endpoint pool
- the connected route created by the secondary TUN CIDR should be the return-path mechanism

Why:

- explicit `/32` route management does not scale for mobile endpoints
- a connected endpoint subnet is the simplest correct Linux routing model for de-NATed replies

Expected rule shape after the change:

- existing legacy NAT rule remains
- new additional NAT rule for endpoint subnet remains separate and explicit
- existing legacy forward rule remains
- new additional forward rule for endpoint subnet is added separately

Do not silently broaden the legacy rule to an oversized supernet unless there is a very strong reason and it is documented. Separate explicit rules are easier to audit.

#### Step 3.2: Validate route behavior on the server host

After the secondary endpoint address exists on `tun0`, Linux must have a connected route for the endpoint subnet via `tun0`.

Verify explicitly:

- `ip addr show dev tun0`
- `ip route show table main`
- `ip route get <sample-endpoint-ip>` resolves to `dev tun0`

Why:

- this is the core mechanism that gets internet replies back into PigeonPost for exact-host delivery

### Phase 4: Preserve Existing Linux Client Behavior

#### Step 4.1: Ensure the legacy client pre-deploy flow still works unchanged

The current Linux client behavior depends on:

- TUN `10.0.0.2/30`
- peer route to `10.0.0.1`
- local source NAT to `10.0.0.2`
- policy routing from `pp-ingress`

This slice should not require endpoint-specific changes in:

- `deploy/client/pre-deploy.sh`
- `deploy/client/deploy-plain.sh`
- `deploy/client/deploy-docker.sh`
- ingress scripts

Why:

- the user explicitly wants to avoid modifying the old client unless strongly required
- the proposed secondary endpoint subnet on the server avoids this requirement

Acceptance for this step:

- current client docs and behavior remain valid
- existing Linux client deployments still route through the server exactly as before

### Phase 5: Add Verification Assets

#### Step 5.1: Add server-egress documentation-driven verification procedure

At minimum, define a reproducible verification sequence for both plain and Docker server deploys.

The verification must prove:

1. server `tun0` has both the legacy address and the endpoint connected address
2. server has NAT for both source ranges
3. server forwards endpoint-subnet packets to the WAN
4. server routes replies for endpoint-subnet addresses back to `tun0`
5. running setup twice produces no duplicate rules or failures

Because Android is not implemented yet, this phase needs an intermediate proof strategy.

Preferred test direction:

- create a synthetic endpoint integration harness that connects to the existing server protocol with one advertised endpoint IP from the endpoint subnet
- sends raw IPv4 packets to a controlled egress target
- asserts that replies return through the server and are delivered back to the same client session

If that is too large for this slice, define and automate as much of the network verification as possible and record the remaining end-to-end proof as a dependency for the first Android vertical slice.

#### Step 5.2: Re-run existing integration coverage

At least re-verify:

- current server/client Docker integration behavior
- existing server runtime tests
- any deployment behavior that relies on the current `/30`

Why:

- the server egress slice must expand capability, not regress the current product

### Phase 6: Document The New Deployment Model

#### Step 6.1: Update deployment docs and script usage text

Document:

- what the legacy `/30` is for
- what the endpoint subnet is for
- which rules NAT each subnet
- why replies for endpoint addresses return to `tun0`
- that endpoint clients must use one unique host IP from the endpoint subnet

#### Step 6.2: Document idempotency expectations

For every touched script, explicitly verify and document:

- first run succeeds
- second run succeeds
- no duplicate `iptables` rules
- no duplicate addresses or routes

## Risks And Design Notes

### Risk 1: Mixing legacy and endpoint address spaces incorrectly

If the endpoint pool overlaps the legacy `/30` or some real LAN range, routing and debugging will become confusing quickly.

Mitigation:

- choose a dedicated endpoint subnet and document it clearly

### Risk 2: Over-generalizing the shared entrypoint in a fragile way

The shared entrypoint is used by both server and client roles.

Mitigation:

- keep existing behavior as the default path
- make extra behavior opt-in and additive

### Risk 3: Forgetting deployment equivalence

Docker and plain-host server deployment must remain equivalent.

Mitigation:

- every networking change must be mirrored in both flows

### Risk 4: Mistaking host routing validation for full end-to-end proof

Server-side NAT and routing can be correct while endpoint runtime is still wrong.

Mitigation:

- treat this slice as a prerequisite for Android, not the final Android proof
- add a synthetic endpoint integration harness as early as practical

## Acceptance Criteria

This slice is complete only when all of the following are true:

1. The server can be provisioned with both:
   - the current legacy site-to-site `/30`
   - a second connected endpoint subnet on the same TUN device
2. The server host NATs both the legacy subnet and the endpoint subnet to the WAN.
3. The server host forwards endpoint-subnet packets from `tun0` to WAN and accepts established return traffic back.
4. Linux routes endpoint-subnet replies back to `tun0` via the connected route.
5. Existing Linux client deployment behavior still works.
6. Docker and plain-host server deployment remain equivalent.
7. All touched scripts are idempotent.
8. The new behavior is documented clearly enough that Android endpoint work can depend on it without rediscovering the network model.

## Dependencies For Later Slices

This slice intentionally leaves these questions to later work:

- how `PigeonPost.EndPoint` picks and persists one client IP from the endpoint pool
- how duplicate endpoint IP claims are avoided operationally
- how Android `VpnService` injects the assigned client IP and default route
- how DNS is configured for full-device VPN clients

Those later slices should consume the network contract produced here instead of redefining it.
