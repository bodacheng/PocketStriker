#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
editor_version="$(sed -n 's/^m_EditorVersion: //p' "$project_root/ProjectSettings/ProjectVersion.txt")"
editor="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
mode="${1:-check}"
platform="${2:-ios}"
case "$platform" in
  ios|iOS) target=iOS; platform=ios; build_method=PocketStrikerValidation.BuildIOS ;;
  mac|macos|StandaloneOSX) target=StandaloneOSX; platform=mac; build_method=PocketStrikerValidation.BuildMac ;;
  *) echo "Usage: $0 [check|startup|compile|build] [ios|mac] (default: ios)" >&2; exit 2 ;;
esac
case "$mode" in
  check) method=PocketStrikerValidation.CheckProject ;;
  startup) method=PocketStrikerValidation.SmokeStartup ;;
  build) method="$build_method" ;;
  compile) method=PocketStrikerValidation.CompilePlayer ;;
  *) echo "Usage: $0 [check|startup|compile|build] [ios|mac] (default: ios)" >&2; exit 2 ;;
esac
if [[ $# -gt 2 ]]; then
  echo "Usage: $0 [check|startup|compile|build] [ios|mac] (default: ios)" >&2
  exit 2
fi
if [[ ! -x "$editor" ]]; then
  echo "Unity $editor_version not found. Set UNITY_EDITOR_PATH to the editor executable." >&2
  exit 2
fi
mkdir -p "$project_root/Logs/Revival"
editor_args=(-batchmode -projectPath "$project_root"
  -buildTarget "$target" -executeMethod "$method"
  -logFile "$project_root/Logs/Revival/$mode-$platform.log")
if [[ "$mode" != startup ]]; then editor_args+=(-quit); fi
echo "Validating Unity $editor_version, $mode, target $target (portrait)."
exec "$editor" "${editor_args[@]}"
