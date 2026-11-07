#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

dotnet build src/PigeonPost.Ping/PigeonPost.Ping.csproj -c Release 1>&2

exec dotnet src/PigeonPost.Ping/bin/Release/net10.0/PigeonPost.Ping.dll "$@"
