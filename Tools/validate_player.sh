#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
player="$project_root/Builds/Revival/PocketStriker.app/Contents/MacOS/PocketStriker"
if [[ ! -x "$player" ]]; then
  echo "Build the explicit macOS fallback first: Tools/validate_unity.sh build mac" >&2
  exit 2
fi
mkdir -p "$project_root/Logs/Revival"
exec "$player" -pocketstriker-smoke \
  -smokeReport "$project_root/Logs/Revival/player-report.json" \
  -screen-fullscreen 0 -screen-width 540 -screen-height 960 \
  -logFile "$project_root/Logs/Revival/player.log"
