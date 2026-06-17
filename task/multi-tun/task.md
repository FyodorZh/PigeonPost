# Multiple Linux TUN Client Deployment Support

## Objective

Enable clean, supported deployment of multiple independent Linux TUN-based clients against one PigeonPost server using the new unified VPN subnet model.

This slice is no longer about fitting multi-client Linux support around the old point-to-point `/30` deployment behavior.

It is about making Linux TUN clients first-class participants in the same unified VPN address space that will later also serve endpoint/mobile clients.

## Scope

This slice covers:

- Linux client deployment artifacts
- shared provisioning behavior used by Docker client deploys
- Linux client addressing rules
- preservation of current runtime validation semantics
- operational documentation and verification

This slice does **not**:

- redesign the runtime protocol
- add authentication
- implement Android or iOS endpoint clients
- add dynamic IP allocation
- remove client-side policy-routing and source-NAT behavior

## Relationship To The Egress Work

This slice depends on the **preferred** server egress direction documented in:

- `task/egress/post-task.md`

That direction replaces the legacy `/30` deployment model with one unified multi-client-capable VPN subnet.

This slice assumes that the server side will expose one stable VPN client subnet and that Linux clients must be deployed inside it.

## Main Conclusion

The runtime already supports multiple Linux TUN clients.

The deployment system does not.

Today, the real client identity is the TUN IPv4 address. Current production client deployment artifacts still hard-code one identity: `10.0.0.2`.

That means the work required here is primarily deployment and operator-contract work, not protocol work.

## Current Baseline

This section describes the current state precisely and is the foundation for the replacement work.

### Runtime Identity Rules Today

The current Linux client identity is derived from the IPv4 address configured on its TUN interface.

Evidence:

- `src/PigeonPost/App/ClientApp.cs` resolves the TUN IPv4 during startup.
- `src/PigeonPost.Tun/TunIpv4AddressResolver.cs` requires exactly one IPv4 address on the TUN interface.
- that address is advertised during handshake and becomes the server-side session identity.

Important consequence:

- a Linux client's identity is not a logical label
- it is not `CLIENT_ID`
- it is the TUN IPv4 address

### `CLIENT_ID` Today

`CLIENT_ID` currently exists only in deployment script output and documentation.

It does not affect:

- handshake identity
- routing
- TUN addressing
- runtime session ownership

Important consequence:

- any real multi-client deployment support must revolve around explicit per-client TUN IP assignment

### Current Production Client Deploy Scheme

Current host pre-deploy (`deploy/client/pre-deploy.sh`):

1. Creates `tun0`.
2. Assigns `10.0.0.2/30`.
3. Adds route to `10.0.0.1/32` via `tun0`.
4. Enables IP forwarding.
5. Adds `POSTROUTING -o tun0 -j MASQUERADE`.
6. Creates `pp-ingress` ipset.
7. Adds a mangle `PREROUTING` rule marking traffic from registered source ranges.
8. Adds policy routing table `234`.
9. Adds default route through the tunnel in that table.
10. Adds `ip rule fwmark 1 table 234`.

Current plain deploy (`deploy/client/deploy-plain.sh`):

1. Publishes the app.
2. Creates `tun0` if needed.
3. Assigns `10.0.0.2/30`.
4. Adds route to `10.0.0.1/32`.
5. Runs the app.

Current Docker deploy:

- `deploy/client/docker/docker-compose.yml` hard-codes:
  - `TUN_NAME=tun0`
  - `TUN_IP=10.0.0.2`
  - `PEER_IP=10.0.0.1`
- `deploy/client/deploy-docker.sh` does not configure a real per-client tunnel identity

Important current behavior that must be preserved conceptually:

- selected ingress traffic is source-NATed to the client's own TUN IP before the app processes it
- that is why the current server's strict source-IP validation works

### Why The Current Deployment Is Not Multi-Client-Safe

Because all current production client deploy artifacts point at the same identity:

- `10.0.0.2`

If two Linux clients are deployed using the current ready-made production flow, they both try to own the same protocol identity.

That is not a minor inconvenience. It is a direct deployment-level identity collision.

### Test Harness Facts

The Docker integration harness proves runtime multi-client capability but not production deployment support.

Evidence:

- `deploy/test/docker/docker-compose.yml` uses client IPs:
  - `10.0.0.2`
  - `10.0.0.6`
  - `10.0.0.9`
  - `10.0.0.13`
- `deploy/test/docker/iperf-server.sh` manually adds extra `/32` host return routes for additional clients

What this proves:

- runtime can manage many clients

What this does not prove:

- that the supported production client deploy artifacts already provide a clean multi-client contract

## Problem Statement

Under the new preferred architecture, there should be one unified VPN subnet used by:

- Linux TUN clients
- future Android endpoint clients
- future iOS endpoint clients

To support multiple Linux TUN clients correctly inside that model, deployment must guarantee all of the following:

