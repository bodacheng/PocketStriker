#!/usr/bin/env python3
"""Run the real project story selector without Unity or paid provider requests."""
import os
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[2]
    version = next(line.split(":", 1)[1].strip()
                   for line in (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{version}/Unity.app")).expanduser()
    if editor.name == "Unity":
        editor = editor.parents[2]
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    for dependency in (mono, compiler):
        if not dependency.is_file():
            raise SystemExit(f"Missing Unity compiler runtime: {dependency}")
    source = root / "Assets/AIStory/PocketStrikerStoryVariety.cs"
    catalog = re.findall(r'^        "([^"]+)",?$', source.read_text().split("static readonly string[] Twists")[0], re.M)
    config = (root / "Assets/AIStory/AIServiceConfig.asset").read_text()
    configured = re.findall(r'^  - "([^"]+)"$', config.split("  pageCount:")[0], re.M)
    if configured != catalog:
        raise SystemExit("Shared legacy config subject catalog differs from the project generator.")
    with tempfile.TemporaryDirectory(prefix="pocketstriker-story-variety-") as directory:
        executable = Path(directory) / "StoryVarietyTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            str(source), str(root / "Tools/Validation/StoryVarietyTests.cs"),
        ], check=True, cwd=root)
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, timeout=30)


if __name__ == "__main__":
    main()
