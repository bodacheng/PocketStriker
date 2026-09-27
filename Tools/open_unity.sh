#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
editor_version="$(sed -n 's/^m_EditorVersion: //p' "$project_root/ProjectSettings/ProjectVersion.txt")"
editor="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
case "${1:-ios}" in
  ios|iOS) target=iOS ;;
  mac|macos|StandaloneOSX) target=StandaloneOSX ;;
  *) echo "Usage: $0 [ios|mac] (default: ios)" >&2; exit 2 ;;
esac
if [[ $# -gt 1 || ! -x "$editor" ]]; then
  echo "Unity $editor_version is required. Set UNITY_EDITOR_PATH if installed elsewhere." >&2
  exit 2
fi
exec "$editor" -projectPath "$project_root" -buildTarget "$target"
