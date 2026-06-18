# macOS Deployment — Avalonia

**Source URL:** https://docs.avaloniaui.net/docs/deployment/macos

## Overview

macOS applications are distributed as `.app` bundles — directories with a defined structure that macOS treats as a single launchable application.

## Bundle Structure

```
MyProgram.app/
└── Contents/
    ├── _CodeSignature/     # Code signing information
    ├── CodeResources
    ├── MacOS/              # dotnet publish output (DLLs + executable)
    │   ├── MyProgram       # App host executable (required)
    │   ├── MyProgram.dll
    │   └── Avalonia.dll
    ├── Resources/
    │   └── MyProgramIcon.icns
    ├── Info.plist          # Bundle manifest
    └── embedded.provisionprofile
```

## Info.plist Required Keys

| Key | Description | Example |
|-----|-------------|---------|
| `CFBundleExecutable` | Executable name (assembly name without .dll) | `MyApp` |
| `CFBundleName` | Display name (max 15 chars) | `My App` |
| `CFBundleDisplayName` | Full display name (if CFBundleName > 15 chars) | `My Application` |
| `CFBundleIconFile` | Icon filename with extension | `MyApp.icns` |
| `CFBundleIdentifier` | Reverse-DNS unique ID | `com.myapp.macos` |
| `NSHighResolutionCapable` | Retina display support | `<true/>` |
| `CFBundleVersion` | Build version number | `1.4.2` |
| `CFBundleShortVersionString` | User-visible version | `1.4.2` |

## App Host (Executable)

The `.app` bundle NEEDS a native executable (not just `.dll`). Ensure it's generated:

```xml
<PropertyGroup>
  <UseAppHost>true</UseAppHost>
</PropertyGroup>
```

Or pass `-p:UseAppHost=true` to `dotnet publish`. Optionally add `-p:PublishSingleFile=true`.

## Manual Packaging Steps

### 1. Publish

```bash
dotnet publish -r osx-x64 --configuration Release -p:UseAppHost=true
```

### 2. Create Bundle Script

```bash
#!/bin/bash
APP_NAME="/path/to/MyApp.app"
PUBLISH_OUTPUT_DIRECTORY="/path/to/publish/"
INFO_PLIST="/path/to/Info.plist"
ICON_FILE="/path/to/myapp-logo.icns"

rm -rf "$APP_NAME"
mkdir -p "$APP_NAME/Contents/MacOS"
mkdir -p "$APP_NAME/Contents/Resources"
cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/$(basename $ICON_FILE)"
cp -a "$PUBLISH_OUTPUT_DIRECTORY" "$APP_NAME/Contents/MacOS"
```

If built on Windows, run `chmod +x MyApp.app/Contents/MacOS/AppName` from Unix.

## Code Signing (Summary)

See [code signing section](https://docs.avaloniaui.net/docs/deployment/macos#code-signing) for full details.

## Notarization (Summary)

Required for distribution outside the Mac App Store (macOS 10.15+). Uses `notarytool` (Xcode 13+).

### Quick Command Sequence

```bash
# Store credentials
xcrun notarytool store-credentials "AC_PASSWORD" \
  --apple-id "user@example.com" \
  --team-id "YOURTEAMID" \
  --password "app-specific-password"

# Notarize
ditto -c -k --sequesterRsrc --keepParent MyApp.app MyApp.zip
xcrun notarytool submit MyApp.zip --keychain-profile "AC_PASSWORD" --wait

# Staple ticket
xcrun stapler staple MyApp.app
```

## App Store Distribution

Requires: Developer account, App Store Connect registration, `3rd Party Mac Developer Application` and `3rd Party Mac Developer Installer` certificates, provisioning profile, sandbox compliance, and Transporter app for upload.

## GitHub Actions CI/CD

Key steps:
1. Create and unlock a temporary keychain
2. Import the `.p12` certificate from a base64 secret
3. Set keychain partition list for codesign
4. Store notarytool credentials
5. Publish, codesign, notarize

## See Also

- [macOS code signing detail](https://docs.avaloniaui.net/docs/deployment/macos#code-signing)
- [Parcel macOS packaging](https://docs.avaloniaui.net/tools/parcel/packaging-for-macos)
- [macOS platform integration](https://docs.avaloniaui.net/docs/platform-specific-guides/macos)
- [iOS deployment](https://docs.avaloniaui.net/docs/deployment/ios)
