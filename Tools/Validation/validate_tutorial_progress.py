#!/usr/bin/env python3
"""Compile actual tutorial marker persistence/queue with an offline request dispatcher.

Exercises all 720 stage arrival orders, failure/recovery and account/session changes.
Uses Unity's bundled compiler and imported PlayFab types; never launches Unity or sends requests.
Only Login.cs delay calls are replaced with a controlled clock because Unity PlayerLoop is unavailable.
"""
import json
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
    unitask = root / "Library/ScriptAssemblies/UniTask.dll"
    for dependency in (mono, compiler, netstandard, playfab, unitask):
        if not dependency.is_file():
            raise SystemExit(f"Import the project in Unity {version} first; missing dependency: {dependency}")
    report_path = root / "Logs/Tutorial/progress-queue.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "networkRequests": 0, "arrivalOrders": 720,
              "scope": "Production TutorialProgress.cs and Login.cs; local preferences/request dispatcher/login clock, imported PlayFab types."}
    try:
        with tempfile.TemporaryDirectory(prefix="pocketstriker-tutorial-progress-") as directory:
            executable = Path(directory) / "TutorialProgressTests.exe"
            login = (root / "Assets/PlayFab/Client/Login.cs").read_text()
            assert login.count("UniTask.Delay(") == 2, "Review changed login delay fixture seams"
            login_source = Path(directory) / "Login.cs"
            login_source.write_text(login.replace("UniTask.Delay(", "TestClock.Delay("))
            subprocess.run([
                str(mono), str(compiler), "/nologo", "/langversion:9.0", "/nowarn:0436", f"/out:{executable}",
                f"/reference:{playfab}", f"/reference:{netstandard}", f"/reference:{unitask}",
                str(login_source),
                str(root / "Assets/PlayFab/Client/TutorialProgress.cs"),
                str(root / "Tools/Validation/TutorialProgressTests.cs"),
            ], check=True, cwd=root)
            environment = os.environ.copy()
            environment["MONO_PATH"] = os.pathsep.join([
                str(playfab.parent), str(scripting / "Managed/UnityEngine"),
                str(mono_root / "lib/mono/4.5/Facades"),
            ])
            result = subprocess.run([str(mono), str(executable)], check=True, cwd=root,
                                    env=environment, timeout=30, text=True, capture_output=True)
            print(result.stdout.strip())
            report["passed"] = True
            report["result"] = result.stdout.strip()
    except subprocess.CalledProcessError as error:
        report["error"] = (error.stderr or error.stdout or str(error)).strip()
        raise
    finally:
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    main()
