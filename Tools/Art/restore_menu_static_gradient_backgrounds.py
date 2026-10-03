"""Verify or restore the complete 28-file state before static gradient integration.

The default and --verify only read files. --restore explicitly restores the
captured pre-integration state, including existing user modifications. All
backups and current files must pass the pinned hash checks before any write.
New gradient shader/material/source assets are deliberately retained.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MANIFEST_RELATIVE = "Tools/ArtSources/MenuBackgrounds/StaticPixelGradient-v1/integration-backup/manifest.json"
EXPECTED_FILE_COUNT = 28


def digest(data):
    return hashlib.sha256(data).hexdigest()


def scoped_path(relative):
    path = Path(relative)
    if path.is_absolute() or ".." in path.parts:
        raise ValueError(f"Unsafe manifest path: {relative}")
    absolute = ROOT / path
    if not absolute.resolve().is_relative_to(ROOT.resolve()):
        raise ValueError(f"Manifest path escapes workspace: {relative}")
    return absolute


def verified_plan():
    manifest = json.loads((ROOT / MANIFEST_RELATIVE).read_text())
    files = manifest["files"]
    if len(files) != EXPECTED_FILE_COUNT or manifest["fileCount"] != EXPECTED_FILE_COUNT:
        raise ValueError("The full 28-file restore manifest is incomplete")
    if len({item["path"] for item in files}) != EXPECTED_FILE_COUNT:
        raise ValueError("Restore manifest contains duplicate destinations")
    plan = []
    for item in files:
        destination = scoped_path(item["path"])
        backup = scoped_path(item["backupPath"])
        if not backup.is_file() or not destination.is_file():
            raise ValueError(f"Required current file or backup is missing: {item['path']}")
        original = backup.read_bytes()
        if digest(original) != item["originalSha256"] or len(original) != item["originalBytes"]:
            raise ValueError(f"Backup differs from the original frozen file: {item['backupPath']}")
        current = destination.read_bytes()
        accepted = {item["originalSha256"], item["acceptedAfterSha256"]}
        if digest(current) not in accepted:
            raise ValueError(f"Current file has later or unregistered modifications; refusing to overwrite: {item['path']}")
        plan.append({"path": item["path"], "destination": destination,
                     "backup": backup, "original": original, "current": current,
                     "alreadyOriginal": current == original})
    return manifest, plan


def run(restore=False):
    manifest, plan = verified_plan()
    if restore:
        # A second complete read catches changes during initial verification.
        # Finish this loop before writing any destination.
        for item in plan:
            if item["destination"].read_bytes() != item["current"]:
                raise ValueError(f"Current file changed during preflight: {item['path']}")
            if item["backup"].read_bytes() != item["original"]:
                raise ValueError(f"Backup changed during preflight: {item['path']}")
        for item in plan:
            if not item["alreadyOriginal"]:
                item["destination"].write_bytes(item["original"])
        if any(item["destination"].read_bytes() != item["original"] for item in plan):
            raise ValueError("Restoration read-back verification failed")
    return {
        "utcTime": datetime.now(timezone.utc).isoformat(), "passed": True,
        "operation": "restore" if restore else "verify/dry-run",
        "productionWrites": restore,
        "fullFileCount": len(plan), "allOriginalBackupHashesValid": True,
        "allCurrentHashesAccepted": True,
        "alreadyOriginalFileCount": sum(p["alreadyOriginal"] for p in plan),
        "wouldChangeFileCount": sum(not p["alreadyOriginal"] for p in plan),
        "restoredFileCount": sum(not p["alreadyOriginal"] for p in plan) if restore else 0,
        "newShaderMaterialsAndSourceAssetsRetained": True,
        "manifest": MANIFEST_RELATIVE,
        "capturedAfterUtc": manifest["capturedAfterUtc"],
        "note": "Any later edit outside the registered before/after hashes blocks restore; review and register the intended final after hash before rerunning.",
        "files": [{"path": p["path"], "backupHashValid": True,
                   "currentHashAccepted": True, "alreadyOriginal": p["alreadyOriginal"]} for p in plan],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--verify", action="store_true", help="Read-only full verification (also the default)")
    action.add_argument("--restore", action="store_true", help="Explicitly restore all original files after full preflight")
    args = parser.parse_args()
    print(json.dumps(run(restore=args.restore), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
