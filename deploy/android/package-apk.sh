#!/usr/bin/env bash
set -euo pipefail

dotnet publish src/PigeonPost.VpnClientView.Android/PigeonPost.VpnClientView.Android.csproj \
  -c Debug -f net10.0-android \
  -o "$(dirname "$0")/../../artifacts/android-debug"

echo "Android debug APK published to artifacts/android-debug/"
