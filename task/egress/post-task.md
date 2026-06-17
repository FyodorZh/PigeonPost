# Post-Task: Replace Legacy `/30` Deployment With A Unified VPN Subnet Model

## Purpose

This document describes the follow-up direction after the original `Server Egress Slice`.

The original slice was intentionally conservative:

- keep the current legacy `/30` Linux TUN deployment behavior
- add a second subnet for future endpoint clients

After further analysis, there is a cleaner long-term direction:

- remove the old single-peer `/30` deployment behavior completely
- replace it with one unified multi-client-capable subnet model
- use that same model for:
  - Linux TUN clients
  - future Android endpoint clients
  - future iOS endpoint clients

This document explains why that replacement is better, what exactly changes, and what work is required.

## Main Conclusion

There is no hard runtime reason to keep the current point-to-point `/30` deployment shape.

The current runtime model already works with:

- one client == one advertised host IPv4
- strict source-IP validation
- exact-host routing back to the client

Those runtime rules do **not** require:

- `10.0.0.1/30`
- `10.0.0.2/30`
- one peer route
- `/30`-specific deployment scripts

The `/30` is a deployment choice, not a protocol invariant.

Because of that, a full replacement of the old deployment behavior is possible and preferable.

## Why Full Replacement Is Better

The additive compatibility plan was originally chosen to minimize migration risk. It was not chosen because the old behavior was fundamentally necessary.

Now that both the server egress problem and the multi-client Linux deployment problem have been analyzed, the cleaner approach is to replace the old behavior entirely.

### Benefit 1: One network model instead of two

If the old `/30` path remains, the product has to support and explain two deployment models:

1. legacy Linux site-to-site `/30`
2. new endpoint-capable subnet model

That increases complexity in:

- documentation
- shell scripts
- Docker configuration
- operator understanding
- testing
- troubleshooting

With full replacement there is only one model.

### Benefit 2: Simpler server networking

Current conservative plan required:

- one legacy `/30`
- one endpoint subnet
- two NAT rule families
- two conceptual address spaces
- preserving older routing assumptions alongside new ones

With replacement:

- one client-capable subnet on the server TUN
- one NAT rule for that subnet
- one FORWARD model
- one return-path model

This is materially simpler.

### Benefit 3: Simpler client deployment

If the old model remains, the Linux client deployment story stays split:

- old default scripts for `10.0.0.2/30`
- new client IP model for actual multi-client support

That creates hidden traps:

- operators may keep deploying duplicate `10.0.0.2` clients
- `CLIENT_ID` may continue to be misunderstood as real identity
- docs must explain why the old examples are not the real future model

With replacement:

- every client must use one unique IP from the same documented pool
- there is no legacy exception

### Benefit 4: Better fit for Android and future platforms

Android endpoint clients will need:

- one owned private IPv4 address
- default-route VPN behavior
- server-side internet egress

Linux multi-client support needs:

- one owned private IPv4 address per client
- server-side return routing
- source-NAT to the owned IP before the packet reaches the server runtime

These are already compatible.

There is no strong value in keeping a separate `/30` Linux-only deployment universe.

### Benefit 5: Lower long-term maintenance burden

Two deployment models mean:

- more conditionals in scripts
- more examples to maintain
- more room for partial regressions
- more confusing test coverage

One deployment model means:

- fewer branches
- fewer shell variables with role-specific meaning
- easier onboarding
- easier testing

## What Is Being Replaced

The old deployment behavior to be removed is:

### On the server

- fixed TUN address `10.0.0.1/30`
- fixed peer route `10.0.0.2/32`
- NAT only for `10.0.0.0/30`
- script logic that assumes one primary peer route is the deployment norm

### On Linux clients

- hard-coded client identity `10.0.0.2`
- implicit `/30` address assumption
- deployment examples built around one point-to-point pair only

### In shared provisioning

- shared entrypoint assumption that `TUN_IP/30` is always the correct address shape
- one-peer provisioning as the core default deployment concept

## Target Unified Model

## Design Principle

Use one subnet for all VPN-capable clients.

That subnet should be:

- large enough for many clients
- connected to the server TUN interface
- NATed to WAN by the server
- used as the source identity space for both Linux TUN clients and future endpoint clients

## Example Topology

The exact subnet value can be finalized separately, but the model should look like this:

- server TUN interface has one address in a client-capable subnet
  - example: `10.0.10.1/24`
- Linux client A uses `10.0.10.11`
- Linux client B uses `10.0.10.12`
- future Android endpoint A uses `10.0.10.21`
- future Android endpoint B uses `10.0.10.22`

Key properties:

- all clients are first-class members of one subnet contract
- every client owns one unique host IP
- server routes replies into `tun0` via the connected route for the subnet
- server runtime exact-host routes the packet to the right session

## Runtime Compatibility With Unified Model

The current runtime remains valid under the unified model.

