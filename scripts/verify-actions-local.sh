#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if ! command -v act >/dev/null 2>&1; then
  echo "act is not installed or not available in PATH" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is not installed or not available in PATH" >&2
  exit 1
fi

act_image="${ACT_IMAGE:-local/act-micro:latest}"

docker build -f scripts/docker/act-micro.Dockerfile -t "$act_image" .

chmod +x scripts/verify-ci.sh scripts/verify-pack.sh

bash scripts/verify-ci.sh
bash scripts/verify-pack.sh
act pull_request -W .github/workflows/ci.yml -P ubuntu-latest="$act_image" --pull=false
act workflow_dispatch -W .github/workflows/pack.yml -P ubuntu-latest="$act_image" --pull=false