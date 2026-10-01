#!/usr/bin/env python3
"""Package Addressables runtime data for inspecting historical player artifacts.

The current player builds its own runtime data; neither Jenkins job uses this
archive. It remains available for comparing builds from the former bootstrap flow.
This command uses local files only; it never uploads or reserves a release.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import tempfile
from urllib.parse import urlsplit
import xml.etree.ElementTree as ET
import zipfile


ARCHIVE_NAME = "player-bootstrap.zip"
REMOTE_HASH_KEY = "AddressablesMainContentCatalogRemoteHash"
CATALOG_KEY = "AddressablesMainContentCatalog"
RUNTIME_PATH = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}"


def required_file(path):
    if path.is_symlink() or not path.is_file() or path.stat().st_size == 0:
        raise ValueError(f"Required Addressables file is missing, empty, or a symlink: {path}")
    return path


def digest(path):
    checksum = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            checksum.update(chunk)
    return checksum.digest()


def verify_runtime(runtime_dir, server_dir):
    """Verify runtime/catalog pairing and return the files to archive."""
    settings = json.loads(required_file(runtime_dir / "settings.json").read_text(encoding="utf-8"))
    if not isinstance(settings, dict):
        raise ValueError("Addressables settings.json must contain an object")
    locations = settings.get("m_CatalogLocations")
    if not isinstance(locations, list) or not all(isinstance(location, dict) for location in locations):
        raise ValueError("Addressables settings must contain catalog locations")
    remote = [location for location in locations if REMOTE_HASH_KEY in location.get("m_Keys", [])]
    main = [location for location in locations if CATALOG_KEY in location.get("m_Keys", [])]
    if len(remote) != 1 or len(main) != 1:
        raise ValueError("Runtime data must contain exactly one main catalog and one remote catalog hash URL")
    remote_url = remote[0].get("m_InternalId", "")
    if not isinstance(remote_url, str):
        raise ValueError("Remote catalog hash URL must be a string")
    parsed = urlsplit(remote_url)
    hash_name = parsed.path.rsplit("/", 1)[-1]
    if (parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password
            or parsed.query or parsed.fragment or not re.fullmatch(r"catalog_[A-Za-z0-9._-]+\.hash", hash_name)):
        raise ValueError(f"Expected an HTTPS remote catalog hash URL: {remote_url}")
    build_target = settings.get("m_buildTarget")
    if not isinstance(build_target, str) or not build_target or parsed.path.split("/")[-2] != build_target:
        raise ValueError("Remote catalog hash URL platform must match the runtime build target")
    if REMOTE_HASH_KEY not in main[0].get("m_Dependencies", []):
        raise ValueError("The main catalog must depend on the remote catalog hash")

    server_catalogs = [server_dir / (hash_name[:-5] + extension) for extension in (".bin", ".json")]
    server_catalogs = [path for path in server_catalogs if path.exists()]
    local_catalogs = [runtime_dir / ("catalog" + extension) for extension in (".bin", ".json")]
    local_catalogs = [path for path in local_catalogs if path.exists()]
    if len(server_catalogs) != 1 or len(local_catalogs) != 1:
        raise ValueError("Expected exactly one local catalog and one matching server catalog")
    server_catalog = required_file(server_catalogs[0])
    local_catalog = required_file(local_catalogs[0])
    if main[0].get("m_InternalId") != f"{RUNTIME_PATH}/{local_catalog.name}":
        raise ValueError("The main catalog location must point to the packaged local catalog")
    if local_catalog.suffix != server_catalog.suffix or digest(local_catalog) != digest(server_catalog):
        raise ValueError("Runtime catalog bytes differ from the independent server catalog")
    local_hash = required_file(runtime_dir / "catalog.hash").read_bytes()
    server_hash = required_file(server_dir / hash_name).read_bytes()
    if not re.fullmatch(rb"[0-9a-fA-F]{32}", local_hash.strip()):
        raise ValueError("Runtime catalog.hash must contain a 32-digit hexadecimal hash")
    if local_hash != server_hash:
        raise ValueError("Runtime catalog hash bytes differ from the independent server catalog hash")

    link = required_file(runtime_dir / "AddressablesLink" / "link.xml")
    if ET.parse(link).getroot().tag != "linker":
        raise ValueError("AddressablesLink/link.xml must have a linker root")
    bundles = sorted(server_dir.rglob("*.bundle"))
    if not bundles:
        raise ValueError("Independent server output contains no bundles")
    for bundle in bundles:
        required_file(bundle)

    files = []
    for path in sorted(runtime_dir.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"Runtime data must not contain symlinks: {path}")
        if path.is_dir():
            continue
        required_file(path)
        relative = path.relative_to(runtime_dir).as_posix()
        if "\\" in relative or ":" in relative:
            raise ValueError(f"Runtime archive path is not portable: {relative}")
        files.append(path)
    return files


def package(runtime_dir, server_dir):
    runtime_dir, server_dir = Path(runtime_dir).resolve(), Path(server_dir).resolve()
    if not runtime_dir.is_dir() or not server_dir.is_dir():
        raise ValueError("Runtime and server directories must already exist")
    if server_dir == runtime_dir or runtime_dir in server_dir.parents:
        raise ValueError("Server output must be outside the runtime directory")
    files = verify_runtime(runtime_dir, server_dir)
    archive = server_dir / ARCHIVE_NAME
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=".player-bootstrap-", suffix=".zip", dir=server_dir, delete=False) as stream:
            temporary = Path(stream.name)
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED) as output:
            for path in files:
                output.write(path, path.relative_to(runtime_dir).as_posix())
        os.replace(temporary, archive)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()
    return archive, len(files)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime-dir", type=Path, required=True)
    parser.add_argument("--server-dir", type=Path, required=True)
    args = parser.parse_args()
    try:
        archive, count = package(args.runtime_dir, args.server_dir)
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"Addressables bootstrap packaging failed: {error}", file=sys.stderr)
        return 1
    print(f"Addressables bootstrap packaged: {archive} ({count} files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