### Inbound client-to-server traffic

Current rule:

- source IP must equal advertised host IP

Still valid under unified model because:

- each Linux client continues to source-NAT routed traffic to its own TUN IP
- each future endpoint client will also own one client IP identity

### Outbound server-to-client traffic

Current rule:

- destination IP must exactly match a registered client host IP

Still valid under unified model because:

- replies to `10.0.10.11` go to client A
- replies to `10.0.10.12` go to client B
- exact-host routing remains correct and simple

## Work Required To Replace The Old Behavior

This section is ordered intentionally. The goal is not only to change the behavior, but to do it coherently.

### Phase 1: Freeze The New Single Subnet Contract

#### Step 1.1: Choose the unified VPN client subnet

A single subnet must be chosen and documented.

Requirements:

- must not overlap expected LAN spaces used by deployment targets
- must be large enough for many Linux and future endpoint clients
- must be stable enough to appear in docs, examples, and tests

Minimum outputs:

- client subnet CIDR
- server TUN address within that subnet

Why this must happen first:

- all later deploy scripts, examples, and tests need one stable network contract

#### Step 1.2: Define explicit configuration variables

The new model must stop relying on hidden `/30` assumptions.

Recommended normalized variables:

- `TUN_NAME`
- `TUN_CIDR`
- `TUN_IP`
- `PEER_IP`
- `PIGEON_URL`

Important note:

- `PEER_IP` should now mean "server tunnel address used as next-hop/gateway" rather than "legacy single peer in a `/30` pair"

Why:

- the meaning of the network contract needs to be explicit and future-proof

### Phase 2: Replace Server Provisioning Logic

#### Step 2.1: Remove server dependence on the legacy `/30`

Update server host setup so that the server TUN is provisioned only with the new unified subnet address.

Required changes:

- stop assigning `10.0.0.1/30`
- stop treating `10.0.0.2/32` as the default peer route to preserve
- stop NATing only `10.0.0.0/30`

New behavior:

- assign the server TUN address from the unified subnet
- rely on the connected subnet route via `tun0`
- NAT the full unified subnet out to WAN
- add FORWARD rules for the unified subnet

Why:

- this is the core simplification that removes dual behavior

#### Step 2.2: Update plain server deploy

`deploy/server/deploy-plain.sh` must stop provisioning the legacy `/30` and instead provision the unified subnet address.

Requirements:

- mirror the final Docker behavior exactly
- preserve idempotency
- keep CLI invocation unchanged except for networking assumptions

#### Step 2.3: Update Docker server deploy and shared entrypoint

`deploy/server/docker/docker-compose.yml` and `docker/docker-entrypoint.sh` must stop centering server provisioning around `TUN_IP/30` plus one peer route as the normative server behavior.

New expectations:

- entrypoint assigns the configured `TUN_CIDR`
- server deploy passes the server's unified subnet address
- peer-route logic is no longer the core model for supporting multiple clients

Important nuance:

- a route to a specific gateway or server IP may still exist where useful
- but explicit `/32` peer-route provisioning must stop being the conceptual foundation of deployment

### Phase 3: Replace Linux Client Provisioning Logic

#### Step 3.1: Make Linux client identity fully configurable

Update `deploy/client/pre-deploy.sh` so it no longer hard-codes:

- `10.0.0.2`
- `/30`
- old point-to-point assumptions

New expectations:

- each client is assigned one configured IP from the unified client subnet
- the TUN address assignment uses the explicit configured CIDR
- the route to the server/gateway uses the configured `PEER_IP`

Why:

- under the unified model, client identity is the client TUN IP
- the deploy system must make that identity explicit and operator-controlled

#### Step 3.2: Preserve client-side NAT and policy routing semantics

Do not remove the current Linux client behavior where policy-routed ingress traffic is source-NATed to the client's TUN IP before reaching PigeonPost.

This is still required because the server runtime still enforces:

- packet source IP must equal that client's advertised host IP

So the new client deploy behavior must preserve:

- `pp-ingress` ipset
- packet marking rule
- table `234`
- default route through the tunnel in that table
- MASQUERADE out of the TUN device

Why:

- full deploy replacement should simplify addressing, not change validated runtime semantics unless there is a very good reason

#### Step 3.3: Replace plain client deploy assumptions

`deploy/client/deploy-plain.sh` must:

- stop using hard-coded `10.0.0.2`
- stop assigning implicit `/30`
- accept or derive explicit client network values from the new configuration contract
- remain behaviorally equivalent to Docker deploy

#### Step 3.4: Replace Docker client deploy assumptions

`deploy/client/docker/docker-compose.yml` and `deploy/client/deploy-docker.sh` must:

- stop hard-coding `10.0.0.2`
- stop making `CLIENT_ID` appear more important than the actual TUN IP
- consume real network configuration values for the client identity

