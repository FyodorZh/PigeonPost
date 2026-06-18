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
- V1 focus is Android plus a simplified macOS client.
- iOS is a known future target, but Apple entitlement readiness is still uncertain.
- The user wants to gather knowledge and de-risk architecture before locking the plan.

## Confirmed Product Decisions

- UI framework will be Avalonia.
- Target runtime is .NET 10.
- New shared VPN runtime project name: `PigeonPost.Vpn`.
- New shared/client UI project prefix: `PigeonPost.VpnClientView.*`.
- The future endpoint client must use the same protocol as the current TUN-based client.
- The future endpoint client must move raw IPv4 packets, not a new higher-level framed protocol.
- V1 is IPv4 only.
- V1 authentication: none.
- V1 secrets storage: none beyond the minimal local URL persistence requirement.
- V1 profile model: one locally stored connection profile, no multiple saved profiles.
- V1 reconnect behavior: immediate reconnect.
- V1 UI language: English only.
- V1 UI requirements:
  - connect/disconnect
  - editable URL
  - client IP selection from the allowed endpoint/mobile range
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
- Linux TUN clients remain a first-class deployment mode.
- Existing Linux ingress helpers remain first-class features.
- V1 endpoint clients should not access Linux TUN peers or other VPN peers.
- Do not change the current one-host identity rule in V1.
- Do not implement dynamic IP allocation in V1.
- Endpoint/mobile IP assignment in V1 should be manual.
- V1 endpoint/mobile DNS should route through the VPN, use a fixed list of public resolvers, and not be user-configurable.
- Android V1 should implement a real full-device VPN.
- macOS V1 should not register a system VPN.
- macOS V1 should still establish the real Pontifex transport and real handshake using the selected client IP.
- macOS V1 should generate synthetic ICMP probe traffic through the tunnel automatically while connected.
- macOS V1 should use `1.1.1.1` as its fixed public IPv4 probe target in V1 and log probe activity periodically.

Important implication:

- "one host" refers to the client's owned IP identity in the PigeonPost protocol
- "default-route VPN" refers to the device sending all traffic through the VPN
- these are compatible and should not be confused with each other
- on macOS V1, UI state `Connected` means the transport/session is connected even though no system VPN is registered

## Current Unified VPN Subnet Preference

- The current deployment model in the repo already uses `10.0.10.0/24` as the unified VPN subnet.
- The user wants to keep using the `10.0.10.x` space for the foreseeable future.
- The server VPN/TUN address is fixed at `10.0.10.1`.
- This subnet choice is currently a planning and deployment convention, not a result of an external network requirement.

Current chosen address-allocation direction:

- reserve `10.0.10.2` through `10.0.10.10` for manually configured Linux TUN clients
- leave the rest of the subnet available for manually assigned endpoint/mobile clients in V1

Current status:

- this is the current preferred and accepted planning direction
- endpoint/mobile IP is not encoded in the URL
- endpoint/mobile IP is selected by the user from the allowed range
- during connection, the server should reject the selection if that IP is already occupied
- there is no server-side "available IPs" query in V1

## Why The Current Client Runtime Is Not Reused

The user explicitly does not want to build the endpoint client on top of the current Linux TUN client runtime.

Reasons already identified:

- `ClientApp` is tightly coupled to Linux TUN opening and Linux TUN IP discovery.
- `ClientSideLogic` is centered on the current TUN-driven client model.
- The endpoint client will use platform VPN APIs, not the existing Linux TUN setup path.

The correct direction is a new endpoint runtime with shared transport and packet logic but different platform integration points.

For V1, the new runtime must support two different platform behaviors:

- Android: real platform VPN integration with full-device routing
- macOS: transport-connected probe mode with no registered system VPN

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
- assigns `10.0.10.1/24` to `tun0`
- brings `tun0` up
- enables `net.ipv4.ip_forward=1`
- adds WAN NAT for source subnet `10.0.10.0/24`
- adds `FORWARD` rules for that tunnel subnet out to WAN and established return traffic back
- does not install a single-client peer route because the connected `/24` subnet provides return routing for client IPs

Current plain server deploy: `deploy/server/deploy-plain.sh`

- publishes the app
- provisions `tun0` with `10.0.10.1/24`
- runs `PigeonPost.dll --role server`

Current Docker server deploy:

- `deploy/server/deploy-docker.sh`
- `deploy/server/docker/docker-compose.yml`
- shared `docker/docker-entrypoint.sh`

Current Docker server environment:

- `TUN_NAME=tun0`
- `TUN_CIDR=10.0.10.1/24`

Current shared entrypoint behavior:

- creates TUN device if needed
- assigns the configured `TUN_CIDR` exactly
- brings it up
- adds route `$PEER_IP/32` only if `PEER_IP` is provided
- executes the app

Current verification helper:

