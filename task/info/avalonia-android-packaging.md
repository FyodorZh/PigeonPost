# Android Publishing — Avalonia

**Source URL:** https://docs.avaloniaui.net/docs/deployment/android

## Overview

Publishing an Avalonia app for Android generates APK (direct install) or AAB (Google Play) files.

## Running on Emulator

```bash
dotnet build
dotnet run
```

This deploys to the default emulator (or launches the first available AVD).

## Running on Device

1. Ensure Android version matches `AndroidManifest.xml` target
2. Connect device via USB with USB Debugging enabled
3. Switch USB mode to MTP if needed
4. Run `dotnet run`

## Publishing

### 1. Create Keystore (one-time)

```bash
keytool -genkeypair -v -keystore myapp.keystore -alias myapp \
  -keyalg RSA -keysize 2048 -validity 10000
```

### 2. Build and Sign

```bash
dotnet publish -f net9.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=myapp.keystore \
  -p:AndroidSigningKeyAlias=myapp \
  -p:AndroidSigningKeyPass=mypassword \
  -p:AndroidSigningStorePass=mypassword
```

Output: `bin/Release/net9.0-android/publish/` (both AAB and APK, signed variant has `-signed` suffix).

### Secure Password Handling

Using environment variable:
```bash
dotnet publish ... \
  -p:AndroidSigningKeyPass=env:ANDROID_SIGNING_PASSWORD \
  -p:AndroidSigningStorePass=env:ANDROID_SIGNING_PASSWORD
```

Using file:
```bash
dotnet publish ... \
  -p:AndroidSigningKeyPass=file:/path/to/password.txt \
  -p:AndroidSigningStorePass=file:/path/to/password.txt
```

**Note**: `env:` prefix is not supported when `AndroidPackageFormat` is `aab`.

## Build Properties Reference

| Property | Description |
|----------|-------------|
| `AndroidKeyStore` | `true` to sign (default: `false`) |
| `AndroidPackageFormats` | `aab`, `apk`, or `aab;apk` (default: `aab;apk`) |
| `AndroidSigningKeyAlias` | Key alias in the keystore |
| `AndroidSigningKeyPass` | Key password (`env:` / `file:` supported) |
| `AndroidSigningKeyStore` | Keystore filename |
| `AndroidSigningStorePass` | Keystore password (`env:` / `file:` supported) |
| `ApplicationTitle` | User-visible app name |
| `ApplicationId` | Unique ID (e.g. `com.companyname.myapp`) |
| `ApplicationVersion` | Build version number |
| `ApplicationDisplayVersion` | Display version string |
| `PublishTrimmed` | Trim unused code (default: `true` for release) |

### Project File Properties

```xml
<PropertyGroup Condition="$(TargetFramework.Contains('-android')) and '$(Configuration)' == 'Release'">
    <AndroidKeyStore>true</AndroidKeyStore>
    <AndroidSigningKeyStore>myapp.keystore</AndroidSigningKeyStore>
    <AndroidSigningKeyAlias>myapp</AndroidSigningKeyAlias>
    <AndroidSigningKeyPass>env:ANDROID_SIGNING_PASSWORD</AndroidSigningKeyPass>
    <AndroidSigningStorePass>env:ANDROID_SIGNING_PASSWORD</AndroidSigningStorePass>
</PropertyGroup>
```

Then publish with just `dotnet publish -f net9.0-android -c Release`.

## Distribution

| Method | Format | Details |
|--------|--------|---------|
| **Google Play** | AAB | Submit via Google Play Console |
| **Direct download** | APK | Host on a website; users enable "unknown sources" |

## See Also

- [Android platform setup](https://docs.avaloniaui.net/docs/platform-specific-guides/android)
