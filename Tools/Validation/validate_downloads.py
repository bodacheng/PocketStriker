#!/usr/bin/env python3
"""Exercise startup download policy with controlled frames and Addressables operations.

Uses Unity's bundled compiler; never starts Unity or makes network requests.
Only NextFrame/Delay waits are substituted in a temporary source copy because
the Unity PlayerLoop is unavailable in this command-line harness.
"""
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[2]
    version = next(line.split(":", 1)[1].strip()
                   for line in (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{version}/Unity.app"))
    if editor.name == "Unity":
        editor = editor.parents[2]
    scripting = editor / "Contents/Resources/Scripting"
    mono_root = scripting / "MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    unitask = root / "Library/ScriptAssemblies/UniTask.dll"
    for required in (mono, compiler, netstandard, unitask):
        if not required.is_file():
            raise SystemExit(f"Import the project with Unity {version} first: {required}")
    source = (root / "Assets/Addressables/PocketStrikerDependencyDownloader.cs").read_text()
    substitutions = {
        "await UniTask.NextFrame();": "await DownloadTestClock.NextFrame();",
        "UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true)": "DownloadTestClock.Delay()",
    }
    for original, replacement in substitutions.items():
        assert source.count(original) == 1, f"PlayerLoop wait changed; review test seam: {original}"
        source = source.replace(original, replacement)
    with tempfile.TemporaryDirectory(prefix="pocketstriker-downloads-") as directory:
        temporary_source = Path(directory) / "PocketStrikerDependencyDownloader.cs"
        temporary_source.write_text(source)
        executable = Path(directory) / "DownloadTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            f"/reference:{unitask}", f"/reference:{netstandard}",
            str(temporary_source), str(root / "Assets/Addressables/PocketStrikerDownloadPolicy.cs"),
            str(root / "Assets/Addressables/PocketStrikerDownloadText.cs"),
            str(root / "Tools/Validation/DownloadTests.cs"),
        ], check=True, cwd=root)
        environment = os.environ.copy()
        environment["MONO_PATH"] = os.pathsep.join([
            str(unitask.parent), str(scripting / "Managed"),
            str(scripting / "Managed/UnityEngine"), str(mono_root / "lib/mono/4.5/Facades"),
        ])
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, env=environment, timeout=30)


if __name__ == "__main__":
    main()
