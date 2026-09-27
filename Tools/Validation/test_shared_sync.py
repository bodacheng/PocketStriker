import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("shared_sync", Path(__file__).resolve().parents[1] / "sync_mcombat_shared.py")
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)


class SharedSyncTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        root = Path(self.directory.name)
        self.source = root / "upstream"
        self.source.mkdir()
        self.project = root / "PocketStriker"
        self.package = self.project / "Packages/com.mcombat.shared"
        self.package.mkdir(parents=True)
        (self.project / "Tools").mkdir()
        (self.project / "Assets").mkdir()
        (self.project / "Assets/local.txt").write_text("project adapter")
        (self.source / "package.json").write_text(json.dumps({"name": "com.mcombat.shared", "version": "0.1.20"}))
        (self.source / "Runtime").mkdir()
        (self.source / "Runtime/example.cs").write_text("committed upstream source")
        (self.source / "sync.sh").write_text("#!/bin/sh\n")
        (self.source / "sync.sh").chmod(0o755)
        self.git("init", "-q")
        self.git("add", ".")
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-qm", "fixture")
        self.commit = self.git("rev-parse", "HEAD").strip()

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.source), *args], text=True)

    def run_sync(self, *args):
        with patch.multiple(sync, PROJECT=self.project, PACKAGE=self.package, LOCK=self.project / "Tools/mcombat-shared-lock.json"):
            with patch.object(sys, "argv", ["sync", "--source", str(self.source), "--revision", self.commit, *args]):
                sync.main()

    def test_review_does_not_write(self):
        self.run_sync()
        self.assertEqual(list(self.package.iterdir()), [])
        self.assertFalse((self.project / "Tools/mcombat-shared-lock.json").exists())

    def test_import_uses_pinned_commit_and_is_idempotent(self):
        (self.source / "Runtime/example.cs").write_text("uncommitted change must not be imported")
        self.run_sync("--apply")
        self.assertEqual((self.package / "Runtime/example.cs").read_text(), "committed upstream source")
        self.assertTrue((self.package / "sync.sh").stat().st_mode & 0o111)
        before = {p: p.read_bytes() for p in self.project.rglob("*") if p.is_file()}
        self.run_sync("--apply")
        self.assertEqual(before, {p: p.read_bytes() for p in self.project.rglob("*") if p.is_file()})
        self.assertEqual((self.project / "Assets/local.txt").read_text(), "project adapter")

    def test_local_package_edit_is_preserved_and_rejected(self):
        self.run_sync("--apply")
        edited = self.package / "Runtime/example.cs"
        edited.write_text("local repair")
        with self.assertRaisesRegex(SystemExit, "Local package edit"):
            self.run_sync("--apply")
        self.assertEqual(edited.read_text(), "local repair")


if __name__ == "__main__":
    unittest.main()