1. each client is assigned one unique TUN IPv4 from the unified VPN subnet
2. that TUN IPv4 is explicitly configurable in both plain and Docker deployments
3. each client still source-NATs selected ingress traffic to its own TUN IPv4 before the app processes it
4. the server sees packets whose source equals the client's advertised host IP
5. replies to each client IP return to that client only
6. documentation explains the address contract clearly and unambiguously

The current deployment scheme fails this because it still hard-codes one client identity and one narrow point-to-point deployment story.

## Target Unified Model

### Design Principle

Every Linux client must be deployed as one unique host inside the unified VPN subnet.

The subnet itself is defined by the server-side egress replacement work.

Example shape only:

- server tunnel address: `10.0.10.1/24`
- Linux client A: `10.0.10.11/24`
- Linux client B: `10.0.10.12/24`
- future Android endpoint A: `10.0.10.21`

Important properties:

- there is no preserved legacy `/30` production path
- there is one operator-visible address model for all clients
- each Linux client still owns exactly one host IP

### Runtime Compatibility

The current runtime rules remain valid under this model.

Inbound client-to-server:

- server still validates `packet source IP == client's advertised host IP`

Outbound server-to-client:

- server still routes by exact destination host IP

Linux clients remain compatible as long as their deploy flow preserves source NAT to their owned TUN IP.

## Ordered Implementation Plan

### Phase 1: Freeze The Linux Client Address Contract

#### Step 1.1: Bind Linux client deploys to the unified VPN subnet

Before changing scripts, freeze the server-provided VPN subnet contract from the egress replacement work.

Required outputs:

- the unified VPN subnet CIDR
- the server tunnel IP within that subnet
- the allowed client address range or allocation policy for Linux clients

Why:

- all Linux client deployment artifacts need a single stable network contract
- this removes the old ambiguity between legacy Linux addressing and future endpoint addressing

#### Step 1.2: Define the explicit client configuration interface

The new Linux client deploy model should stop deriving network shape from hidden defaults.

Recommended explicit variables:

- `TUN_NAME`
- `TUN_CIDR`
- `TUN_IP`
- `PEER_IP`
- `PIGEON_URL`

Why:

- `TUN_IP` is the real protocol identity
- `TUN_CIDR` makes the network model explicit instead of hiding `/30` assumptions
- `PEER_IP` clearly names the server tunnel address used as next hop

### Phase 2: Replace Linux Client Host Pre-Deploy Behavior

#### Step 2.1: Update `deploy/client/pre-deploy.sh`

Current limitations:

- hard-coded `tun0`
- hard-coded `10.0.0.2`
- hard-coded `/30`
- hard-coded peer `10.0.0.1`

Required changes:

1. accept explicit client network values rather than fixed ones
2. assign the configured `TUN_CIDR`
3. add the route to the configured `PEER_IP`
4. preserve existing behavior:
   - IP forwarding
   - `pp-ingress` ipset
   - mangle packet marking
   - `FORWARD` rules
   - policy table `234`
   - default route through the tunnel in table `234`
   - `POSTROUTING -o $TUN -j MASQUERADE`
5. keep all operations idempotent

Why this exact preservation matters:

- current Linux routing helpers are already aligned with the runtime source-validation model
- the deployment problem is the hard-coded identity, not the fundamental local routing pattern

#### Step 2.2: Preserve source-NAT semantics intentionally

The Linux client must continue rewriting selected ingress traffic so that packets entering PigeonPost appear to originate from the client's owned TUN IP.

This is required because the server runtime still enforces:

- `source IP == advertised host IP`

Therefore:

- removing client-side MASQUERADE is out of scope here
- changing the runtime validation rule is out of scope here

The correct move is to preserve those semantics and make them work for many clients by giving each client a unique TUN IP.

### Phase 3: Replace Linux Client Plain Deploy Behavior

#### Step 3.1: Update `deploy/client/deploy-plain.sh`

Current limitations:

- hard-coded `10.0.0.2`
- implicit `/30`
- hard-coded `10.0.0.1` peer

Required changes:

1. consume the explicit client configuration contract
2. provision the configured TUN address and peer route before starting the app
3. preserve idempotent provisioning behavior
4. stay equivalent to the final Docker deployment behavior

Acceptance for this step:

- two different Linux hosts can be deployed using different client TUN IPs from the unified VPN subnet and connect to the same server successfully

### Phase 4: Replace Linux Client Docker Deploy Behavior

#### Step 4.1: Update `deploy/client/docker/docker-compose.yml`

Current limitations:

- hard-coded `10.0.0.2`
- no explicit client CIDR variable
- hard-coded `10.0.0.1` peer

Required changes:

1. consume explicit network variables for the client identity
2. stop treating the old fixed values as defaults of the supported model
3. remain compatible with the shared provisioning entrypoint after it is generalized

#### Step 4.2: Update `deploy/client/deploy-docker.sh`

Current limitations:

- only sets `PIGEON_URL`
- exposes cosmetic `CLIENT_ID`

Required changes:

