#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

tools_dir="$(mktemp -d "${TMPDIR:-/tmp}/endpoint-probe-tools.XXXXXX")"

cleanup() {
  rm -rf "$tools_dir"
}
trap cleanup EXIT

dotnet restore --configfile NuGet.Config
dotnet pack src/EndpointProbe.Tool/EndpointProbe.Tool.csproj -c Release --no-restore -o ./nupkg
dotnet tool install EndpointProbe.Tool --tool-path "$tools_dir" --add-source ./nupkg
"$tools_dir/endpoint-probe"