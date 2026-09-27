#!/usr/bin/env python3
"""Test the actual postprocessor against an exported Podfile and CocoaPods without Unity."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser()
    parser.add_argument("--export", type=Path, default=root / "Builds/Revival/iOS")
    args = parser.parse_args()
    version = next(line.split(":", 1)[1].strip()
                   for line in (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{version}/Unity.app"))
    if editor.name == "Unity":
        editor = editor.parents[2]
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    podfile = args.export / "Podfile"
    pods_project = args.export / "Pods/Pods.xcodeproj"
    for dependency in (mono, compiler, podfile, pods_project):
        if not dependency.exists():
            raise SystemExit(f"Missing validation dependency: {dependency}")
    with tempfile.TemporaryDirectory(prefix="pocketstriker-ios-pods-") as directory:
        executable = Path(directory) / "IOSPodsTests.exe"
        patched = Path(directory) / "Podfile"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            str(root / "Assets/Editor/PocketStrikerIOSPods.cs"),
            str(root / "Tools/Validation/IOSPodsTests.cs"),
        ], check=True, cwd=root)
        subprocess.run([str(mono), str(executable), str(podfile), str(patched)], check=True, cwd=root)
        subprocess.run(["ruby", "-c", str(patched)], check=True, cwd=root)
        subprocess.run(["ruby", str(root / "Tools/Validation/IOSPodsHookTests.rb"), str(patched), str(pods_project)],
                       check=True, cwd=root, timeout=30)


if __name__ == "__main__":
    main()