If `CLIENT_ID` remains at all, it should be documented as a label only unless it gains real runtime meaning later.

### Phase 4: Simplify Documentation And Examples

#### Step 4.1: Remove legacy `/30` examples from primary docs

Once replacement is chosen, the docs should stop teaching the old deployment model as if it were still current.

That includes:

- AGENTS/docs/examples that show server `10.0.0.1/30` and client `10.0.0.2/30` as the normative deployment contract
- any text that suggests one peer route is the intended production model

Why:

- leaving old examples in place would preserve the same ambiguity the replacement is meant to eliminate

#### Step 4.2: Rewrite deployment explanation around one subnet

The new documentation should explain simply:

1. server owns one VPN subnet on `tun0`
2. every client gets one unique host IP from that subnet
3. Linux clients NAT routed ingress traffic to their own TUN IP
4. server NATs the subnet to WAN
5. replies come back to the owning client by exact-host routing

That story is much easier to understand than the old dual-path model.

### Phase 5: Rework Validation Around The New Single Model

#### Step 5.1: Re-verify server egress under the unified subnet

The new server validation must prove:

- server TUN has the unified subnet address
- server NATs the unified client subnet to WAN
- replies to any client IP in that subnet are routed into `tun0`

#### Step 5.2: Re-verify two or more Linux clients under the unified subnet

Validation must prove:

1. client A with IP `X` can connect
2. client B with IP `Y` can connect
3. each client's forwarded traffic is source-NATed to its own TUN IP
4. replies to `X` go only to A
5. replies to `Y` go only to B

#### Step 5.3: Revisit Docker integration harness

The current Docker integration test is shaped around the old `/30` assumptions and manual route injection.

After full replacement, that harness should be reviewed and likely rewritten to reflect the real supported deployment model.

Desired end state:

- integration tests should demonstrate the same topology the docs claim is production-supported
- route-injection hacks tied to the old deployment shape should disappear where possible

### Phase 6: Migration Discipline

#### Step 6.1: Treat this as a true migration, not an additive feature

Because the old deployment behavior is being removed, this work must be tracked as a migration.

Required migration outputs:

- explicit statement of which old assumptions are obsolete
- updated deployment instructions for existing Linux users
- examples for assigning unique client IPs in the new subnet

#### Step 6.2: Verify script idempotency after the replacement

For every touched script, confirm:

- first run succeeds
- second run succeeds
- no duplicate routes
- no duplicate iptables rules
- no duplicate `ip rule` entries
- no duplicate TUN addresses

This remains mandatory.

## Expected Complexity Reduction

This replacement should reduce complexity in several concrete ways.

### Before

- legacy `/30` model
- separate endpoint/client-capable subnet model
- two conceptual address systems
- server scripts carrying old and new behavior
- client docs split between legacy defaults and real multi-client needs

### After

- one subnet model
- one identity rule for all clients
- one server NAT model
- one return-path model
- one set of operator instructions

This is the core reason for choosing replacement now.

## Risks Of Full Replacement

### Risk 1: Wider immediate blast radius

This is a broader change than the conservative additive path.

Affected areas include:

- server deploy scripts
- client deploy scripts
- shared Docker entrypoint
- test harness
- deployment docs

Mitigation:

- treat this as a coordinated migration slice
- re-verify both server egress and Linux multi-client behavior before declaring success

### Risk 2: Existing Linux operators must update their configuration

Anyone depending on the old `/30` assumptions will need to move to the unified model.

Mitigation:

- provide clear migration documentation
- provide example assignments for multiple clients

### Risk 3: Partial migration creates more confusion than before

If only half the scripts are updated, the project becomes harder to understand than it is now.

Mitigation:

- do not land this as a partial conceptual migration
- ensure docs, scripts, and tests all move together

## Acceptance Criteria

This replacement is complete only when all of the following are true:

1. The old legacy `/30` deployment behavior is no longer the supported production model.
2. The server is deployed using one unified client-capable subnet on `tun0`.
3. Linux clients are deployed using unique host IPs from that same subnet.
4. Client-side policy routing and source-NAT semantics still produce server-visible packet sources equal to the owning client's TUN IP.
5. The server NATs the unified subnet to WAN and routes replies back into `tun0`.
6. Two or more Linux TUN clients can be deployed cleanly without ad hoc route hacks.
7. Docker and plain-host deployment remain equivalent.
8. Primary docs and examples explain only the unified model as the current behavior.
9. All touched scripts remain idempotent.

## Final Recommendation

The unified replacement model is now the cleaner long-term direction.

It should replace, not coexist with, the legacy `/30` deployment behavior.

Reasons:

- lower conceptual complexity
- simpler scripts
- cleaner docs
- better fit for multi-client Linux and future endpoint/mobile clients
- no need to maintain an increasingly misleading legacy deployment path

In short:

- the conservative additive plan was safe
- the unified replacement plan is better
