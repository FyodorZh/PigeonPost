# Multiple Linux TUN Client Deployment Problem

## What This Problem Is About

PigeonPost already has a server runtime that can manage multiple clients at once.

Each client advertises one IPv4 host address, and the server routes packets back to that client by exact destination host match.

So the runtime already understands multiple clients.

The real problem is deployment.

Today, the ready-made Linux client deployment artifacts still behave as if there is one default client identity. That makes the system look multi-client in code but single-client in production operations.

This document explains why that happens, why it matters, and what the correct new model should be.

## The Most Important Fact

In the current architecture, a Linux client's real identity is its TUN IPv4 address.

It is not:

- `CLIENT_ID`
- hostname
- container name
- logical label

The client process reads the IPv4 configured on its TUN interface and advertises that address to the server.

That means two deployed clients cannot safely share the same TUN IP. If they do, they are trying to claim the same server-side identity.

## Why Current Production Deployment Is Not Enough

### Current production client deployment hard-codes one identity

The current production Linux client artifacts are built around:

- `tun0`
- `10.0.0.2/30`
- route to `10.0.0.1`

That means if an operator uses the ready-made client deployment flow on two different machines, both will default to the same TUN identity.

This is not just inconvenient. It is a direct identity collision.

### `CLIENT_ID` does not solve this

There is a `CLIENT_ID` variable in the Docker client deploy script, but it does not change the protocol identity.

Today it is only cosmetic output.

So changing `CLIENT_ID` does **not** create a distinct network client.

### The shared provisioning path still assumes old `/30` behavior

The shared Docker entrypoint assigns `TUN_IP/30` and adds one peer route.

That comes from the old point-to-point deployment mindset, not from a true multi-client-capable deployment contract.

## Why The Runtime Still Works

It is important to separate deployment limitations from runtime limitations.

The runtime already supports:

- one server
- multiple clients
- each client identified by one host IP
- exact-host routing back to each client
- strict validation that a client's packets must use that client's own source IP

This is why the multi-client Docker integration test can work.

But that test succeeds because it uses special manual wiring, not because production deployment artifacts are already correct.

## Why The Existing Docker Integration Test Is Not The Same As Supported Production Deployment

The integration test creates multiple clients with addresses like:

- `10.0.0.2`
- `10.0.0.6`
- `10.0.0.9`
- `10.0.0.13`

It then manually injects extra return routes on the server side.

That proves the runtime can carry multiple identities.

It does **not** prove that the supported production deploy model is already clean and ready.

The production scripts and docs still lead operators toward the old single-client shape.

## Why This Should Be Solved Together With The New Unified VPN Subnet Model

There was an earlier conservative idea:

- keep the old Linux `/30` deploy behavior
- add a second subnet for endpoint/mobile clients

That would work, but it would leave two deployment universes in the product.

That is not the cleanest direction.

The better direction is:

- remove the old `/30` production deploy model completely
- use one unified VPN subnet for all clients

That means:

- Linux TUN clients live in that subnet
- future Android endpoint clients live in that subnet
- future iOS endpoint clients live in that subnet

This gives the product one consistent story.

## What The New Model Should Look Like

The exact subnet value still needs to be finalized, but the concept is simple.

Example only:

- server tunnel address: `10.0.10.1/24`
- Linux client A: `10.0.10.11/24`
- Linux client B: `10.0.10.12/24`
- future Android endpoint A: `10.0.10.21`

Key rule:

- every client owns one unique IP from the unified VPN subnet

That one IP is both:

- the protocol identity
- the source address that the server expects to see after client-side source NAT

## Why Client-Side MASQUERADE Still Matters

This is one of the most important technical details.

Current Linux client deployment does not send arbitrary original ingress source IPs to the server. It source-NATs selected traffic so the packet appears to come from the client's own TUN IP.

That behavior must remain.

Why:

- the server still enforces that packet source IP must equal the client's advertised host IP

So the new deployment model should **not** remove this behavior. It should preserve it and make it work for many clients by giving each client a unique TUN IP.

## What Must Change

### 1. Client deployment must become explicitly IP-configured

Linux client deploy scripts must stop hard-coding `10.0.0.2`.

Instead they must accept a real per-client network contract, such as:

- TUN device name
- TUN IP
- TUN CIDR
- server tunnel peer IP
- transport URL

### 2. Docker and plain deploys must both use that contract

The repo requires Docker and plain-host deployment paths to remain equivalent.

So both need to support explicit per-client identity.

### 3. Shared provisioning must stop assuming `/30`

If the shared entrypoint continues to force `TUN_IP/30`, the unified model is not really implemented.

The entrypoint must assign the configured CIDR exactly.

### 4. Docs must explain the true identity rule clearly

Operators must understand:

- the real client identity is the TUN IPv4
- `CLIENT_ID` is not handshake identity
- duplicate client IPs are deployment errors

## Why This Work Is Important

Without this slice, the product would keep a dangerous mismatch:

- runtime supports many clients
- production deployment defaults still pretend there is only one client identity

That creates confusion and operational mistakes.

Examples:

- two clients both deployed as `10.0.0.2`
- operators thinking `CLIENT_ID` changes identity
- docs showing an old model that does not match the intended future platform direction

This slice removes that mismatch.

## What Will Stay The Same

This work does not require changing the runtime protocol.

These rules remain:

- one client advertises one host IP
- server routes by exact host match
- server validates source IP exactly
- raw IPv4 packets remain the payload

So the change is primarily about deployment clarity and correctness, not protocol redesign.

## The Proposed Fix In Simple Terms

The proposed fix is:

1. define one unified VPN subnet on the server side
2. require every Linux client to use one unique TUN IP from that subnet
3. parameterize both plain and Docker deploy flows with that IP
4. preserve client-side source NAT so forwarded traffic still appears from that client's owned IP
5. document clearly that this TUN IP is the real client identity

## In One Sentence

PigeonPost already supports multiple Linux TUN clients at runtime, but the production deployment artifacts still default to one old single-client identity; this slice replaces that old deployment model with a unified subnet-based model where every Linux client gets a real unique network identity that matches the existing protocol.
