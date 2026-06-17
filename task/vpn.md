# VPN Client Knowledge Base

## Purpose

This document is a living knowledge base for the future PigeonPost VPN client work.

It is not the final implementation plan.

Its job is to capture:

- decisions already made
- precise facts about the current codebase and deployment model
- platform constraints for Android, iOS, Avalonia, and future desktop targets
- architecture direction that has already been discussed
- known limitations in the current implementation
- open questions that still need answers before converting this into an actionable plan

This file should grow over time. When enough uncertainty has been removed, it can be transformed into the real delivery plan.

## Current Planning Status

- Planning is still in progress.
- The target direction is a new endpoint-style VPN client, not a reuse of the current Linux TUN client runtime.
- V1 focus is Android only.
- iOS is a known future target, but Apple entitlement readiness is still uncertain.
- The user wants to gather knowledge and de-risk architecture before locking the plan.

## Confirmed Product Decisions

- UI framework will be Avalonia.
- Target runtime is .NET 10.
- New shared endpoint runtime project name: `PigeonPost.EndPoint`.
- New shared/client UI project prefix: `PigeonPost.EPClient.*`.
- The future endpoint client must use the same protocol as the current TUN-based client.
- The future endpoint client must move raw IPv4 packets, not a new higher-level framed protocol.
- V1 is IPv4 only.
- V1 authentication: none.
- V1 secrets storage: none beyond the minimal local URL persistence requirement.
- V1 profile model: one locally stored URL, no multiple saved profiles.
- V1 reconnect behavior: immediate reconnect.
- V1 UI language: English only.
- V1 UI requirements:
  - connect/disconnect
  - editable URL
  - online/offline state
  - current speed
  - sent/received traffic during current session
  - logs list
- UI should be designed so that it scales well to desktop in the future.
- Development style should be test-first and built in vertical functionality slices.
- Changes to the current Linux TUN client should be minimized unless strongly required.

## Confirmed VPN Model Decisions

- VPN mode should be full-device VPN, not split-tunnel, for Android and future iOS.
- Protocol identity should remain one advertised client IPv4 host address.
- Device routing model should be default-route VPN on the mobile device.
- The endpoint runtime should later use the same one-host identity model that the server already understands.

Important implication:

- "one host" refers to the client's owned IP identity in the PigeonPost protocol
- "default-route VPN" refers to the device sending all traffic through the VPN
- these are compatible and should not be confused with each other

## Why The Current Client Runtime Is Not Reused

The user explicitly does not want to build the endpoint client on top of the current Linux TUN client runtime.

Reasons already identified:

- `ClientApp` is tightly coupled to Linux TUN opening and Linux TUN IP discovery.
- `ClientSideLogic` is centered on the current TUN-driven client model.
- The endpoint client will use platform VPN APIs, not the existing Linux TUN setup path.

The correct direction is a new endpoint runtime with shared transport and packet logic but different platform integration points.

## Current Repo Facts

- The repository is a .NET 10 solution.
- The current product is a Linux-only console app.
- Solution projects today:
  - `src/PigeonPost`
  - `src/PigeonPost.Bridge`
  - `src/PigeonPost.Tun`
  - `src/PigeonPost.Tun.Virtual`
  - tests under `tests/`
- Global build settings from `Directory.Build.props`:
  - `TargetFramework=net10.0`
  - `ImplicitUsings=false`
  - `Nullable=enable`
  - `TreatWarningsAsErrors=true`

## Current Runtime Architecture Facts

- The current server runtime owns one TUN device and supports multiple concurrent clients.
- Each current client session is keyed by its advertised IPv4 host address.
- The current server routes outbound packets by exact destination host match.
- The current server accepts inbound client packets only if the packet source IP equals that client's advertised host IP.
- There is no fallback route.
- There is no subnet ownership model in the runtime today.
- There is no authentication in the current runtime.

Important source files:

- `src/PigeonPost/App/ServerApp.cs`
- `src/PigeonPost/App/ClientApp.cs`
- `src/PigeonPost.Bridge/Logic/ServerSideLogic.cs`
- `src/PigeonPost.Bridge/Logic/ClientSideLogic.cs`
- `src/PigeonPost.Bridge/Server/ServerHub.cs`
- `src/PigeonPost.Bridge/Protocol/ClientHandshake.cs`

## Current Protocol Facts

- Handshake carries one advertised IPv4 host address.
- The server rejects duplicate advertised host IP claims.
- The server stores sessions keyed by that one host IP.
- Outbound routing from server to client is exact-host only.
- Inbound validation from client to server is exact-source only.
- Payload mapping is one raw IPv4 packet per Pontifex message.

What this means for future endpoint clients:

- the server already understands the key identity model needed by the endpoint client
- the deployment/networking model is the bigger problem, not the handshake shape