- `deploy/server/verify-egress.sh` verifies the unified server egress shape against `10.0.10.0/24`

Important consequence:

- current production server deployment is already based on one unified client-capable subnet model

## Current Production Linux Client Deployment Facts

Current host pre-deploy script: `deploy/client/pre-deploy.sh`

What it does today:

- accepts explicit `TUN_CIDR` and `PEER_IP`
- defaults to `tun0`
- defaults to `10.0.10.11/24`
- adds route to the configured peer via `tun0`
- enables IP forwarding
- adds `POSTROUTING -o tun0 -j MASQUERADE`
- creates `pp-ingress` ipset
- adds mangle rule marking packets whose source IP belongs to `pp-ingress`
- adds policy routing table `234`
- adds default route via the configured peer in table `234`
- adds `ip rule fwmark 1 table 234`

Current plain client deploy: `deploy/client/deploy-plain.sh`

- publishes the app
- provisions `tun0` with the configured `TUN_CIDR`
- defaults to `10.0.10.11/24`
- adds route to the configured `PEER_IP`
- runs `PigeonPost.dll --role client`

Current Docker client deploy:

- `deploy/client/deploy-docker.sh`
- `deploy/client/docker/docker-compose.yml`

Current Docker client environment:

- `TUN_NAME=tun0`
- `TUN_CIDR=10.0.10.11/24` by default
- `PEER_IP=10.0.10.1` by default
- `CLIENT_ID` exists only as cosmetic output

Important consequence:

- production client deployment artifacts now support explicit per-client identity through `TUN_CIDR`
- operators still need a documented allocation policy so different clients do not accidentally claim the same IP

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
  - `10.0.10.11`
  - `10.0.10.12`
  - `10.0.10.13`
  - `10.0.10.14`
- the unified `/24` subnet on the server provides automatic return routing for those clients

What it does not prove:

- that the future endpoint/mobile address allocation contract is finalized

Why not:

- it is still a Docker integration harness, not the full future endpoint/mobile deployment path
- it does not decide how endpoint/mobile clients will obtain their client IPs

Important conclusion:

- runtime multi-client support exists
- the repo deployment model now aligns with unified multi-client Linux support
- the remaining gap is mainly policy and product-contract clarity, especially around endpoint/mobile allocation

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
- no access to Linux TUN peers or other VPN peers in V1

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
- V1 direction has changed from "future desktop only" to a simplified real macOS client in parallel with Android.
- macOS V1 will not register a system VPN and will not depend on Apple VPN entitlements.
- macOS V1 will still establish a real Pontifex session and claim a real selected client IP during handshake.
- macOS V1 should behave as if the VPN module existed from the protocol/config/state perspective.
- macOS V1 should generate automatic synthetic ICMP probe traffic through the tunnel to exercise server egress and return routing.
- macOS V1 should use `1.1.1.1` as its fixed public IPv4 probe target in V1 rather than hostname-based probing.

Current V1 decision:

- implement Android and simplified macOS simultaneously in V1

## Architecture Direction Already Discussed

### Shared Runtime

New shared VPN runtime project:

- `PigeonPost.Vpn`

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

- `PigeonPost.VpnClientView.*`

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

- `PigeonPost.VpnClientView`
- `PigeonPost.VpnClientView.Android`
- `PigeonPost.VpnClientView.macOS`
- later `PigeonPost.VpnClientView.iOS`
- later `PigeonPost.VpnClientView.iOS.Extension`

The exact project list is still not locked, but this is the current direction.

Important future assumption accepted for planning:

- the real iOS tunnel runtime should be assumed to live in the extension process

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
- the current deployment scheme in the repo already uses the unified VPN subnet model needed for future endpoint clients

Why it can work in principle:

- the server already enables IP forwarding and NATs tunnel-originated traffic to WAN
- runtime already accepts client packets for arbitrary destination IPs
- replies can route back to exact client host identities if Linux routes them into `tun0`

What is still unresolved:

- the long-term allocation contract for endpoint/mobile client IPs is not frozen
- exact endpoint/mobile user experience for selecting an available IP is not frozen
- the exact fixed public resolver list is not frozen
- the deployment model is ahead of the planning document and still needs documentation cleanup

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

- the unified model has now been implemented in deployment artifacts in the repo
- the `/30` is a deployment artifact, not a runtime invariant
- a unified subnet model reduces long-term deployment complexity
- a unified model avoids dual-path docs, scripts, and testing

Current planning preference:

- the unified replacement model is now preferred over additive compatibility
- use the current `10.0.10.0/24` deployment as the planning baseline unless a concrete conflict appears later

## Multiple Linux TUN Deployment Findings

Research result already established:

- the runtime supports multiple Linux TUN clients
- the production deployment artifacts now support the unified model mechanically

Why runtime support exists:

- server sessions are keyed by advertised host IP
- exact-host routing already works for multiple unique clients
- Docker integration tests demonstrate multiple distinct client identities at runtime

