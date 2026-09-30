#!/usr/bin/env python3
"""Publish only the remote Addressables paired with an exported iOS player.

The default is a read-only plan. Published release paths are immutable: a
second, different full build must use a new project version and URL.
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parent / 'Validation'))
from verify_ios_addressables_pair import digest, player_catalog_url, verify


def aws(profile, *arguments):
    return subprocess.run(['aws', *arguments, '--profile', profile],
                          capture_output=True, text=True)


def destination(url):
    parsed = urlsplit(url)
    match = re.fullmatch(r'([a-z0-9.-]+)\.s3\.[a-z0-9-]+\.amazonaws\.com', parsed.hostname or '')
    if parsed.scheme != 'https' or not match or parsed.query or parsed.fragment:
        raise ValueError('Player catalog must use the expected HTTPS S3 endpoint')
    key = parsed.path.lstrip('/')
    if not re.fullmatch(r'release/v/\d+\.\d+\.\d+/iOS/catalog_[A-Za-z0-9._-]+\.hash', key):
        raise ValueError('Only a versioned release iOS destination can be published')
    return match.group(1), key.rsplit('/', 1)[0], key.rsplit('/', 1)[1]


def remote_bytes(profile, bucket, key):
    with tempfile.TemporaryDirectory(prefix='pocketstriker-catalog-') as directory:
        path = Path(directory) / 'object'
        result = aws(profile, 's3api', 'get-object', '--bucket', bucket, '--key', key, str(path))
        if result.returncode:
            if re.search(r'\((?:NoSuchKey|404)\)', result.stderr):
                return None
            raise ValueError(f'Cannot read s3://{bucket}/{key}: {result.stderr.strip()}')
        return path.read_bytes()


def publish(player_aa, server_dir, profile, write=False):
    verify(player_aa, server_dir, False, None)
    bucket, prefix, hash_name = destination(player_catalog_url(player_aa))
    local_hash = (server_dir / hash_name).read_bytes().strip()
    catalog = next(path for path in server_dir.iterdir()
                   if path.name in (hash_name[:-5] + '.bin', hash_name[:-5] + '.json'))
    bundles = sorted(server_dir.glob('*.bundle'))
    ordered = bundles + [catalog, server_dir / hash_name]
    plan = {'catalogHash': local_hash.decode('ascii'), 'destination': f's3://{bucket}/{prefix}/',
            'files': {p.name: {'size': p.stat().st_size, 'sha256': digest(p)} for p in ordered}}
    claim_name = '.addressables-player-manifest.json'
    claim_bytes = (json.dumps(plan, sort_keys=True, indent=2) + '\n').encode('utf-8')
    # Check before any write, even when a previous build used a different catalog name.
    result = aws(profile, 's3api', 'list-objects-v2', '--bucket', bucket, '--prefix', prefix + '/')
    if result.returncode:
        raise ValueError(f'Cannot inspect release destination: {result.stderr.strip()}')
    listing = json.loads(result.stdout)
    if listing.get('IsTruncated'):
        raise ValueError('Release destination listing is incomplete; publication cannot be verified')
    keys = [entry['Key'] for entry in listing.get('Contents', [])]
    hashes = [key for key in keys if re.fullmatch(r'catalog_[A-Za-z0-9._-]+\.hash', key.rsplit('/', 1)[-1])]
    for key in hashes:
        existing = remote_bytes(profile, bucket, key)
        if key != prefix + '/' + hash_name or existing is None or existing.strip() != local_hash:
            raise ValueError('This release URL already contains a different player build. '
                             'Increase the project version in MCombat/Version Sync and rebuild; '
                             'a full build must not replace assets used by installed players.')
    existing_catalog = remote_bytes(profile, bucket, prefix + '/' + catalog.name)
    if existing_catalog is not None and existing_catalog != catalog.read_bytes():
        raise ValueError('Existing catalog bytes differ; use a new project version')
    if keys:
        # A retry must contain exactly this payload, including already uploaded bundles.
        for key in keys:
            relative = key[len(prefix) + 1:]
            if relative == claim_name:
                if remote_bytes(profile, bucket, key) != claim_bytes:
                    raise ValueError('Release destination is reserved for a different build; use a new version')
                continue
            local = server_dir / relative
            if '/' in relative or not local.is_file() or not (relative.endswith('.bundle') or relative in (catalog.name, hash_name)):
                raise ValueError('Release destination contains unrecognized unfinished content')
            existing = remote_bytes(profile, bucket, key)
            if existing != local.read_bytes():
                raise ValueError('Unfinished release belongs to a different build; use a new version')
    print(f"{'Publish' if write else 'Read-only plan'}: {len(bundles)} bundles, {plan['destination']}, catalog {plan['catalogHash']}")
    if not write:
        return plan
    # Atomically reserve the version for this payload across concurrent publishers.
    # https://docs.aws.amazon.com/AmazonS3/latest/userguide/conditional-writes.html
    with tempfile.TemporaryDirectory(prefix='pocketstriker-publication-') as directory:
        claim_path = Path(directory) / 'manifest.json'
        claim_path.write_bytes(claim_bytes)
        result = aws(profile, 's3api', 'put-object', '--bucket', bucket, '--key', prefix + '/' + claim_name,
                     '--body', str(claim_path), '--if-none-match', '*', '--content-type', 'application/json')
        if result.returncode:
            if not re.search(r'\((?:PreconditionFailed|412)\)', result.stderr):
                raise ValueError(f'Cannot reserve release destination: {result.stderr.strip()}')
            if remote_bytes(profile, bucket, prefix + '/' + claim_name) != claim_bytes:
                raise ValueError('Another publisher reserved this release for a different build; use a new version')
    # Upload the catalog hash last so clients discover it after its dependencies.
    for path in ordered:
        cache = 'public,max-age=31536000,immutable' if path.suffix == '.bundle' else 'no-cache'
        result = aws(profile, 's3', 'cp', str(path), f's3://{bucket}/{prefix}/{path.name}',
                     '--cache-control', cache, '--only-show-errors')
        if result.returncode:
            raise ValueError(f'Upload failed for {path.name}: {result.stderr.strip()}')
    published_hash = remote_bytes(profile, bucket, prefix + '/' + hash_name)
    if published_hash is None or published_hash.strip() != local_hash:
        raise ValueError('Published catalog hash did not match this player')
    if remote_bytes(profile, bucket, prefix + '/' + catalog.name) != catalog.read_bytes():
        raise ValueError('Published catalog bytes did not match this player')
    print('Published catalog verified against the exported player')
    return plan


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player-aa', required=True, type=Path)
    parser.add_argument('--server-dir', required=True, type=Path)
    parser.add_argument('--aws-profile', required=True)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--dry-run', action='store_true', help='Read-only plan (default)')
    modes.add_argument('--publish', action='store_true', help='Upload the paired build to its versioned URL')
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9_-]+', args.aws_profile):
        parser.error('Invalid AWS profile name')
    try:
        publish(args.player_aa, args.server_dir, args.aws_profile, args.publish)
    except (OSError, ValueError, StopIteration) as error:
        print(f'Addressables publication stopped: {error}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
