#!/usr/bin/env python3
"""Review or advance the pinned MCombatShared Git submodule without editing Assets."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

PROJECT = Path(__file__).resolve().parents[1]
PACKAGE = PROJECT / "Packages/com.mcombat.shared"
LOCK = PROJECT / "Tools/mcombat-shared-lock.json"
REPOSITORY = "https://github.com/bodacheng/MCombatShared.git"


def git(source, *args):
    return subprocess.check_output(["git", "-C", str(source), *args])


def snapshot(source, commit):
    files = {}
    executable = []
    for entry in git(source, "ls-tree", "-r", "-z", commit).split(b"\0"):
        if not entry:
            continue
        info, raw_path = entry.split(b"\t", 1)
        mode, kind, object_id = info.decode().split()
        path = raw_path.decode()
        if kind != "blob" or mode not in ("100644", "100755"):
            raise SystemExit("Unsupported upstream entry: " + path)
        files[path] = hashlib.sha256(git(source, "cat-file", "blob", object_id)).hexdigest()
        if mode == "100755":
            executable.append(path)
    package = json.loads(git(source, "show", commit + ":package.json"))
    if package["name"] != "com.mcombat.shared":
        raise SystemExit("Source is not the MCombatShared package")
    return {"repository": REPOSITORY, "commit": commit, "version": package["version"],
            "executable": sorted(executable), "files": dict(sorted(files.items()))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, help="Optional local upstream checkout; defaults to this submodule")
    parser.add_argument("--revision", help="Commit/ref to review; defaults to the recorded commit")
    parser.add_argument("--apply", action="store_true", help="Check out the reviewed commit and update the provenance lock")
    args = parser.parse_args()
    if not (PACKAGE / ".git").is_file():
        raise SystemExit("Initialize the package first: git submodule update --init --recursive")
    root = git(PACKAGE, "rev-parse", "--show-superproject-working-tree").decode().strip()
    if Path(root).resolve() != PROJECT.resolve():
        raise SystemExit("Package is not a submodule of this project")
    previous = json.loads(LOCK.read_text())
    source = args.source or PACKAGE
    commit = git(source, "rev-parse", "--verify", (args.revision or previous["commit"]) + "^{commit}").decode().strip()
    current = git(PACKAGE, "rev-parse", "HEAD").decode().strip()
    package = json.loads(git(source, "show", commit + ":package.json"))
    if package["name"] != "com.mcombat.shared":
        raise SystemExit("Source is not the MCombatShared package")
    print(f"MCombatShared {current} -> {commit} (version {package['version']})")
    if current != previous["commit"]:
        raise SystemExit("Submodule HEAD differs from the provenance lock; reconcile it before updating")
    dirty = git(PACKAGE, "status", "--porcelain", "--untracked-files=all").decode().strip()
    if dirty:
        print(dirty)
        if args.apply:
            raise SystemExit("Local package edit must be reconciled first")
    proposed = snapshot(source, commit)
    old_files = previous["files"]
    for path in sorted(old_files.keys() | proposed["files"].keys()):
        if old_files.get(path) != proposed["files"].get(path):
            print("UPDATE" if path in old_files and path in proposed["files"] else
                  "ADD" if path in proposed["files"] else "REMOVE", path)
    if not args.apply:
        print("Review only. Use --apply to update this project's submodule; Assets are never changed.")
        return
    if args.source:
        git(PACKAGE, "fetch", "--no-tags", str(source.resolve()), commit)
    git(PACKAGE, "checkout", "--detach", commit)
    LOCK.write_text(json.dumps(proposed, indent=2) + "\n")
    print("Updated submodule and provenance lock. Review Assets migrations, then stage both files:")
    print("git add Packages/com.mcombat.shared Tools/mcombat-shared-lock.json")


if __name__ == "__main__":
    main()
