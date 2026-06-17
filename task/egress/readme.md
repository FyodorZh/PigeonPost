# Server Egress Problem

## What This Problem Is About

PigeonPost already knows how to move raw IPv4 packets between a server and a client. The next product direction is different from the current Linux-to-Linux tunnel model:

- the future Android client will not use the current Linux TUN setup scripts
- it will still use the same transport protocol and the same "one client equals one advertised IPv4 host" identity model
- it will act as a full-device VPN, which means normal device traffic will go through PigeonPost

For that to work, the server must do more than just accept packets from the client. The server must become the internet exit point for those packets.

That is what "server egress" means here.

## The Current Deployment Model

Today PigeonPost is built around a Linux site-to-site shape.

### Server side today

The server host is prepared like this:

1. Create `tun0`
2. Assign `10.0.0.1/30`
3. Add route `10.0.0.2/32` via `tun0`
4. Enable IP forwarding
5. NAT source subnet `10.0.0.0/30` out to the WAN interface
6. Allow forwarded traffic from that tunnel subnet out to WAN

This is implemented in `deploy/server/pre-deploy.sh`.

The server Docker path follows the same model:

- `deploy/server/docker/docker-compose.yml`
- `docker/docker-entrypoint.sh`

The shared entrypoint assigns one `/30` address and adds one peer route.

### Client side today

The current Linux client is prepared like this:

1. Create `tun0`
2. Assign `10.0.0.2/30`
3. Add route to `10.0.0.1/32`
4. Enable forwarding
5. Mark selected ingress traffic
6. Policy-route that marked traffic into the tunnel
7. Source-NAT it to `10.0.0.2`

This is implemented in `deploy/client/pre-deploy.sh` and the ingress scripts.

That source NAT matters a lot. It means the server does not see arbitrary LAN IPs from behind the client. It sees traffic whose source has already been rewritten to the client's TUN IP.

## Why The Current Runtime Rules Matter

The current server runtime is strict by design.

For traffic coming from the client to the server:

- the packet must be valid IPv4
- the source IP must equal that client's advertised host IP

For traffic going from the server TUN back to a client:

- the destination IP must exactly match one connected client host IP

This means the runtime already has the right idea for endpoint clients:

- one client owns one IP
- replies to that IP go back to that client

The runtime is not the main problem.

The main problem is the host networking and deployment scheme around it.

## Why The Current Implementation Is Not Good Enough

The current deployment is too narrow for endpoint clients.

### Problem 1: NAT is limited to one tiny `/30`

The server only NATs traffic whose source is inside `10.0.0.0/30`.

That works for the current site-to-site model, because the current Linux client rewrites ingress traffic to `10.0.0.2` before sending it through PigeonPost.

It does not provide a clean address space for many endpoint clients.

### Problem 2: The shared deployment logic only understands one peer

The shared entrypoint knows how to:

- assign one address
- add one route to one peer IP

That is fine for one point-to-point pair. It is not a real multi-endpoint network model.

The test harness already proves this limitation. In Docker integration tests, extra return routes have to be added manually for additional client IPs because the shared server setup only installs the first peer route.

### Problem 3: There is no dedicated endpoint address pool

Future Android endpoint clients need one private IPv4 address each.

The current deployment does not define:

- a dedicated endpoint subnet
- how the server routes that endpoint subnet back into `tun0`
- how the server NATs that endpoint subnet to the WAN

### Problem 4: Full-device VPN needs default-route behavior

In the future Android design, the device will send ordinary internet traffic through the VPN.

That means traffic like this must work:

- source: one endpoint client IP
- destination: any internet IP

The server must forward it to the internet and route the reply back through PigeonPost.

The current deployment only partially supports that idea, and only for the current very narrow `/30` setup.

## What We Need Instead

We do not want to throw away the existing Linux client model casually.

The better approach is:

1. keep the current legacy `/30` path for existing Linux client deployments
2. add a second connected subnet on the server TUN specifically for endpoint clients
3. NAT that second subnet to the WAN
4. let Linux route replies for that subnet back into `tun0`

## The Proposed Fix

### Keep the legacy path

The current site-to-site deployment should continue to exist:

- server still has `10.0.0.1/30`
- legacy Linux client can still use `10.0.0.2`

This avoids unnecessary disruption.

### Add an endpoint subnet on the server TUN

Add a second address on `tun0`, for example:

- `10.0.10.1/24`

This creates a connected route for the entire endpoint subnet through `tun0`.

Then future endpoint clients can each own one address from that subnet, for example:

- `10.0.10.23`
- `10.0.10.24`
- `10.0.10.25`

### NAT the endpoint subnet to the WAN

Add a separate NAT rule for the endpoint subnet.

When an endpoint client sends internet traffic:

1. the packet arrives through PigeonPost with source `10.0.10.23`
2. the server injects it into the host stack via `tun0`
3. Linux forwards it out the WAN interface
4. Linux NATs it to the server's public address

When the reply comes back:

1. Linux de-NATs it back to `10.0.10.23`
2. because `10.0.10.0/24` is connected to `tun0`, Linux routes it to `tun0`
3. PigeonPost reads it from `tun0`
4. PigeonPost sees destination `10.0.10.23`
5. PigeonPost exact-host routes it to the session that advertised `10.0.10.23`

That is exactly the behavior we need.

## Why This Proposal Is Better Than Replacing The Current `/30`

It would be tempting to simply replace the current `/30` with a larger subnet everywhere.

That is not the best first move.

Reasons:

- the current Linux client deployment scripts are explicitly built around the current `/30`
- the shared entrypoint is used by both server and client roles
- the user wants to avoid unnecessary changes to the existing TUN client
- a second endpoint subnet gives the server the new capability without forcing an immediate redesign of the old path

So the proposal is deliberately incremental.

## What Will Change

Server-side deployment and provisioning will change:

- server TUN provisioning will support more than one address
- server NAT rules will support both the legacy subnet and the endpoint subnet
- server forwarding rules will support both subnets
- shared Docker entrypoint logic will become more general

Documentation will change:

- server deployment docs must explain both address spaces
- the endpoint subnet contract must be documented clearly

Validation will change:

- this slice needs explicit verification that replies to endpoint-subnet addresses route back to `tun0`
- all updated shell scripts must remain idempotent

## What Will Not Change

This slice does not change the protocol.

The following stay the same:

- one client advertises one IPv4 host address
- source IP validation remains exact
- server-side client routing remains exact-host
- raw IPv4 packets remain the payload
- no authentication is introduced

## Why This Work Is Important

Without this slice, Android full-device VPN work would be built on a server deployment model that is only accidentally suitable for one legacy client shape.

That would create predictable problems later:

- endpoint clients would not have a clean address space
- server NAT would be incomplete
- reply routing would be fragile
- deployment docs would be misleading
- each new endpoint host would push more ad hoc route hacks into the scripts

This slice solves the network foundation first, so later Android work can focus on Android-specific VPN behavior instead of fighting server routing and NAT issues.

## In One Sentence

The current server already knows how to forward tunnel traffic to the internet, but it is deployed like a narrow point-to-point `/30`; this slice expands the server into a proper egress node for a future pool of endpoint client IPs while keeping the current protocol and legacy Linux client path intact.