1. pass real network identity values into Docker deploy
2. de-emphasize or document `CLIENT_ID` as non-identity if it remains
3. make it clear in logs/output which TUN IP the client is actually using

Why:

- operators need to see the real identity contract, not a cosmetic label

### Phase 5: Align Shared Provisioning With The Unified Model

#### Step 5.1: Update `docker/docker-entrypoint.sh`

Current limitation:

- assigns `$TUN_IP/30` always

Required behavior:

1. assign the configured `TUN_CIDR` exactly
2. add the route to the configured `PEER_IP`
3. preserve exact/idempotent route checks
4. stop making `/30` the implicit client network shape

Why:

- the shared entrypoint is part of the production client deployment path for Docker
- if it keeps forcing `/30`, the unified model is not really implemented

### Phase 6: Validation Under The Unified Model

#### Step 6.1: Validate two real Linux clients under the new contract

The minimum supported proof should show:

1. client A is configured with unique IP `X`
2. client B is configured with unique IP `Y`
3. both connect to the same server
4. both are accepted as unique sessions
5. traffic routed through A exits with source `X`
6. traffic routed through B exits with source `Y`
7. replies to `X` go only to A
8. replies to `Y` go only to B

#### Step 6.2: Revisit the Docker integration harness

The current harness uses a layout shaped by the legacy `/30` provisioning model and manual route repair.

After the unified model is implemented, the harness should be reviewed and likely updated so that:

- tests reflect the real supported production topology
- route hacks specific to the old model are removed where practical

The desired end state is that test topology, deployment docs, and supported production model all describe the same network shape.

#### Step 6.3: Verify idempotency

For every touched Linux client deployment script, verify:

- first run succeeds
- second run succeeds
- no duplicate TUN addresses
- no duplicate routes
- no duplicate `ip rule` entries
- no duplicate `iptables` rules
- no duplicate ipset creation failures

This remains mandatory.

### Phase 7: Documentation And Operator Guidance

#### Step 7.1: Remove legacy Linux deployment framing

Docs should stop describing `10.0.0.2/30` as the normative Linux client identity model.

Why:

- that would preserve the exact ambiguity this slice is intended to remove

#### Step 7.2: Document the real Linux client identity rule

State explicitly:

- the client's protocol identity is its TUN IPv4 address
- `CLIENT_ID` is not handshake identity
- two clients must never share the same TUN IP

#### Step 7.3: Document the unified Linux deployment contract

The final docs must explain:

1. the unified VPN subnet
2. the server tunnel IP
3. how to choose a unique client IP
4. how plain deploy is parameterized
5. how Docker deploy is parameterized
6. why client-side MASQUERADE must remain
7. how server reply routing returns traffic to the correct client

#### Step 7.4: Document expected failure modes

At minimum:

- duplicate client IPs are deployment errors and are rejected during handshake
- wrong client IP or missing client-side MASQUERADE causes source-validation drops
- wrong server tunnel peer or route causes traffic blackholing
- cosmetic labels do not define client identity

## Expected Complexity Reduction

This slice should reduce complexity relative to the old world because it removes the mixed signals around client identity.

### Before

- runtime says multi-client by host IP
- client scripts say `10.0.0.2`
- `CLIENT_ID` suggests configurability that is not real
- test harness proves multi-client only through a workaround topology

### After

- one unified subnet model
- one client identity rule
- one clear per-client configuration contract
- one consistent explanation across runtime, deployment, docs, and tests

## Risks

### Risk 1: Partial migration leaves identity ambiguous

If some client artifacts still hard-code `10.0.0.2` while others use the unified model, deployment becomes more confusing than before.

Mitigation:

- treat this as a full replacement of the old Linux client deploy behavior, not a partial adjustment

### Risk 2: Source-NAT semantics are accidentally broken

If the client no longer rewrites selected ingress traffic to its own TUN IP, the server will reject packets.

Mitigation:

- preserve MASQUERADE behavior intentionally and test it explicitly

### Risk 3: Docs still over-emphasize `CLIENT_ID`

That would continue to mislead operators.

Mitigation:

- document clearly that TUN IPv4 is the true identity

## Acceptance Criteria

This slice is complete only when all of the following are true:

1. Linux clients are deployed only through the unified VPN subnet model.
2. The old `10.0.0.2/30` production client behavior is no longer the supported path.
3. Each Linux client is assigned one explicit unique TUN IP from the unified VPN subnet.
4. Client-side source-NAT and policy-routing semantics still ensure server-visible packet sources equal the owning client TUN IP.
5. Two or more Linux TUN clients can connect to one server cleanly without deployment-level route hacks.
6. Docker and plain-host client deploys remain equivalent.
7. All touched scripts remain idempotent.
8. Documentation clearly explains the true identity and address contract.

## Final Recommendation

This slice should be executed as the Linux-client side of the unified VPN subnet migration.

It should not try to preserve the legacy `/30` production client deployment behavior.

That old behavior is the source of the current ambiguity. The clean solution is to remove it and make Linux TUN clients use the same unified client-capable subnet model as the rest of the future VPN product.
