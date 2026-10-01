"""Verify the independent asset job's player bootstrap archive using temp files.

Run: python3 -m unittest discover -s Tools/Validation -p test_addressables_bootstrap.py -v
No Unity, AWS, or network request is executed by these tests.
"""

import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).resolve().parents[1] / "package_addressables_bootstrap.py"
spec = importlib.util.spec_from_file_location("addressables_bootstrap_under_test", SCRIPT)
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)


class BootstrapTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.runtime = self.root / "runtime"
        self.server = self.root / "server"
        self.runtime.mkdir()
        self.server.mkdir()
        self.remote_url = "https://example.com/release/v/3.0.2/iOS/catalog_3.0.2.hash"
        self.settings = {
            "m_buildTarget": "iOS", "m_AddressablesVersion": "2.9.1",
            "m_CatalogLocations": [
                {"m_Keys": [bootstrap.REMOTE_HASH_KEY], "m_InternalId": self.remote_url},
                {"m_Keys": [bootstrap.CATALOG_KEY],
                 "m_InternalId": bootstrap.RUNTIME_PATH + "/catalog.bin",
                 "m_Dependencies": [bootstrap.REMOTE_HASH_KEY]},
            ],
        }
        self.write_settings()
        self.catalog = b"independently built catalog"
        self.hash = b"a" * 32
        (self.runtime / "catalog.bin").write_bytes(self.catalog)
        (self.runtime / "catalog.hash").write_bytes(self.hash)
        (self.server / "catalog_3.0.2.bin").write_bytes(self.catalog)
        (self.server / "catalog_3.0.2.hash").write_bytes(self.hash)
        (self.runtime / "AddressablesLink").mkdir()
        (self.runtime / "AddressablesLink/link.xml").write_text(
            '<linker><assembly fullname="Assembly-CSharp" preserve="all"/></linker>', encoding="utf-8")
        (self.server / ("units_" + "b" * 32 + ".bundle")).write_bytes(b"remote bundle")

    def write_settings(self):
        (self.runtime / "settings.json").write_text(json.dumps(self.settings), encoding="utf-8")

    def package(self):
        return bootstrap.package(self.runtime, self.server)

    def test_packages_exact_relative_runtime_files_and_preserves_nested_bundles(self):
        local = self.runtime / "iOS/local.bundle"
        local.parent.mkdir()
        local.write_bytes(b"local bundle")
        archive, count = self.package()
        self.assertEqual(archive, self.server / "player-bootstrap.zip")
        self.assertEqual(count, 5)
        with zipfile.ZipFile(archive) as output:
            self.assertEqual(set(output.namelist()), {
                "settings.json", "catalog.bin", "catalog.hash", "AddressablesLink/link.xml", "iOS/local.bundle"})
            self.assertEqual(output.read("catalog.bin"), self.catalog)
            self.assertEqual(output.read("iOS/local.bundle"), b"local bundle")
        self.assertEqual(local.read_bytes(), b"local bundle")

    def test_json_catalog_is_supported(self):
        (self.runtime / "catalog.bin").rename(self.runtime / "catalog.json")
        (self.server / "catalog_3.0.2.bin").rename(self.server / "catalog_3.0.2.json")
        self.settings["m_CatalogLocations"][1]["m_InternalId"] = bootstrap.RUNTIME_PATH + "/catalog.json"
        self.write_settings()
        archive, _ = self.package()
        with zipfile.ZipFile(archive) as output:
            self.assertIn("catalog.json", output.namelist())

    def test_missing_or_empty_required_files_are_rejected(self):
        for path in [self.runtime / "settings.json", self.runtime / "catalog.bin",
                     self.runtime / "catalog.hash", self.runtime / "AddressablesLink/link.xml",
                     self.server / "catalog_3.0.2.bin", self.server / "catalog_3.0.2.hash"]:
            original = path.read_bytes()
            for absent in (True, False):
                with self.subTest(path=path.name, absent=absent):
                    path.unlink() if absent else path.write_bytes(b"")
                    with self.assertRaises((ValueError, OSError)):
                        self.package()
                    path.write_bytes(original)
        self.assertFalse((self.server / bootstrap.ARCHIVE_NAME).exists())

    def test_catalog_and_hash_bytes_must_match_server_output(self):
        for path in [self.server / "catalog_3.0.2.bin", self.server / "catalog_3.0.2.hash"]:
            original = path.read_bytes()
            path.write_bytes(original + b"\n")
            with self.subTest(path=path.name), self.assertRaisesRegex(ValueError, "bytes differ"):
                self.package()
            path.write_bytes(original)

    def test_malformed_hash_and_link_xml_are_rejected(self):
        for content in (b"z" * 32, b"a" * 31):
            (self.runtime / "catalog.hash").write_bytes(content)
            (self.server / "catalog_3.0.2.hash").write_bytes(content)
            with self.subTest(hash=content), self.assertRaisesRegex(ValueError, "hexadecimal"):
                self.package()
        (self.runtime / "catalog.hash").write_bytes(self.hash)
        (self.server / "catalog_3.0.2.hash").write_bytes(self.hash)
        (self.runtime / "AddressablesLink/link.xml").write_text("<other/>", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "linker root"):
            self.package()

    def test_remote_url_must_be_https_and_match_platform_and_catalog(self):
        for url in [self.remote_url.replace("https:", "http:"), self.remote_url + "?token=x",
                    self.remote_url.replace("/iOS/", "/Android/"), self.remote_url.replace("3.0.2.hash", "3.0.3.hash")]:
            self.settings["m_CatalogLocations"][0]["m_InternalId"] = url
            self.write_settings()
            with self.subTest(url=url), self.assertRaises(ValueError):
                self.package()

    def test_remote_hash_must_be_unique_and_used_by_main_catalog(self):
        locations = self.settings["m_CatalogLocations"]
        locations.append(dict(locations[0]))
        self.write_settings()
        with self.assertRaisesRegex(ValueError, "exactly one"):
            self.package()
        locations.pop()
        locations[1]["m_Dependencies"] = []
        self.write_settings()
        with self.assertRaisesRegex(ValueError, "depend on"):
            self.package()

    def test_wrong_local_catalog_location_or_ambiguous_catalog_is_rejected(self):
        self.settings["m_CatalogLocations"][1]["m_InternalId"] = "https://example.com/catalog.bin"
        self.write_settings()
        with self.assertRaisesRegex(ValueError, "packaged local catalog"):
            self.package()
        self.settings["m_CatalogLocations"][1]["m_InternalId"] = bootstrap.RUNTIME_PATH + "/catalog.bin"
        self.write_settings()
        (self.runtime / "catalog.json").write_bytes(self.catalog)
        with self.assertRaisesRegex(ValueError, "exactly one local catalog"):
            self.package()

    def test_existing_bundle_naming_is_preserved_and_no_bundles_is_rejected(self):
        bundle = next(self.server.glob("*.bundle"))
        bundle.rename(self.server / "units.bundle")
        self.package()
        (self.server / "units.bundle").unlink()
        with self.assertRaisesRegex(ValueError, "no bundles"):
            self.package()

    def test_runtime_symlinks_and_nonportable_names_are_rejected(self):
        external = self.root / "external"
        external.mkdir()
        (external / "secret").write_bytes(b"not runtime data")
        shortcut = self.runtime / "shortcut"
        shortcut.symlink_to(external, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinks"):
            self.package()
        shortcut.unlink()
        (self.runtime / "unsafe\\path").write_bytes(b"invalid archive path")
        with self.assertRaisesRegex(ValueError, "not portable"):
            self.package()

    def test_invalid_build_preserves_previous_archive(self):
        archive, _ = self.package()
        previous = archive.read_bytes()
        (self.server / "catalog_3.0.2.bin").write_bytes(b"unpaired catalog")
        with self.assertRaises(ValueError):
            self.package()
        self.assertEqual(archive.read_bytes(), previous)
        self.assertFalse(list(self.server.glob(".player-bootstrap-*")))

    def test_cli_reports_success_and_failure(self):
        command = [sys.executable, str(SCRIPT), "--runtime-dir", str(self.runtime), "--server-dir", str(self.server)]
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("player-bootstrap.zip", result.stdout)
        (self.runtime / "settings.json").unlink()
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn("packaging failed", result.stderr)


if __name__ == "__main__":
    unittest.main()
