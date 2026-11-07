#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(dirname "$0")"
dotnet publish "$SCRIPT_DIR/../../src/PigeonPost.VpnClientView.Android/PigeonPost.VpnClientView.Android.csproj" \
  -c Debug -f net10.0-android \
  -o "$SCRIPT_DIR/../../artifacts/android-debug"

echo "Android debug APK published to artifacts/android-debug/"
