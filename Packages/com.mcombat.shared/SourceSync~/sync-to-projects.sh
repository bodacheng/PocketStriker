#!/usr/bin/env bash
set -euo pipefail

shared_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mcombat_project="${MCOMBAT_PROJECT:-/Users/daisei/MComat}"
pocket_project="${POCKET_PROJECT:-/Users/daisei/PocketStriker}"

python3 - "$shared_root" "$mcombat_project" "$pocket_project" <<'PY'
from pathlib import Path
import json
import shutil
import sys

shared_root = Path(sys.argv[1])
projects = [Path(p) for p in sys.argv[2:]]
source_sync_root = shared_root / "SourceSync~"
manifest = json.loads((source_sync_root / "manifest.json").read_text())

def rel_posix(path, root):
    return path.relative_to(root).as_posix()

def is_global_excluded(path, skip_meta=True):
    return path.name == ".DS_Store" or (skip_meta and path.suffix == ".meta")

def is_excluded(rel, excluded):
    return rel in excluded or any(rel.startswith(f"{item.rstrip('/')}/") for item in excluded)

def copy_file(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)

def remove_path(path):
    if path.is_dir():
        shutil.rmtree(path)
    else:
        path.unlink()

def sync_tree(src_root, dst_root, excluded=(), preserve_excluded=True, skip_meta=True):
    excluded = set(excluded)
    src_files = set()

    dst_root.mkdir(parents=True, exist_ok=True)
    for src in src_root.rglob("*"):
        rel = rel_posix(src, src_root)
        if is_excluded(rel, excluded) or is_global_excluded(src, skip_meta):
            continue
        dst = dst_root / rel
        if src.is_dir():
            dst.mkdir(parents=True, exist_ok=True)
        elif src.is_file():
            src_files.add(rel)
            copy_file(src, dst)

    for dst in sorted(dst_root.rglob("*"), key=lambda p: len(p.parts), reverse=True):
        rel = rel_posix(dst, dst_root)
        if is_global_excluded(dst, skip_meta):
            continue
        if preserve_excluded and is_excluded(rel, excluded):
            continue
        if dst.is_file() and rel not in src_files:
            remove_path(dst)
        elif dst.is_dir():
            try:
                dst.rmdir()
            except OSError:
                pass

def sync_project(project):
    for root in manifest["roots"]:
        asset_root = root["path"]
        sync_tree(
            source_sync_root / asset_root,
            project / "Assets" / asset_root,
            root.get("exclude", ()),
            preserve_excluded=True,
        )

    for rel in manifest["files"]:
        copy_file(source_sync_root / rel, project / "Assets" / rel)

    for rel in manifest["obsolete"]:
        path = project / "Assets" / rel
        if path.exists():
            remove_path(path)

    package_dir = project / "Packages" / "com.mcombat.shared"
    if package_dir.resolve() != shared_root.resolve():
        sync_tree(
            shared_root,
            package_dir,
            {".git", "SourceSync~"},
            preserve_excluded=True,
            skip_meta=False,
        )
        legacy_source_sync = package_dir / "SourceSync"
        if legacy_source_sync.exists():
            remove_path(legacy_source_sync)

for project in projects:
    sync_project(project)

print("Synced SourceSync and com.mcombat.shared to:")
for project in projects:
    print(f"  {project}")
PY
