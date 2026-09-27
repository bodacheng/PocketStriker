#!/usr/bin/env python3
"""Review or update this project's embedded MCombatShared snapshot only."""

import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import subprocess
import tarfile


PROJECT = Path(__file__).resolve().parents[1]
PACKAGE = PROJECT / "Packages/com.mcombat.shared"
LOCK = PROJECT / "Tools/mcombat-shared-lock.json"


def git(source, *args):
    return subprocess.check_output(["git", "-C", str(source), *args])


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Local MCombatShared git checkout")
    parser.add_argument("--revision", help="Commit/ref to import; defaults to the recorded commit")
    parser.add_argument("--apply", action="store_true", help="Apply the reviewed package update")
    args = parser.parse_args()
    previous = json.loads(LOCK.read_text()) if LOCK.exists() else {}
    revision = args.revision or previous.get("commit")
    if not revision:
        parser.error("the first import requires --revision")
    commit = git(args.source, "rev-parse", "--verify", revision + "^{commit}").decode().strip()
    snapshot = {}
    executable = set()
    with tarfile.open(fileobj=io.BytesIO(git(args.source, "archive", commit))) as archive:
        for member in archive:
            if member.isdir():
                continue
            path = PurePosixPath(member.name)
            if not member.isfile() or path.is_absolute() or ".." in path.parts:
                raise SystemExit("Unsupported archive entry: " + member.name)
            snapshot[member.name] = archive.extractfile(member).read()
            if member.mode & 0o111:
                executable.add(member.name)
    package = json.loads(snapshot["package.json"])
    if package["name"] != "com.mcombat.shared":
        raise SystemExit("Source is not the MCombatShared package")

    existing = {p.relative_to(PACKAGE).as_posix(): p for p in PACKAGE.rglob("*") if p.is_file()}
    additions = sorted(set(snapshot) - set(existing))
    changes = sorted(p for p in snapshot.keys() & existing.keys()
                     if snapshot[p] != existing[p].read_bytes()
                     or bool(existing[p].stat().st_mode & 0o111) != (p in executable))
    removals = sorted(set(existing) - set(snapshot))
    print("MCombatShared", commit, "version", package["version"])
    print(f"Package changes: {len(additions)} added, {len(changes)} changed, {len(removals)} removed")
    for kind, paths in [("ADD", additions), ("UPDATE", changes), ("REMOVE", removals)]:
        for path in paths:
            print(kind, path)
    if not args.apply:
        print("Review only. Use --apply to update this project's package; Assets are never changed.")
        return

    # Never silently discard local package edits or unknown files during an update.
    recorded = previous.get("files", {})
    for path in changes + removals:
        if previous and digest(existing[path].read_bytes()) != recorded.get(path):
            raise SystemExit("Local package edit must be reconciled first: " + path)
        if "executable" in previous and bool(existing[path].stat().st_mode & 0o111) != (path in previous["executable"]):
            raise SystemExit("Local package mode change must be reconciled first: " + path)
        if not previous and path in removals:
            raise SystemExit("Untracked package file must be reviewed first: " + path)
    for path in additions + changes:
        destination = PACKAGE / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(snapshot[path])
        destination.chmod(0o755 if path in executable else 0o644)
    for path in removals:
        existing[path].unlink()
    for path in sorted(PACKAGE.rglob("*"), key=lambda p: len(p.parts), reverse=True):
        if path.is_dir() and not any(path.iterdir()):
            path.rmdir()
    LOCK.write_text(json.dumps({
        "repository": "https://github.com/bodacheng/MCombatShared.git",
        "commit": commit,
        "version": package["version"],
        "executable": sorted(executable),
        "files": {path: digest(data) for path, data in sorted(snapshot.items())},
    }, indent=2) + "\n")
    print("Updated embedded package and provenance lock. Review Assets API migrations separately.")


if __name__ == "__main__":
    main()
