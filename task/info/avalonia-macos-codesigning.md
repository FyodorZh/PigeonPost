# macOS Code Signing — Avalonia

**Source URL:** https://docs.avaloniaui.net/docs/deployment/macos#code-signing

## Overview

Code signing is required for macOS app distribution. Hardened runtime is mandatory for notarization (required since macOS 10.15 Catalina for apps distributed outside the App Store).

## Developer ID Certificate

Required for distribution outside the Mac App Store. Obtain via:

### If You Own the Account
1. Xcode → Settings → Accounts → Add Apple ID
2. Select team → Download Manual Profiles
3. Export as `.p12` from Keychain Access (right-click → Export)

### If Shared with You
1. Get the exported `.p12` file from the account owner
2. Keychain Access → File → Import Items

Verify: `security find-identity -v`

## Hardened Runtime Entitlements

Create `MyAppEntitlements.entitlements`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.automation.apple-events</key>
    <true/>
</dict>
</plist>
```

- `allow-jit` is **required** for Avalonia/.NET runtime (JIT compilation)
- `apple-events` fixes Console.app errors
- Other hardened runtime exceptions from Microsoft docs may impose security risks

## Running codesign

Sign every file individually — **do NOT use `--deep`** (Apple recommends against it):

```bash
#!/bin/bash
APP_NAME="/path/to/MyApp.app"
ENTITLEMENTS="/path/to/MyAppEntitlements.entitlements"
SIGNING_IDENTITY="Developer ID: MyCompanyName"

find "$APP_NAME/Contents/MacOS/" | while read fname; do
    if [[ -f $fname ]]; then
        codesign --force --timestamp --options=runtime \
            --entitlements "$ENTITLEMENTS" \
            --sign "$SIGNING_IDENTITY" "$fname"
    fi
done

codesign --force --timestamp --options=runtime \
    --entitlements "$ENTITLEMENTS" \
    --sign "$SIGNING_IDENTITY" "$APP_NAME"
```

### Verification

```bash
codesign --verify --verbose /path/to/MyApp.app
```

## Notarization (from Code Signing Context)

1. **Codesign** the app properly first
2. **Zip** using `ditto -c -k --sequesterRsrc --keepParent MyApp.app MyApp.zip`
3. **Submit** via `xcrun notarytool submit MyApp.zip --keychain-profile "AC_PASSWORD" --wait`
4. **Staple** the ticket: `xcrun stapler staple MyApp.app`

### .dmg Notarization

If distributing as `.dmg`:
1. Notarize and staple the `.app`
2. Create the `.dmg` with the stapled `.app`
3. Notarize the `.dmg` using the same `notarytool submit` process
4. Staple the ticket to the `.dmg`

## App Store Distribution Certificates

| Certificate | Purpose |
|-------------|---------|
| `3rd Party Mac Developer Application` | Sign the `.app` bundle |
| `3rd Party Mac Developer Installer` | Sign the `.pkg` installer |

## Sandbox Entitlements (App Store)

### Helper executables entitlements:
```xml
<key>com.apple.security.app-sandbox</key>
<true/>
<key>com.apple.security.inherit</key>
<true/>
```

### App bundle entitlements:
```xml
<key>com.apple.security.cs.allow-jit</key>
<true/>
<key>com.apple.security.app-sandbox</key>
<true/>
<!-- Optional: network, files, etc. -->
```

## Bundle Structure Rules (App Store)

- `.dll` files → `Resources/` folder (not code, no signing needed)
- `.dylib` files → `Frameworks/` folder
- `MacOS/` → only mach-o executables
- Use relative symlinks from `MacOS/` to `Resources/` and `Frameworks/`

## See Also

- [Full macOS deployment guide](https://docs.avaloniaui.net/docs/deployment/macos)
- [Parcel macOS packaging](https://docs.avaloniaui.net/tools/parcel/packaging-for-macos)
