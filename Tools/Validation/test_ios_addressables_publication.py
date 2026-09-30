"""Exercise paired iOS publication with temporary files and an in-memory S3.

No AWS process or network request is executed by these tests.
Run: python3 -m unittest discover -s Tools/Validation -p test_ios_addressables_publication.py -v
"""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from urllib.parse import urlsplit


spec = importlib.util.spec_from_file_location(
    'ios_publication_under_test', Path(__file__).resolve().parents[1] / 'publish_ios_addressables.py')
publication = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publication)
pair = sys.modules['verify_ios_addressables_pair']


class MemoryS3:
    def __init__(self):
        self.objects = {}
        self.calls = []
        self.uploads = []
        self.claims = []
        self.claim_error = None
        self.claim_race = None
        self.get_errors = {}
        self.list_error = None
        self.truncated = False
        self.upload_error = None
        self.final_read_overrides = {}

    def run(self, command, **kwargs):
        self.calls.append(list(command))
        if command[0] != 'aws':
            raise AssertionError('Unexpected process: ' + repr(command))
        if command[1:3] == ['s3api', 'list-objects-v2']:
            if self.list_error:
                return subprocess.CompletedProcess(command, 1, '', self.list_error)
            prefix = command[command.index('--prefix') + 1]
            content = [{'Key': key} for key in sorted(self.objects) if key.startswith(prefix)]
            return subprocess.CompletedProcess(command, 0, json.dumps(
                {'Contents': content, 'IsTruncated': self.truncated}), '')
        if command[1:3] == ['s3api', 'get-object']:
            key = command[command.index('--key') + 1]
            if key in self.get_errors:
                return subprocess.CompletedProcess(command, 1, '', self.get_errors[key])
            data = self.objects.get(key)
            if self.uploads and key in self.final_read_overrides:
                data = self.final_read_overrides[key]
            if data is None:
                return subprocess.CompletedProcess(command, 1, '',
                    'An error occurred (NoSuchKey) when calling GetObject: absent')
            output = Path(command[command.index('--key') + 2])
            output.write_bytes(data)
            return subprocess.CompletedProcess(command, 0, '{}', '')
        if command[1:3] == ['s3api', 'put-object']:
            key = command[command.index('--key') + 1]
            data = Path(command[command.index('--body') + 1]).read_bytes()
            if '--if-none-match' not in command or command[command.index('--if-none-match') + 1] != '*':
                raise AssertionError('Release claim must be conditional')
            if self.claim_error:
                return subprocess.CompletedProcess(command, 1, '', self.claim_error)
            if self.claim_race is not None:
                # Another publisher wins after listing, before the conditional write.
                self.objects[key] = data if self.claim_race == 'matching' else b'other-build-manifest'
                self.claim_race = None
            if key in self.objects:
                return subprocess.CompletedProcess(command, 1, '',
                    'An error occurred (PreconditionFailed) when calling PutObject: exists')
            self.objects[key] = data
            self.claims.append(key)
            return subprocess.CompletedProcess(command, 0, '{}', '')
        if command[1:3] == ['s3', 'cp']:
            source, target = Path(command[3]), urlsplit(command[4])
            key = target.path.lstrip('/')
            if self.upload_error == key:
                return subprocess.CompletedProcess(command, 1, '', 'simulated upload failure')
            self.uploads.append(key)
            self.objects[key] = source.read_bytes()
            return subprocess.CompletedProcess(command, 0, '', '')
        raise AssertionError('Unexpected AWS operation: ' + repr(command))


class IOSAddressablesPublicationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.player = self.root / 'player-aa'
        self.server = self.root / 'server'
        self.player.mkdir()
        self.server.mkdir()
        self.prefix = 'release/v/3.0.1/iOS'
        self.hash_name = 'catalog_3.0.1.hash'
        self.catalog_name = 'catalog_3.0.1.bin'
        self.claim_name = '.addressables-player-manifest.json'
        self.catalog_hash = b'0123456789abcdef0123456789abcdef'
        self.catalog_bytes = b'paired-binary-catalog-fixture'
        self.configure_url('https://fixture.s3.ap-northeast-1.amazonaws.com/'
                           + self.prefix + '/' + self.hash_name)
        (self.player / 'catalog.hash').write_bytes(self.catalog_hash)
        (self.player / 'catalog.bin').write_bytes(self.catalog_bytes)
        (self.server / self.hash_name).write_bytes(self.catalog_hash)
        (self.server / self.catalog_name).write_bytes(self.catalog_bytes)
        (self.server / 'a_scripts.bundle').write_bytes(b'new-script-archive')
        (self.server / 'z_effects.bundle').write_bytes(b'new-effect-archive')
        self.s3 = MemoryS3()
        self.addCleanup(patch.stopall)
        patch.object(publication.subprocess, 'run', side_effect=self.s3.run).start()
        # Catch any accidental HTTP request, including future helper changes.
        patch.object(pair, 'read_remote', side_effect=AssertionError('Unexpected HTTP request')).start()

    def configure_url(self, url):
        (self.player / 'settings.json').write_text(json.dumps({'m_CatalogLocations': [
            {'m_Keys': ['AddressablesMainContentCatalogRemoteHash'], 'm_InternalId': url}
        ]}), encoding='utf-8')

    def key(self, filename):
        return self.prefix + '/' + filename

    def run_publication(self, write=False):
        with contextlib.redirect_stdout(io.StringIO()):
            return publication.publish(self.player, self.server, 'fixture-profile', write)

    def seed_payload(self, include_hash=True):
        for file in self.server.iterdir():
            if include_hash or file.suffix != '.hash':
                self.s3.objects[self.key(file.name)] = file.read_bytes()

    def assert_no_uploads(self):
        self.assertEqual(self.s3.uploads, [])
        self.assertFalse(any(command[1:3] == ['s3', 'cp'] for command in self.s3.calls))

    def test_default_dry_run_inspects_destination_without_uploading(self):
        plan = self.run_publication()
        self.assert_no_uploads()
        self.assertEqual(self.s3.claims, [])
        self.assertFalse(any(command[1:3] == ['s3api', 'put-object'] for command in self.s3.calls))
        self.assertEqual(plan['destination'], 's3://fixture/' + self.prefix + '/')
        self.assertEqual(set(plan['files']),
                         {'a_scripts.bundle', 'z_effects.bundle', self.catalog_name, self.hash_name})
        self.assertTrue(self.s3.calls)

    def test_different_local_catalog_hash_fails_before_any_aws_call(self):
        (self.server / self.hash_name).write_bytes(b'f' * 32)
        with self.assertRaisesRegex(ValueError, 'does not match'):
            self.run_publication(write=True)
        self.assertEqual(self.s3.calls, [])

    def test_equal_hash_with_different_catalog_bytes_fails_before_aws(self):
        (self.server / self.catalog_name).write_bytes(b'unrelated-catalog-despite-same-hash')
        with self.assertRaisesRegex(ValueError, 'catalog|Catalog'):
            self.run_publication(write=True)
        self.assertEqual(self.s3.calls, [])

    def test_previous_v2_catalog_protects_release_even_with_new_catalog_filename(self):
        self.s3.objects[self.key('catalog_v2.hash')] = self.catalog_hash
        self.s3.objects[self.key('catalog_v2.bin')] = b'old-catalog'
        with self.assertRaisesRegex(ValueError, 'different player|new.*version|Increase'):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_existing_different_hash_is_rejected_before_any_write(self):
        self.s3.objects[self.key(self.hash_name)] = b'f' * 32
        with self.assertRaisesRegex(ValueError, 'different player|Increase'):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_existing_identical_release_allows_safe_retry(self):
        self.seed_payload()
        before = dict(self.s3.objects)
        plan = self.run_publication(write=True)
        self.assertEqual(plan['catalogHash'], self.catalog_hash.decode('ascii'))
        for key, data in before.items():
            self.assertEqual(self.s3.objects[key], data)
        self.assertEqual(json.loads(self.s3.objects[self.key(self.claim_name)]), plan)

    def test_matching_hash_does_not_allow_overwriting_different_catalog(self):
        self.seed_payload()
        self.s3.objects[self.key(self.catalog_name)] = b'wrong-catalog'
        with self.assertRaisesRegex(ValueError, 'catalog|Catalog'):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_matching_hash_does_not_allow_overwriting_different_bundle(self):
        self.seed_payload()
        self.s3.objects[self.key('a_scripts.bundle')] = b'incompatible-script-archive'
        with self.assertRaises(ValueError):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_matching_hash_does_not_allow_unknown_existing_content(self):
        self.seed_payload()
        self.s3.objects[self.key('unknown.bundle')] = b'belongs-to-another-build'
        with self.assertRaises(ValueError):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_new_release_uploads_bundles_then_catalog_then_hash_and_verifies(self):
        plan = self.run_publication(write=True)
        expected = [self.key(name) for name in
                    ['a_scripts.bundle', 'z_effects.bundle', self.catalog_name, self.hash_name]]
        self.assertEqual(self.s3.uploads, expected)
        self.assertEqual(self.s3.claims, [self.key(self.claim_name)])
        claim_index = next(index for index, command in enumerate(self.s3.calls)
                           if command[1:3] == ['s3api', 'put-object'])
        first_upload = next(index for index, command in enumerate(self.s3.calls)
                            if command[1:3] == ['s3', 'cp'])
        self.assertLess(claim_index, first_upload)
        self.assertEqual(json.loads(self.s3.objects[self.key(self.claim_name)]), plan)
        for key in expected:
            self.assertEqual(self.s3.objects[key], (self.server / key.rsplit('/', 1)[1]).read_bytes())
        last_upload = max(index for index, command in enumerate(self.s3.calls)
                          if command[1:3] == ['s3', 'cp'])
        verified = [command[command.index('--key') + 1] for command in self.s3.calls[last_upload + 1:]
                    if command[1:3] == ['s3api', 'get-object']]
        self.assertIn(self.key(self.hash_name), verified)
        self.assertIn(self.key(self.catalog_name), verified)

    def test_identical_partial_upload_can_retry(self):
        self.s3.objects[self.key('a_scripts.bundle')] = (self.server / 'a_scripts.bundle').read_bytes()
        self.run_publication(write=True)
        self.assertEqual(self.s3.objects[self.key(self.hash_name)], self.catalog_hash)
        self.assertEqual(self.s3.uploads[-1], self.key(self.hash_name))

    def test_different_partial_upload_cannot_retry(self):
        self.s3.objects[self.key('a_scripts.bundle')] = b'another-build-script-archive'
        with self.assertRaises(ValueError):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_partial_upload_failure_does_not_publish_hash(self):
        self.s3.upload_error = self.key('z_effects.bundle')
        with self.assertRaisesRegex(ValueError, 'Upload failed'):
            self.run_publication(write=True)
        self.assertNotIn(self.key(self.hash_name), self.s3.objects)
        self.s3.upload_error = None
        self.run_publication(write=True)
        self.assertEqual(self.s3.objects[self.key(self.hash_name)], self.catalog_hash)
        self.assertEqual(len(self.s3.claims), 1)

    def test_existing_matching_claim_allows_retry_without_replacing_claim(self):
        plan = self.run_publication()
        claim = (json.dumps(plan, sort_keys=True, indent=2) + '\n').encode('utf-8')
        self.s3.objects[self.key(self.claim_name)] = claim
        self.run_publication(write=True)
        self.assertEqual(self.s3.claims, [])
        self.assertEqual(self.s3.objects[self.key(self.claim_name)], claim)
        self.assertEqual(self.s3.uploads[-1], self.key(self.hash_name))

    def test_existing_different_claim_rejects_before_payload_write(self):
        self.s3.objects[self.key(self.claim_name)] = b'other-build-manifest'
        with self.assertRaisesRegex(ValueError, 'different build|new version'):
            self.run_publication(write=True)
        self.assert_no_uploads()
        self.assertEqual(self.s3.claims, [])

    def test_concurrent_different_claim_rejects_before_any_bundle_upload(self):
        self.s3.claim_race = 'different'
        with self.assertRaisesRegex(ValueError, 'Another publisher|different build'):
            self.run_publication(write=True)
        self.assert_no_uploads()
        self.assertEqual(self.s3.objects[self.key(self.claim_name)], b'other-build-manifest')
        self.assertNotIn(self.key(self.hash_name), self.s3.objects)

    def test_concurrent_matching_claim_allows_identical_payload_retry(self):
        self.s3.claim_race = 'matching'
        plan = self.run_publication(write=True)
        self.assertEqual(self.s3.claims, [])
        self.assertEqual(json.loads(self.s3.objects[self.key(self.claim_name)]), plan)
        self.assertEqual(self.s3.uploads[-1], self.key(self.hash_name))

    def test_claim_permission_403_is_not_treated_as_concurrent_success(self):
        self.s3.claim_error = 'An error occurred (403) when calling PutObject: Forbidden'
        with self.assertRaisesRegex(ValueError, 'Cannot reserve|403'):
            self.run_publication(write=True)
        self.assert_no_uploads()
        self.assertEqual(self.s3.objects, {})

    def test_access_denied_is_not_treated_as_missing_object(self):
        self.s3.get_errors[self.key(self.catalog_name)] = \
            'An error occurred (403) when calling GetObject: Forbidden'
        with self.assertRaisesRegex(ValueError, 'Cannot read|403'):
            self.run_publication(write=True)
        self.assert_no_uploads()

    def test_incomplete_or_denied_listing_never_allows_upload(self):
        for listing_error, truncated in [(None, True), ('AccessDenied (403)', False)]:
            with self.subTest(listing_error=listing_error, truncated=truncated):
                self.s3.list_error, self.s3.truncated = listing_error, truncated
                with self.assertRaises(ValueError):
                    self.run_publication(write=True)
                self.assert_no_uploads()

    def test_destination_is_derived_only_from_versioned_release_player_url(self):
        bad_urls = [
            'https://fixture.s3.ap-northeast-1.amazonaws.com/dev/v/3.0.1/iOS/' + self.hash_name,
            'https://fixture.s3.ap-northeast-1.amazonaws.com/release/v/latest/iOS/' + self.hash_name,
            'https://fixture.s3.ap-northeast-1.amazonaws.com/release/v/3.0.1/Android/' + self.hash_name,
            'https://other.example/release/v/3.0.1/iOS/' + self.hash_name,
            'http://fixture.s3.ap-northeast-1.amazonaws.com/' + self.prefix + '/' + self.hash_name,
            'https://fixture.s3.ap-northeast-1.amazonaws.com/' + self.prefix + '/' + self.hash_name + '?x=1',
            'https://fixture.s3.ap-northeast-1.amazonaws.com/' + self.prefix + '/' + self.hash_name + '#fragment',
        ]
        for url in bad_urls:
            with self.subTest(url=url):
                self.configure_url(url)
                self.s3.calls.clear()
                with self.assertRaises(ValueError):
                    self.run_publication(write=True)
                self.assertEqual(self.s3.calls, [])
                self.assert_no_uploads()

    def test_final_hash_mismatch_is_reported(self):
        self.s3.final_read_overrides[self.key(self.hash_name)] = b'f' * 32
        with self.assertRaisesRegex(ValueError, 'hash|Hash'):
            self.run_publication(write=True)

    def test_final_catalog_mismatch_is_reported(self):
        self.s3.final_read_overrides[self.key(self.catalog_name)] = b'corrupted-publication'
        with self.assertRaisesRegex(ValueError, 'catalog|Catalog'):
            self.run_publication(write=True)

    def test_missing_final_hash_is_reported_as_validation_error(self):
        self.s3.final_read_overrides[self.key(self.hash_name)] = None
        with self.assertRaises(ValueError):
            self.run_publication(write=True)

    def test_cli_returns_failure_for_final_verification_error(self):
        self.s3.final_read_overrides[self.key(self.hash_name)] = None
        arguments = ['publish_ios_addressables.py', '--player-aa', str(self.player),
                     '--server-dir', str(self.server), '--aws-profile', 'fixture-profile', '--publish']
        with patch.object(sys, 'argv', arguments), contextlib.redirect_stdout(io.StringIO()), \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(publication.main(), 1)


if __name__ == '__main__':
    unittest.main()
