# macOS Code Signing & Notarization

## Prerequisites
- Apple Developer account with a valid Developer ID Application certificate
- Xcode 14+ command line tools installed

## Signing
```bash
codesign --force --options runtime --deep \
  --sign "Developer ID Application: Your Name (TEAMID)" \
  path/to/PigeonPost.app
```

## Notarization
```bash
zip -r PigeonPost.zip PigeonPost.app
xcrun notarytool submit PigeonPost.zip \
  --apple-id "your@email.com" \
  --team-id "TEAMID" \
  --password "@keychain:AC_PASSWORD" \
  --wait
xcrun stapler staple PigeonPost.app
```

## Local Development
For local testing without signing, run directly from the build output:
```bash
dotnet run --project src/PigeonPost.VpnClientView.Desktop/
```
