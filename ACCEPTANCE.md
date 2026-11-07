# PigeonPost V1 Acceptance Checklist

## 1. Build & Test
- [ ] `dotnet build` — 0 errors
- [ ] `dotnet test` — all passing
- [ ] `dotnet nuget locals all --clear && dotnet restore --packages /tmp/nuget-verify` — offline restore succeeds

## 2. Desktop / macOS Connected Mode
- [ ] `dotnet run --project src/PigeonPost.VpnClientView.Desktop/` launches shell window
- [ ] Four tabs visible: Dashboard, Config, Logs, About
- [ ] Config tab accepts valid URL and client IP (11-254)
- [ ] Profile auto-saves on valid input
- [ ] Dashboard shows Connect button (Disabled without profile)
- [ ] Connect to a real server reaches Connected state
- [ ] Dashboard counters and speeds update
- [ ] Logs tab shows session lifecycle entries
- [ ] Disconnect returns to Disconnected state
- [ ] Re-launch restores saved profile

## 3. Desktop / macOS Probe Mode
- [ ] While connected, probe traffic sends ICMP echo requests
- [ ] Logs show periodic probe send entries
- [ ] Sent counters increase with probe traffic

## 4. Android Real Tunnel
- [ ] Android app builds: `dotnet publish -f net10.0-android -c Debug`
- [ ] VPN permission dialog appears on connect
- [ ] Foreground notification appears while connected
- [ ] System VPN indicator shows
- [ ] Internet traffic flows through tunnel
- [ ] Dashboard counters and speeds update
- [ ] Disconnect stops VPN interface and removes notification
- [ ] `onRevoke()` forces disconnect immediately

## 5. Server Interaction
- [ ] Duplicate client IP rejected with clear log message
- [ ] Endpoint isolation: endpoint client cannot ping another VPN peer
- [ ] Linux client can ping another Linux VPN peer
- [ ] Server egress (NAT to WAN) works for all clients

## 6. Deployment
- [ ] `deploy/server/pre-deploy.sh <wan_if>` runs idempotently
- [ ] `deploy/client/pre-deploy.sh` runs idempotently
- [ ] `deploy/server/verify-egress.sh` passes all checks
- [ ] Docker-based deploy scripts build and run

## 7. NuGet & Offline
- [ ] Local `./nugets/` feed contains all required packages
- [ ] `dotnet restore` works without internet access