## Current Linux Client Identity Facts

- The real identity of a current Linux client is the IPv4 address configured on its TUN device.
- `src/PigeonPost/App/ClientApp.cs` resolves the TUN IP at startup.
- `src/PigeonPost.Tun/TunIpv4AddressResolver.cs` requires exactly one IPv4 address on the TUN device.

Important consequence:

- client identity is not a logical name today
- client identity is not controlled by `CLIENT_ID`
- changing TUN IP changes the protocol identity

## `CLIENT_ID` Facts

- `CLIENT_ID` currently appears only in docs and `deploy/client/deploy-docker.sh` output.
- It does not affect the actual client handshake or routing identity.
- It is cosmetic only in the current codebase.

Important consequence:

- any future real multi-client deployment support must revolve around unique TUN/client IPs, not labels

## Current Production Server Deployment Facts

Current host pre-deploy script: `deploy/server/pre-deploy.sh`

What it does today:

- expects a WAN interface argument
- creates `tun0`
- assigns `10.0.0.1/30` to `tun0`
- brings `tun0` up
- adds route `10.0.0.2/32` via `tun0`
- enables `net.ipv4.ip_forward=1`
- adds WAN NAT for source subnet `10.0.0.0/30`
- adds `FORWARD` rules for that tunnel subnet out to WAN and established return traffic back

Current plain server deploy: `deploy/server/deploy-plain.sh`

- publishes the app
- provisions `tun0` with `10.0.0.1/30`
- adds route to `10.0.0.2/32`
- runs `PigeonPost.dll --role server`

Current Docker server deploy:

- `deploy/server/deploy-docker.sh`
- `deploy/server/docker/docker-compose.yml`
- shared `docker/docker-entrypoint.sh`

Current Docker server environment:

- `TUN_NAME=tun0`
- `TUN_IP=10.0.0.1`
- `PEER_IP=10.0.0.2`

Current shared entrypoint behavior:

- creates TUN device if needed
- assigns `$TUN_IP/30`
- brings it up
- adds route `$PEER_IP/32`
- executes the app

Important consequence:

- current production server deployment is still fundamentally single-peer in shape

## Current Production Linux Client Deployment Facts

Current host pre-deploy script: `deploy/client/pre-deploy.sh`

What it does today:

- creates `tun0`
- assigns `10.0.0.2/30`
- adds route to `10.0.0.1/32` via `tun0`
- enables IP forwarding
- adds `POSTROUTING -o tun0 -j MASQUERADE`
- creates `pp-ingress` ipset
- adds mangle rule marking packets whose source IP belongs to `pp-ingress`
- adds policy routing table `234`
- adds default route via `10.0.0.1 dev tun0` in table `234`
- adds `ip rule fwmark 1 table 234`

Current plain client deploy: `deploy/client/deploy-plain.sh`

- publishes the app
- provisions `tun0` with `10.0.0.2/30`
- adds route to `10.0.0.1/32`
- runs `PigeonPost.dll --role client`

Current Docker client deploy:

- `deploy/client/deploy-docker.sh`
- `deploy/client/docker/docker-compose.yml`

Current Docker client environment:

- `TUN_NAME=tun0`
- `TUN_IP=10.0.0.2`
- `PEER_IP=10.0.0.1`

Important consequence:

- all ready-made production client deployment artifacts are still single-client by default
- they all point at the same client identity `10.0.0.2`

## Why Current Linux Client Works With Strict Source-IP Validation

The current Linux client setup MASQUERADEs policy-routed traffic out of `tun0`.

That means:

- the server does not see arbitrary original LAN or ingress source addresses
- the server sees traffic rewritten to the client's own TUN IP

That is exactly why the server's strict validation rule works for today's Linux client model.

Important implication for future work:

- any deployment model that keeps the current runtime validation rules must preserve the "traffic appears from the client's owned TUN IP" property

## Existing Ingress Helper Facts

Client ingress scripts:

- `deploy/client/ingress/ingress-lan.sh`
- `deploy/client/ingress/ingress-pptp.sh`
- `deploy/client/ingress/ingress-debug.sh`

What they do conceptually:

- register source subnets or debug namespaces into `pp-ingress`
- let policy routing send those marked packets into the tunnel

Important implication:

- current Linux client traffic selection is source-based, not destination-based
- this is separate from the future full-device endpoint client model

## Existing Docker Integration Test Facts

Integration harness:

- `deploy/test/docker/docker-compose.yml`
- `deploy/test/docker/iperf-server.sh`

What it proves:

- the runtime can handle one server plus multiple clients
- clients can advertise unique TUN IPs such as:
  - `10.0.0.2`
  - `10.0.0.6`
  - `10.0.0.9`
  - `10.0.0.13`

