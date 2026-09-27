#!/usr/bin/env python3
"""Validate client CloudScript wire contracts using the installed PlayFab SDK.

Run after Unity has imported the project:
    python3 Tools/Validation/validate_cloudscript_contracts.py

UNITY_EDITOR_PATH may point to Unity.app or its executable. The test uses Unity's
bundled Mono/compiler and never starts the editor or contacts PlayFab.
"""

import os
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[2]
    configured = os.environ.get("UNITY_EDITOR_PATH")
    if configured:
        editor = Path(configured).expanduser()
        if editor.name == "Unity":
            editor = editor.parents[2]
    else:
        version = next(line.split(":", 1)[1].strip()
                       for line in (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                       if line.startswith("m_EditorVersion:"))
        editor = Path("/Applications/Unity/Hub/Editor") / version / "Unity.app"
        if not editor.is_dir():
            raise SystemExit(f"Set UNITY_EDITOR_PATH or install the project's Unity version ({version}).")

    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    playfab = root / "Library/ScriptAssemblies/PlayFab.dll"
    json_dlls = sorted((root / "Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    if not playfab.is_file() or not json_dlls:
        raise SystemExit("Open/import this project in Unity first; PlayFab.dll and Newtonsoft.Json.dll are required.")
    for dependency in (mono, compiler, netstandard):
        if not dependency.is_file():
            raise SystemExit(f"Missing Unity compiler dependency: {dependency}")
    newtonsoft = json_dlls[-1]
    sdk_source = (root / "Assets/PlayFabSDK/Shared/Public/PlayFabSettings.cs").read_text()
    sdk_version = re.search(r'SdkVersion\s*=\s*"([^"]+)"', sdk_source).group(1)

    with tempfile.TemporaryDirectory(prefix="pocketstriker-contracts-") as directory:
        executable = Path(directory) / "CloudScriptContractTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            f"/reference:{playfab}", f"/reference:{newtonsoft}", f"/reference:{netstandard}",
            str(root / "Assets/PlayFab/CloudScripts/CloudScriptDtos.cs"),
            str(root / "Assets/PlayFab/CloudScripts/Gotcha.cs"),
            str(root / "Tools/Validation/CloudScriptContractTests.cs"),
        ], check=True, cwd=root)
        environment = os.environ.copy()
        environment["MONO_PATH"] = os.pathsep.join([
            str(playfab.parent), str(newtonsoft.parent),
            str(editor / "Contents/Resources/Scripting/Managed/UnityEngine"),
            str(mono_root / "lib/mono/4.5/Facades"),
        ])
        subprocess.run([str(mono), str(executable), sdk_version], check=True, cwd=root, env=environment)


if __name__ == "__main__":
    main()
