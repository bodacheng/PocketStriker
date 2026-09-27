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
        self.project.mkdir()
        self.package = self.project / "Packages/com.mcombat.shared"
        (self.project / "Tools").mkdir()
        (self.project / "Assets").mkdir()
        (self.project / "Assets/local.txt").write_text("project adapter")
        (self.source / "package.json").write_text(json.dumps({"name": "com.mcombat.shared", "version": "0.1.20"}))
        (self.source / "example.cs").write_text("committed upstream source")
        (self.source / "sync.sh").write_text("#!/bin/sh\n")
        (self.source / "sync.sh").chmod(0o755)
        self.git(self.source, "init", "-q")
        self.commit_source()
        self.original = self.git(self.source, "rev-parse", "HEAD").strip()
        self.git(self.project, "init", "-q")
        self.git(self.project, "-c", "protocol.file.allow=always", "submodule", "add", "-q", str(self.source), "Packages/com.mcombat.shared")
        (self.project / "Tools/mcombat-shared-lock.json").write_text(json.dumps(sync.snapshot(self.source, self.original), indent=2) + "\n")
        (self.source / "example.cs").write_text("updated upstream source")
        self.commit_source()
        self.commit = self.git(self.source, "rev-parse", "HEAD").strip()

    def git(self, source, *args):
        return subprocess.check_output(["git", "-C", str(source), *args], text=True)

    def commit_source(self):
        self.git(self.source, "add", ".")
        self.git(self.source, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-qm", "fixture")

    def run_sync(self, *args):
        with patch.multiple(sync, PROJECT=self.project, PACKAGE=self.package, LOCK=self.project / "Tools/mcombat-shared-lock.json"):
            with patch.object(sys, "argv", ["sync", "--source", str(self.source), "--revision", self.commit, *args]):
                sync.main()

    def test_review_does_not_write(self):
        self.run_sync()
        self.assertEqual(self.git(self.package, "rev-parse", "HEAD").strip(), self.original)
        self.assertEqual((self.package / "example.cs").read_text(), "committed upstream source")

    def test_import_uses_pinned_commit_and_preserves_submodule(self):
        (self.source / "example.cs").write_text("uncommitted change must not be imported")
        pointer = (self.package / ".git").read_bytes()
        self.run_sync("--apply")
        self.assertEqual((self.package / "example.cs").read_text(), "updated upstream source")
        self.assertTrue((self.package / "sync.sh").stat().st_mode & 0o111)
        self.assertEqual(self.git(self.package, "rev-parse", "HEAD").strip(), self.commit)
        self.assertEqual((self.package / ".git").read_bytes(), pointer)
        self.run_sync("--apply")
        self.assertEqual((self.project / "Assets/local.txt").read_text(), "project adapter")
        self.assertEqual(self.git(self.package, "status", "--porcelain"), "")

    def test_local_package_edit_is_preserved_and_rejected(self):
        edited = self.package / "example.cs"
        edited.write_text("local repair")
        with self.assertRaisesRegex(SystemExit, "Local package edit"):
            self.run_sync("--apply")
        self.assertEqual(edited.read_text(), "local repair")
        self.assertEqual(self.git(self.package, "rev-parse", "HEAD").strip(), self.original)

    def test_uninitialized_submodule_explains_recovery(self):
        (self.package / ".git").unlink()
        with self.assertRaisesRegex(SystemExit, "submodule update --init"):
            self.run_sync("--apply")


if __name__ == "__main__":
    unittest.main()