What it does not prove:

- that production deployment artifacts already support multi-client Linux deployment cleanly

Why not:

- the server-side iperf helper manually injects extra `/32` return routes for additional client IPs
- the shared deployment logic still centers on one peer route and `/30`
- the client addresses are chosen around the current `/30`-based provisioning behavior, not a clean production network contract

Important conclusion:

- runtime multi-client support exists
- production multi-client deployment support does not yet exist as a clean, documented, first-class capability

## Avalonia Facts Relevant To Planning

- Avalonia supports Android, iOS, Windows, macOS, Linux, and browser targets.
- Avalonia documentation recommends a shared core/shared UI project plus thin platform-specific host projects.
- Shared code should contain views, viewmodels, styles, and business logic.
- Platform-specific behavior should be introduced through abstractions and DI.
- Compiled bindings with `x:DataType` are preferred.
- CommunityToolkit.Mvvm is the recommended MVVM library for this work.

Important implication:

- shared UI for Android and later desktop is realistic
- platform VPN APIs still need platform-specific host/service layers

## Android Facts Relevant To Planning

- Android VPN apps are built around `VpnService`.
- `VpnService` is the Android base class for app-implemented VPNs.
- Android requires user approval before a VPN can be created the first time.
- Only one VPN can be active at a time on the device.
- Android shows a system-managed VPN notification while active.
- Android can revoke the VPN and the app must react correctly.
- On Android 8+ the VPN app must promote itself to a foreground service after background startup.

Current planning decision:

- V1 Android minimum API level target: API 26

Android V1 requirements already accepted by the user:

- full-device VPN
- always-on VPN support
- kill-switch support
- immediate reconnect
- live UI status where practical

## iOS Facts Relevant To Planning

- iOS VPN uses `NetworkExtension` APIs.
- A packet-tunnel VPN is implemented in a `NEPacketTunnelProvider` extension.
- The real running VPN logic is not hosted inside the UI app process.
- The UI app and the VPN extension are separate processes.
- If the tunnel must keep running while the UI app is closed, the runtime belongs in the extension.

Important implications:

- later iOS support will require a separate extension project
- shared status/log/config likely need a shared mechanism such as App Group storage
- iOS signing/provisioning/entitlements are a real delivery dependency

Current status of Apple readiness:

- the user has a Mac with Xcode
- the user has an iPad for testing
- the user probably has an Apple Developer account
- Apple entitlements are not ready yet, but the user expects to get them

Important future constraint:

- iOS work should not be treated as only a UI port; it is a separate privileged extension deployment problem

## macOS Facts Relevant To Planning

- macOS was discussed as a possible fallback if iOS entitlements are delayed.
- Important conclusion already established: macOS does not really remove the Apple VPN entitlement/capability problem if a real system VPN is required.
- macOS may still be useful later as:
  - a real Apple VPN platform once Apple setup is ready
  - a UI or development host

Current V1 decision:

- start with Android only

## Architecture Direction Already Discussed

### Shared Runtime

New shared endpoint runtime project:

- `PigeonPost.EndPoint`

Responsibilities expected for this project:

- Pontifex transport integration
- handshake/session lifecycle
- reconnect behavior
- raw IPv4 packet flow handling
- config validation
- connection state model
- counters and diagnostics model
- no Avalonia dependency
- no Android/iOS/macOS UI types
- no current Linux TUN dependency as a primary abstraction

### Shared Client UI

Shared Avalonia UI project family:

- `PigeonPost.EPClient.*`

Expected shared UI responsibilities:

- app shell
- pages
- viewmodels
- styles/themes/assets
- shared DI setup
- URL editing and local persistence
- logs and live status presentation

### Platform Hosts Expected Later

Expected direction discussed earlier:

- `PigeonPost.EPClient`
- `PigeonPost.EPClient.Android`
- later `PigeonPost.EPClient.iOS`
- later `PigeonPost.EPClient.iOS.Extension`

The exact project list is still not locked, but this is the current direction.

## Suggested Shared Abstraction Direction

No final interface set is locked yet, but the following abstractions were already identified as likely necessary:

- packet flow abstraction between platform VPN layer and shared endpoint runtime
- runtime start/stop/status abstraction
- config store abstraction
- status/log publication abstraction

Important architecture rule:

- Avalonia UI should not directly own or implement platform VPN behavior

## Server Egress Findings

Research result already established:

- the current server can act as internet egress architecturally
- the current deployment scheme cannot support future full-device endpoint clients cleanly as-is

Why it can work in principle:

- the server already enables IP forwarding and NATs tunnel-originated traffic to WAN
- runtime already accepts client packets for arbitrary destination IPs
- replies can route back to exact client host identities if Linux routes them into `tun0`

