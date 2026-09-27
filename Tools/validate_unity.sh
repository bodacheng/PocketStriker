#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
editor_version="$(sed -n 's/^m_EditorVersion: //p' "$project_root/ProjectSettings/ProjectVersion.txt")"
editor="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
mode="${1:-check}"
case "$mode" in
  check) method=PocketStrikerValidation.CheckProject ;;
  startup) method=PocketStrikerValidation.SmokeStartup ;;
  build) method=PocketStrikerValidation.BuildMac ;;
  compile) method=PocketStrikerValidation.CompilePlayer ;;
  *) echo "Usage: $0 [check|startup|compile|build]" >&2; exit 2 ;;
esac
if [[ ! -x "$editor" ]]; then
  echo "Unity $editor_version not found. Set UNITY_EDITOR_PATH to the editor executable." >&2
  exit 2
fi
mkdir -p "$project_root/Logs/Revival"
editor_args=(-batchmode -projectPath "$project_root"
  -buildTarget StandaloneOSX -executeMethod "$method"
  -logFile "$project_root/Logs/Revival/$mode.log")
if [[ "$mode" != startup ]]; then editor_args+=(-quit); fi
exec "$editor" "${editor_args[@]}"