What is still missing:

- a frozen operator-visible address-allocation contract
- documentation that clearly explains which addresses are intended for manual Linux TUN clients and which are intended for future endpoint/mobile clients
- a final documented UI/config contract for manual endpoint/mobile IP assignment

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
- Linux TUN remains a first-class participant in that one subnet model

## Current Recommended V1 Delivery Direction

- Android plus simplified macOS in V1
- shared VPN runtime in `PigeonPost.Vpn`
- shared Avalonia UI with future desktop-friendly layout
- build shared UI and shared runtime as product foundations, not as Android-only throwaway work
- test-first, vertical slices
- use the current unified deployment model as the baseline and document the manual client allocation contract before Android-specific runtime work
- keep Linux TUN deployment and ingress features as first-class capabilities while adding endpoint/mobile support
- keep current runtime protocol and validation rules unless a strong reason appears to change them
- keep V1 endpoint/mobile networking simple: manual client IP, no peer-to-peer access, fixed VPN-routed DNS
- on macOS V1, validate the transport/protocol path and server egress using automatic synthetic ICMP probe traffic rather than a real system VPN

## Planning Risks Already Known

- Confusing protocol identity with route scope.
- Assuming `CLIENT_ID` affects identity when it does not.
- Assuming the current Docker integration test already proves production multi-client deployment support.
- Assuming macOS avoids Apple VPN entitlement complexity.
- Underestimating how much server/networking work is required before full-device endpoint VPN can succeed cleanly.
- Breaking the current Linux client flow while expanding the server/network contract.
- Letting the macOS probe mode drift too far from the real shared runtime contract used by Android.
- Confusing "Connected" transport state on macOS with "system VPN is active".

## Known Open Questions

These are still unresolved or not yet frozen.

- Exact final project structure for `PigeonPost.Vpn` and `PigeonPost.VpnClientView.*`.
- Exact shared abstraction/interface set between endpoint runtime and platform hosts.
- Whether `10.0.10.0/24` should be treated as fully frozen long-term or only as the current stable planning baseline.
- Exact address-allocation contract inside `10.0.10.0/24`.
- Exact UI/config contract for manual endpoint/mobile IP selection and local persistence.
- Which exact fixed public DNS resolvers should be used in V1.
- Whether macOS should later become a real VPN platform or only a UI/development host first.
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
- V1 endpoint/mobile profile should contain server URL plus a user-selected client IP from the allowed range.
- Reconnect behavior in V1: immediate reconnect.
- Android always-on and kill-switch support: yes.
- iOS tunnel should continue while UI is closed: yes, eventually.
- UI should show real-time online/offline state, speed, sent/received counters, and logs: yes.
- UI should be suitable for future desktop usage: yes.
- V1 roadmap is Android plus simplified macOS in parallel: yes.
- Shared runtime project rename: `PigeonPost.Vpn`: yes.
- Shared UI project family rename: `PigeonPost.VpnClientView.*`: yes.
- Linux TUN clients remain a first-class deployment mode: yes.
- Existing ingress helpers remain first-class features: yes.
- V1 endpoint clients should access internet egress but not Linux TUN peers or other VPN peers: yes.
- Server VPN IP should be fixed at `10.0.10.1`: yes.
- Current preferred subnet family remains `10.0.10.x`: yes.
- Current preferred manual Linux TUN range is `10.0.10.2-10.0.10.10`: yes.
- V1 endpoint/mobile clients should use manual IP assignment: yes.
- Dynamic IP allocation is out of scope for V1: yes.
- The current one-host identity rule should remain unchanged in V1: yes.
- V1 endpoint/mobile should not provide peer-to-peer access: yes.
- V1 endpoint/mobile DNS should route through the VPN using a fixed public resolver list and should not be user-configurable: yes.
- If manual endpoint/mobile IP assignment is chosen in V1, the IP should be visible to the user: yes.
- V1 endpoint/mobile IP is not encoded in the URL and is selected separately by the user: yes.
- If the selected endpoint/mobile IP is already occupied, the server should reject the connection attempt: yes.
- V1 does not include a server-side "available IPs" query before connect: yes.
- Android V1 should implement a real full-device VPN: yes.
- macOS V1 should not register a system VPN: yes.
- macOS V1 should still establish real Pontifex transport and a real handshake with the selected client IP: yes.
- macOS V1 should use automatic synthetic ICMP probe traffic to exercise the tunnel while connected: yes.
- macOS V1 fixed public IPv4 probe target is `1.1.1.1`: yes.
- macOS V1 `Connected` state means transport/session connected, not system VPN active: yes.
- MTU and similar tunnel-tuning decisions are deferred to V2.
- Shared UI work should be treated as first-class and not mobile-only: yes.
- iOS planning may assume the real tunnel runtime lives in the extension: yes.

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
