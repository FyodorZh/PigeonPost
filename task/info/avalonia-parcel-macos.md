# Packaging for macOS with Parcel — Avalonia

**Source URL:** https://docs.avaloniaui.net/tools/parcel/packaging-for-macos

## Overview

Parcel is a CLI tool that automates macOS packaging for Avalonia apps. It handles bundle structure, Info.plist generation, code signing, notarization, and DMG creation from a single command. Can create macOS bundles from Windows and Linux.

## Packaging

### Bundle Configuration

**Application Name**: Used as `CFBundleDisplayName`  
**Package Name**: Used for bundle and output DMG file names

### Bundle Properties

| Property | Description |
|----------|-------------|
| `Bundle Identifier` | Reverse-DNS unique ID (e.g. `com.Company.AppName`) |
| `Team ID` | Apple Developer account team ID (required for signing) |
| `App Category` | macOS App Store classification |
| `App Icon` | ICNS or SVG format (Parcel generates bundle icon structure) |
| `Permissions` | System permissions with usage descriptions (mandatory) |

### Custom Info.plist

1. Create `Info.plist` in the project root
2. Custom keys take precedence over auto-generated ones
3. Missing properties are auto-added based on project config

### DMG Creation

- Fixed window size: **660x422** pixels
- App icon at (180, 170) with 160px
- Applications folder at (480, 170) with 160px
- Background image in TIFF format
- **WSL2 required** for DMG creation on Windows (ZIP works without WSL2)

### ZIP Creation

Preserves executable permissions. Bundle structure remains intact when extracted on macOS.

## Code Signing

### Prerequisites
- Active Apple Developer Program membership ($99/year)
- Xcode Command Line Tools (macOS only)

### Signing Methods

**KeyChain Identity (macOS only)**: Uses certificates from macOS Keychain.

**P12 Certificate (Cross-Platform)**: Portable format exported from Keychain or generated with OpenSSL. Parcel uses `rcodesign` to sign on Windows/Linux.

### Creating a Developer Certificate

**Keychain (macOS):**
1. Keychain Access → Certificate Assistant → Request a Certificate
2. Upload CSR to Apple Developer Portal → Download .cer
3. Import to Keychain → Export as .p12 for cross-platform use

**OpenSSL (Cross-Platform):**
```bash
openssl genrsa -out private.key 2048
openssl req -new -key private.key -out certificate.csr
# Upload CSR to Apple Developer Portal, download .cer
openssl x509 -in development.cer -inform DER -out certificate.pem -outform PEM
openssl pkcs12 -export -out certificate.p12 -inkey private.key -in certificate.pem
```

## Notarization

Required for macOS 10.15+ distribution outside the App Store.

### Apple Account Authentication

**App-Specific Password (Recommended)**:
- Generate at appleid.apple.com
- Configure via Apple ID + app-specific password + Team ID
- Use environment variables for credentials

**Keychain Profile (macOS only)**:
```bash
xcrun notarytool store-credentials "MyParcelProfile" \
  --apple-id "user@example.com" \
  --team-id "YOUR_TEAM_ID"
```
Then reference by profile name in Parcel.

### For Testing Without Notarization

System Preferences → Security & Privacy → "Open Anyway" for blocked apps. Code sign with Developer ID certificate when possible even without notarization.

## See Also

- [Parcel setup](https://docs.avaloniaui.net/tools/parcel/setup)
- [Parcel command line reference](https://docs.avaloniaui.net/tools/parcel/command-line-reference)
- [macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos)
