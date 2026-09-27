#!/usr/bin/env python3
"""Exercise UTC sale logic and the imported VersionSyncUtility without Unity startup."""

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
    assembly = root / "Library/ScriptAssemblies/Assembly-CSharp-Editor.dll"
    for required in [mono, compiler, assembly]:
        if not required.is_file():
            raise SystemExit(f"Missing dependency; import the project with Unity {version} first: {required}")
    with tempfile.TemporaryDirectory(prefix="pocketstriker-upgrade-regressions-") as directory:
        executable = Path(directory) / "UpgradeRegressionTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            str(root / "Assets/UI/TimeLimitedSaleWindow.cs"),
            str(root / "Tools/Validation/UpgradeRegressionTests.cs"),
        ], check=True, cwd=root)
        environment = os.environ.copy()
        environment["TZ"] = "Asia/Tokyo"
        environment["MONO_PATH"] = os.pathsep.join([
            str(assembly.parent), str(scripting / "Managed"),
            str(scripting / "Managed/UnityEngine"), str(mono_root / "lib/mono/4.5/Facades"),
        ])
        subprocess.run([str(mono), str(executable), str(assembly)], check=True, cwd=root, env=environment)


if __name__ == "__main__":
    main()
