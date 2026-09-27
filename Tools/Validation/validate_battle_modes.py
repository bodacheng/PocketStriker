#!/usr/bin/env python3
"""Exercise adventure rules, menu navigation and Boss reward callbacks.

Uses production C# sources and the imported PlayFab SDK with a controlled request
dispatcher. Never starts Unity or sends network requests.
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
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{version}/Unity.app")).expanduser()
    if editor.name == "Unity":
        editor = editor.parents[2]
    scripting = editor / "Contents/Resources/Scripting"
    mono_root = scripting / "MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    playfab = root / "Library/ScriptAssemblies/PlayFab.dll"
    for dependency in (mono, compiler, netstandard, playfab):
        if not dependency.is_file():
            raise SystemExit(f"Import the project in Unity {version} first; missing dependency: {dependency}")

    sources = [
        "Packages/com.mcombat.shared/Runtime/Combat/FightEnums.cs",
        "Assets/MainSceneSystem/NAGARE/Processes/AdventureModeRules.cs",
        "Assets/MainSceneSystem/NAGARE/SceneProcess.cs",
        "Assets/MainSceneSystem/NAGARE/MSceneProcess.cs",
        "Assets/MainSceneSystem/NAGARE/ProcessesRunner.cs",
        "Assets/PlayFab/CloudScripts/EventBattle.cs",
        "Tools/Validation/BattleModeTests.cs",
    ]
    with tempfile.TemporaryDirectory(prefix="pocketstriker-battle-modes-") as directory:
        executable = Path(directory) / "BattleModeTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            f"/reference:{playfab}", f"/reference:{netstandard}",
            *[str(root / source) for source in sources],
        ], check=True, cwd=root)
        environment = os.environ.copy()
        environment["MONO_PATH"] = os.pathsep.join([
            str(playfab.parent), str(scripting / "Managed/UnityEngine"),
            str(mono_root / "lib/mono/4.5/Facades"),
        ])
        subprocess.run([
            str(mono), str(executable), str(root / "Assets/ExternalAssets/Config/stage_mode.csv"),
        ], check=True, cwd=root, env=environment, timeout=30)


if __name__ == "__main__":
    main()
