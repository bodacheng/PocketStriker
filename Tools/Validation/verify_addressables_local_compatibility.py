#!/usr/bin/env python3
"""Check whether published Addressables bootstrap content fits an existing player.

Only local bundles and the generated linker are compared. Catalogs and their
hashes may change during a compatible remote content update. This command reads
local files only and never downloads, extracts, or modifies content.
"""

import argparse
import hashlib
from pathlib import Path, PurePosixPath
import stat
import sys
import zipfile


LINKER = "AddressablesLink/link.xml"


def digest(stream):
    checksum = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        checksum.update(chunk)
    return checksum.hexdigest()


def bootstrap_files(archive):
    files = {}
    seen = set()
    for entry in archive.infolist():
        path = PurePosixPath(entry.filename)
        if (not entry.filename or path.is_absolute() or "\\" in entry.filename
                or ":" in entry.filename or ".." in path.parts
                or entry.filename.rstrip("/") != path.as_posix()):
            raise ValueError(f"Invalid bootstrap archive path: {entry.filename!r}")
        if entry.filename in seen:
            raise ValueError(f"Duplicate bootstrap archive path: {entry.filename}")
        seen.add(entry.filename)
        if stat.S_ISLNK(entry.external_attr >> 16):
            raise ValueError(f"Bootstrap archive contains a symlink: {entry.filename}")
        if not entry.is_dir() and (entry.filename.endswith(".bundle") or entry.filename == LINKER):
            if entry.file_size == 0:
                raise ValueError(f"Bootstrap local content is empty: {entry.filename}")
            with archive.open(entry) as stream:
                files[entry.filename] = digest(stream)
    if LINKER not in files:
        raise ValueError(f"Bootstrap archive is missing {LINKER}")
    if not any(name.endswith(".bundle") for name in files):
        raise ValueError("Bootstrap archive contains no local bundles")
    return files


def player_files(player_aa):
    if not player_aa.is_dir() or player_aa.is_symlink():
        raise ValueError(f"Player Addressables directory is missing or a symlink: {player_aa}")
    files = {}
    for path in sorted(player_aa.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"Player Addressables content contains a symlink: {path}")
        name = path.relative_to(player_aa).as_posix()
        if path.is_file() and (path.suffix == ".bundle" or name == LINKER):
            if path.stat().st_size == 0:
                raise ValueError(f"Player local content is empty: {name}")
            with path.open("rb") as stream:
                files[name] = digest(stream)
    if LINKER not in files:
        raise ValueError(f"Player Addressables directory is missing {LINKER}")
    if not any(name.endswith(".bundle") for name in files):
        raise ValueError("Player Addressables directory contains no local bundles")
    return files


def verify(player_aa, bootstrap_path):
    """Raise on local payload changes, regardless of catalog/hash differences."""
    installed = player_files(Path(player_aa))
    with zipfile.ZipFile(bootstrap_path) as archive:
        published = bootstrap_files(archive)
    missing = sorted(published.keys() - installed.keys())
    removed = sorted(installed.keys() - published.keys())
    changed = sorted(name for name in installed.keys() & published.keys()
                     if installed[name] != published[name])
    differences = []
    if missing:
        differences.append("missing from player: " + ", ".join(missing))
    if removed:
        differences.append("absent from bootstrap: " + ", ".join(removed))
    if changed:
        differences.append("different bytes: " + ", ".join(changed))
    if differences:
        raise ValueError("Incompatible local Addressables content; " + "; ".join(differences)
                         + ". Rebuild the player with the matching bootstrap.")
    return len(installed) - 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player-aa", type=Path, required=True,
                        help="Installed/exported player's Data/Raw/aa directory")
    parser.add_argument("--bootstrap", type=Path, required=True,
                        help="Published candidate player-bootstrap.zip")
    args = parser.parse_args()
    try:
        count = verify(args.player_aa, args.bootstrap)
    except (OSError, ValueError, zipfile.BadZipFile, RuntimeError) as error:
        print(f"Addressables local compatibility check failed: {error}", file=sys.stderr)
        return 1
    print(f"Addressables local content compatible: {count} local bundles and linker match")
    return 0


if __name__ == "__main__":
    sys.exit(main())