Why it is not good enough as-is:

- NAT is hard-coded to `10.0.0.0/30`
- shared provisioning assumes one `/30` address and one peer route
- there is no dedicated endpoint address pool
- return routing for many client identities is not a clean production contract today

Documented follow-up slice:

- `task/egress/task.md`
- `task/egress/readme.md`
- `task/egress/post-task.md`

Original conservative direction from that slice:

- preserve the legacy server `/30` path
- add a second connected subnet on the server TUN for future endpoint clients
- NAT that endpoint subnet to WAN
- keep runtime protocol unchanged

Updated preferred direction after further analysis:

- do not preserve the old `/30` deploy behavior
- replace it completely with one unified multi-client-capable VPN subnet model
- use that same subnet model for:
  - Linux TUN clients
  - future Android endpoint clients
  - future iOS endpoint clients
- keep runtime protocol unchanged

Reason the preferred direction changed:

- nothing has been implemented yet
- the `/30` is a deployment artifact, not a runtime invariant
- a unified subnet model reduces long-term deployment complexity
- a unified model avoids dual-path docs, scripts, and testing

Current planning preference:

- the unified replacement model is now preferred over additive compatibility

## Multiple Linux TUN Deployment Findings

Research result already established:

- the runtime supports multiple Linux TUN clients
- the production deployment artifacts do not support them cleanly today

Why runtime support exists:

- server sessions are keyed by advertised host IP
- exact-host routing already works for multiple unique clients
- Docker integration tests demonstrate multiple distinct client identities at runtime

Why production deployment support does not exist yet:

- production client deployment hard-codes `10.0.0.2`
- `CLIENT_ID` is not real identity
- shared provisioning is still `/30`-centric
- production server deployment originally started as single-peer

Documented follow-up slice:

- `task/multi-tun/task.md`
- `task/multi-tun/readme.md`

Core proposed direction from that slice:

- define a real client-capable address pool
- make both plain and Docker client deployments explicitly parameterize TUN IP identity
- preserve client-side MASQUERADE so server strict source validation still works

This slice now aligns naturally with the updated preferred server direction:

- one unified VPN subnet model
- no preserved legacy `/30` production path

## Current Recommended V1 Delivery Direction

- Android only in V1
- shared endpoint runtime in `PigeonPost.EndPoint`
- shared Avalonia UI with future desktop-friendly layout
- test-first, vertical slices
- replace the legacy `/30` deployment model with one unified client-capable VPN subnet model before Android-specific runtime work
- complete server and deployment groundwork before Android-specific VPN runtime work
- keep current runtime protocol and validation rules unless a strong reason appears to change them

## Planning Risks Already Known

- Confusing protocol identity with route scope.
- Assuming `CLIENT_ID` affects identity when it does not.
- Assuming the current Docker integration test already proves production multi-client deployment support.
- Assuming macOS avoids Apple VPN entitlement complexity.
- Underestimating how much server/networking work is required before full-device endpoint VPN can succeed cleanly.
- Breaking the current Linux client flow while expanding the server/network contract.

## Known Open Questions

These are still unresolved or not yet frozen.

- Exact final project structure for `PigeonPost.EndPoint` and `PigeonPost.EPClient.*`.
- Exact shared abstraction/interface set between endpoint runtime and platform hosts.
- Exact unified VPN client subnet contract to be used long-term.
- Whether macOS should later become a real VPN platform or only a UI/development host first.
- How future endpoint clients will obtain and persist their unique client IP assignment.
- Whether any shared logic should be extracted from the current Linux TUN client after the new runtime takes shape.

Questions that are no longer preferred planning directions:

- whether to preserve the old `/30` deployment behavior alongside the new one
- whether Linux multi-client and endpoint egress should live in separate production address models

Current preference on those topics:

- do not preserve the old `/30` production deploy path
- use one unified subnet model if practical implementation confirms it

## Questions Already Answered

- Same protocol as current TUN client: yes.
- Raw IP packets: yes.
- IPv6 in V1: no.
- Authentication in V1: no.
- Multiple user profiles in V1: no.
- User-editable setting in V1: server URL only.
- Reconnect behavior in V1: immediate reconnect.
- Android always-on and kill-switch support: yes.
- iOS tunnel should continue while UI is closed: yes, eventually.
- UI should show real-time online/offline state, speed, sent/received counters, and logs: yes.
- UI should be suitable for future desktop usage: yes.

## Suggested Future Structure For This File

As planning continues, future additions should be recorded under clear labels such as:

- Confirmed decisions
- Codebase facts
- Deployment facts
- Platform facts
- Risks
- Open questions
- Deferred decisions

The purpose is to avoid re-discovering the same facts and to keep planning grounded in the actual repo and deployment behavior.
