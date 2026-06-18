# Stage 04 - Profile Persistence

## Goal

Persist the single V1 profile locally and restore it automatically on startup.

## Why This Stage Exists

- URL persistence is explicitly required.
- One-profile storage is simple and should be stabilized early.
- First-launch behavior depends on whether a saved profile exists.

## User-Visible Value

The app remembers the user's server URL, selected client IP, and verbosity across restarts.

## Create Or Modify

- Add `IProfileStore` in `PigeonPost.Vpn`.
- Add a desktop implementation backed by a JSON file in app data.
- Add a placeholder Android implementation interface registration only; real Android storage wiring can come later.
- Bind auto-save behavior from `ConfigViewModel`.

## Technical Decisions

- Store exactly one profile file, not a list.
- Do not store secrets because V1 has none.
- Corrupt profile files must fall back cleanly to defaults and produce a visible log entry later.
- First launch goes to Config when no profile exists; otherwise go to Dashboard.

## Implementation Steps

1. Add the store abstraction and desktop JSON implementation.
2. Add startup loading in the desktop host DI composition.
3. Auto-save on every config change.
4. Teach `MainViewModel` to choose the first tab based on profile presence.
5. Keep the file format minimal and versionable.

## Automated Verification

- `dotnet test tests/PigeonPost.Vpn.Tests/`
- `dotnet test tests/PigeonPost.VpnClientView.Tests/`

Suggested NUnit coverage:

- Save then load roundtrip.
- Missing file returns null or defaults without failure.
- Corrupt JSON is handled safely.
- First-launch tab selection logic.

## Manual Verification

1. Launch the desktop host with no existing profile and confirm Config opens first.
2. Enter a URL and client IP.
3. Close and relaunch the app.
4. Confirm the values are restored and Dashboard is now the default tab.

## Completion Criteria

- One profile is saved and restored automatically.
- The app survives missing or corrupt profile storage.
- No Save button is needed; persistence is immediate.

## Out Of Scope

- Runtime connect/disconnect.
- Android-specific storage implementation details.
- Log replay from previous sessions.
