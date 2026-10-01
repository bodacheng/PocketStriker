"""Regression checks for independently rebuilt Addressables local content.

Run: python3 -m unittest discover -s Tools/Validation -p test_addressables_local_compatibility.py -v
"""

import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).with_name("verify_addressables_local_compatibility.py")
spec = importlib.util.spec_from_file_location("addressables_local_compatibility", SCRIPT)
compatibility = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compatibility)


class LocalCompatibilityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.player = self.root / "aa"
        self.player.mkdir()
        self.archive = self.root / "player-bootstrap.zip"
        self.local = {
            "iOS/units_assets_human_tetsuya.bundle": b"prefab referencing CAB-old-monoscripts",
            "iOS/old_monoscripts.bundle": b"CAB-old-monoscripts",
            compatibility.LINKER: b'<linker><assembly fullname="Assembly-CSharp"/></linker>',
        }
        for name, content in self.local.items():
            path = self.player / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        (self.player / "catalog.bin").write_bytes(b"old catalog")
        (self.player / "catalog.hash").write_bytes(b"old hash")
        self.write_bootstrap()

    def write_bootstrap(self, files=None):
        with zipfile.ZipFile(self.archive, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, content in (self.local if files is None else files).items():
                archive.writestr(name, content)
            archive.writestr("catalog.bin", b"new catalog with remote updates")
            archive.writestr("catalog.hash", b"new catalog hash")

    def verify(self):
        return compatibility.verify(self.player, self.archive)

    def test_remote_catalog_update_with_unchanged_local_content_is_compatible(self):
        before = {path: path.read_bytes() for path in self.player.rglob("*") if path.is_file()}
        self.assertEqual(self.verify(), 2)
        self.assertEqual(before, {path: path.read_bytes() for path in self.player.rglob("*") if path.is_file()})

    def test_same_bundle_name_with_new_script_cab_is_incompatible(self):
        files = dict(self.local)
        files["iOS/units_assets_human_tetsuya.bundle"] = b"prefab referencing CAB-new-monoscripts"
        self.write_bootstrap(files)
        with self.assertRaisesRegex(ValueError, "different bytes: iOS/units_assets_human_tetsuya.bundle"):
            self.verify()

    def test_renamed_monoscript_bundle_reports_both_missing_and_old_payload(self):
        files = dict(self.local)
        files["iOS/new_monoscripts.bundle"] = files.pop("iOS/old_monoscripts.bundle")
        self.write_bootstrap(files)
        with self.assertRaises(ValueError) as raised:
            self.verify()
        self.assertIn("missing from player: iOS/new_monoscripts.bundle", str(raised.exception))
        self.assertIn("absent from bootstrap: iOS/old_monoscripts.bundle", str(raised.exception))

    def test_new_or_removed_local_bundle_requires_matching_player(self):
        for add in (True, False):
            files = dict(self.local)
            if add:
                files["iOS/new-unit.bundle"] = b"new local prefab"
            else:
                files.pop("iOS/units_assets_human_tetsuya.bundle")
            self.write_bootstrap(files)
            with self.subTest(add=add), self.assertRaisesRegex(ValueError, "Incompatible local Addressables"):
                self.verify()

    def test_changed_linker_requires_matching_player(self):
        files = dict(self.local)
        files[compatibility.LINKER] = b'<linker><assembly fullname="NewRuntime"/></linker>'
        self.write_bootstrap(files)
        with self.assertRaisesRegex(ValueError, "different bytes: AddressablesLink/link.xml"):
            self.verify()

    def test_empty_or_missing_local_content_is_not_a_success(self):
        for name, content in [(compatibility.LINKER, None), ("iOS/old_monoscripts.bundle", b"")]:
            files = dict(self.local)
            files.pop(name) if content is None else files.update({name: content})
            self.write_bootstrap(files)
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "missing|empty"):
                self.verify()
        self.write_bootstrap({compatibility.LINKER: self.local[compatibility.LINKER]})
        with self.assertRaisesRegex(ValueError, "no local bundles"):
            self.verify()

    def test_invalid_archive_paths_and_duplicates_are_rejected(self):
        for name in ["../other.bundle", "/other.bundle", "iOS\\other.bundle", "iOS//other.bundle"]:
            files = dict(self.local)
            files[name] = b"invalid path"
            self.write_bootstrap(files)
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "Invalid bootstrap archive path"):
                self.verify()
        self.write_bootstrap()
        with zipfile.ZipFile(self.archive, "a") as archive:
            import warnings
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", UserWarning)
                archive.writestr("catalog.bin", b"ambiguous catalog")
        with self.assertRaisesRegex(ValueError, "Duplicate bootstrap archive path"):
            self.verify()

    def test_player_symlink_is_rejected(self):
        link = self.player / "iOS/shortcut.bundle"
        link.symlink_to(self.player / "iOS/old_monoscripts.bundle")
        with self.assertRaisesRegex(ValueError, "symlink"):
            self.verify()

    def test_cli_reports_compatibility_and_incompatible_payload(self):
        command = [sys.executable, str(SCRIPT), "--player-aa", str(self.player), "--bootstrap", str(self.archive)]
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("2 local bundles and linker match", result.stdout)
        (self.player / "iOS/old_monoscripts.bundle").write_bytes(b"different CAB")
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn("Incompatible local Addressables content", result.stderr)
        self.assertIn("old_monoscripts.bundle", result.stderr)


if __name__ == "__main__":
    unittest.main()
