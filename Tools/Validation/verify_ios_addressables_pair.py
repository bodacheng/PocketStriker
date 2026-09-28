#!/usr/bin/env python3
"""Check that an iOS player and its remote Addressables came from one build."""

import argparse
import hashlib
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path


def digest(path):
    checksum = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            checksum.update(chunk)
    return checksum.hexdigest()


def read_remote(url):
    with urllib.request.urlopen(url, timeout=20) as response:
        return response.read()


def verify(player_aa, server_dir, check_remote, manifest_path):
    settings_path = player_aa / "settings.json"
    local_hash_path = player_aa / "catalog.hash"
    remote_hash_path = server_dir / "catalog_v2.hash"
    remote_catalog_path = server_dir / "catalog_v2.bin"
    for path in (settings_path, local_hash_path, remote_hash_path, remote_catalog_path):
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f"Required Addressables file is missing or empty: {path}")

    settings = json.loads(settings_path.read_text(encoding="utf-8"))
    remote_urls = [
        location["m_InternalId"]
        for location in settings.get("m_CatalogLocations", [])
        if "AddressablesMainContentCatalogRemoteHash" in location.get("m_Keys", [])
    ]
    if len(remote_urls) != 1 or not remote_urls[0].startswith("https://"):
        raise ValueError("Player must contain exactly one HTTPS remote catalog hash URL")
    remote_url = remote_urls[0]
    if not remote_url.endswith("/catalog_v2.hash"):
        raise ValueError(f"Unexpected remote catalog URL: {remote_url}")

    player_hash = local_hash_path.read_text(encoding="ascii").strip()
    server_hash = remote_hash_path.read_text(encoding="ascii").strip()
    if len(player_hash) != 32 or player_hash != server_hash:
        raise ValueError(
            f"Player catalog {player_hash} does not match this build's remote catalog {server_hash}"
        )

    files = sorted(path for path in server_dir.iterdir() if path.is_file())
    bundles = [path for path in files if path.suffix == ".bundle"]
    if not bundles:
        raise ValueError(f"No remote bundles found in {server_dir}")

    if check_remote:
        try:
            published_hash = read_remote(remote_url).decode("ascii").strip()
            published_catalog = read_remote(remote_url[:-5] + ".bin")
        except (urllib.error.URLError, UnicodeError) as error:
            raise ValueError(f"Could not read published catalog: {error}") from error
        if published_hash != player_hash:
            raise ValueError(
                f"Published catalog {published_hash} does not match player catalog {player_hash}"
            )
        if hashlib.sha256(published_catalog).hexdigest() != digest(remote_catalog_path):
            raise ValueError("Published catalog bytes differ from this player's remote catalog")

    if manifest_path is not None:
        manifest = {
            "playerCatalogHash": player_hash,
            "remoteCatalogHashUrl": remote_url,
            "files": {
                path.name: {"size": path.stat().st_size, "sha256": digest(path)}
                for path in files
            },
        }
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print(f"Addressables pair verified: {player_hash}, {len(bundles)} remote bundles")
    if check_remote:
        print("Published catalog hash and bytes match the player build")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player-aa", type=Path, required=True)
    parser.add_argument("--server-dir", type=Path, required=True)
    parser.add_argument("--check-remote", action="store_true")
    parser.add_argument("--manifest", type=Path)
    args = parser.parse_args()
    try:
        verify(args.player_aa, args.server_dir, args.check_remote, args.manifest)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"Addressables pair check failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
