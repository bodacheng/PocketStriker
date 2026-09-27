#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export_path="$project_root/Builds/Revival/iOS"
if [[ ! -d "$export_path/Unity-iPhone.xcworkspace" ]]; then
  echo "Export iOS and resolve its CocoaPods dependencies first: Tools/validate_unity.sh build" >&2
  exit 2
fi
mkdir -p "$project_root/Logs/Revival"
echo "Compiling the local iOS export with Xcode, without code signing or installation."
exec xcodebuild -workspace "$export_path/Unity-iPhone.xcworkspace" \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -destination 'generic/platform=iOS' -jobs 4 \
  -derivedDataPath "$project_root/Library/RevivalXcode" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY= \
  DEVELOPMENT_TEAM= build > "$project_root/Logs/Revival/native-ios.log" 2>&1
