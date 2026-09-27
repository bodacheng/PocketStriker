#!/usr/bin/env bash
set -euo pipefail

shared_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mcombat_project="${MCOMBAT_PROJECT:-/Users/daisei/MComat}"
pocket_project="${POCKET_PROJECT:-/Users/daisei/PocketStriker}"

python3 - "$shared_root" "$mcombat_project" "$pocket_project" <<'PY'
from pathlib import Path
import filecmp
import json
import sys

shared_root = Path(sys.argv[1])
projects = [Path(p) for p in sys.argv[2:]]
source_sync_root = shared_root / "SourceSync~"
manifest = json.loads((source_sync_root / "manifest.json").read_text())
errors = []

def is_managed_file(path):
    return path.is_file() and path.suffix != ".meta" and path.name != ".DS_Store"

def is_excluded(rel, excluded):
    return rel in excluded or any(rel.startswith(f"{item.rstrip('/')}/") for item in excluded)

def managed_files(root):
    return {
        p.relative_to(root).as_posix(): p
        for p in root.rglob("*")
        if is_managed_file(p)
    }

for root in manifest["roots"]:
    asset_root = root["path"]
    excluded = set(root.get("exclude", ()))
    source_root = source_sync_root / asset_root
    source_files = managed_files(source_root)

    for rel in excluded:
        if rel in source_files:
            errors.append(f"excluded file must stay project-local: {asset_root}/{rel}")

    for project in projects:
        project_root = project / "Assets" / asset_root
        project_files = {
            p.relative_to(project_root).as_posix(): p
            for p in project_root.rglob("*")
            if is_managed_file(p)
            and not is_excluded(p.relative_to(project_root).as_posix(), excluded)
        }

        missing_from_source = sorted(set(project_files) - set(source_files))
        for rel in missing_from_source:
            errors.append(f"{project}: project {asset_root} file is not SourceSync-managed: {rel}")

        for rel, src in source_files.items():
            dst = project_root / rel
            if not dst.exists():
                errors.append(f"{project}: missing synced {asset_root} file: {rel}")
            elif not filecmp.cmp(src, dst, shallow=False):
                errors.append(f"{project}: synced {asset_root} file differs: {rel}")

for rel in manifest["files"]:
    src = source_sync_root / rel
    if not src.exists():
        errors.append(f"missing SourceSync file: {rel}")
        continue
    for project in projects:
        dst = project / "Assets" / rel
        if not dst.exists():
            errors.append(f"{project}: missing synced file: {rel}")
        elif not filecmp.cmp(src, dst, shallow=False):
            errors.append(f"{project}: synced file differs: {rel}")

for project in projects:
    if (project / "Packages" / "com.mcombat.shared" / "SourceSync").exists():
        errors.append(f"{project}: embedded package must not contain SourceSync")

if errors:
    print("SourceSync verification failed:")
    for error in errors:
        print(f"  {error}")
    sys.exit(1)

print("SourceSync asset roots match both projects.")
PY

for project in "$mcombat_project" "$pocket_project"; do
  package_dir="$project/Packages/com.mcombat.shared"
  if [[ "$(cd "$package_dir" && pwd -P)" == "$(cd "$shared_root" && pwd -P)" ]]; then
    continue
  fi
  diff -qr --exclude .git --exclude SourceSync --exclude 'SourceSync~' "$shared_root" "$package_dir" >/dev/null
done

echo "Embedded com.mcombat.shared packages match shared root."
