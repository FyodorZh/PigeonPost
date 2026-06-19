#!/usr/bin/env bash
set -euo pipefail

APP_NAME="PigeonPost"
PUBLISH_DIR="$(dirname "$0")/../../src/PigeonPost.VpnClientView.Desktop/bin/Release/net10.0/publish"
BUNDLE_DIR="$(dirname "$0")/../../artifacts/${APP_NAME}.app"
CONTENTS_DIR="${BUNDLE_DIR}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"

dotnet publish src/PigeonPost.VpnClientView.Desktop/PigeonPost.VpnClientView.Desktop.csproj \
  -c Release -o "${PUBLISH_DIR}" \
  --self-contained true \
  -p:UseAppHost=true

mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}"

cp -R "${PUBLISH_DIR}/" "${MACOS_DIR}/"

cat > "${CONTENTS_DIR}/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>PigeonPost</string>
    <key>CFBundleIdentifier</key>
    <string>com.pigeonpost.vpnclient</string>
    <key>CFBundleName</key>
    <string>PigeonPost VPN Client</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
</dict>
</plist>
EOF

echo "macOS .app bundle created at: ${BUNDLE_DIR}"
