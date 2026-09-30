#!/usr/bin/env python3
"""Exercise production unit preparation with controlled async model/Unity doubles.

Uses Unity's bundled compiler without starting Unity. UNITASK_DLL may point to
an already imported checkout's UniTask.dll when validating a fresh worktree.
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
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    managed = editor / "Contents/Resources/Scripting/Managed"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    unitask = Path(os.environ.get("UNITASK_DLL", str(root / "Library/ScriptAssemblies/UniTask.dll")))
    for dependency in (mono, compiler, netstandard, unitask):
        if not dependency.is_file():
            raise SystemExit(f"Missing compiler dependency: {dependency}; set UNITASK_DLL to an imported UniTask.dll if needed.")
    sources = [
        "Assets/Singleton/GeneralModelPool.cs",
        "Assets/ResourceLoading/UnitCreator.cs",
        "Tools/Validation/ModelLoadingTests.cs",
    ]
    with tempfile.TemporaryDirectory(prefix="pocketstriker-models-") as directory:
        executable = Path(directory) / "ModelLoadingTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            f"/reference:{unitask}", f"/reference:{netstandard}",
            *[str(root / source) for source in sources],
        ], check=True, cwd=root)
        environment = os.environ.copy()
        environment["MONO_PATH"] = os.pathsep.join([
            str(unitask.parent), str(managed), str(managed / "UnityEngine"),
            str(mono_root / "lib/mono/4.5/Facades"),
        ])
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, env=environment, timeout=30)


if __name__ == "__main__":
    main()
