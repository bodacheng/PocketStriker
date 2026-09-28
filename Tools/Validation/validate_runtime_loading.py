#!/usr/bin/env python3
"""Run actual resource loader code with controlled async asset/pool doubles.

Uses Unity's bundled compiler and imported UniTask.dll; never starts Unity.
"""
import os
from pathlib import Path
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
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    managed = editor / "Contents/Resources/Scripting/Managed"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    unitask = root / "Library/ScriptAssemblies/UniTask.dll"
    for dependency in (mono, compiler, netstandard, unitask):
        if not dependency.is_file():
            raise SystemExit(f"Import the project first; missing compiler dependency: {dependency}")
    sources = [
        "Assets/BoundaryControl/BoundaryControlByGod.cs",
        "Assets/ResourceLoading/AnimationResourceLoader.cs",
        "Assets/AnimControl/AnimationManger_ResourceLoad.cs",
        "Assets/ResourceLoading/AudioResourceLoading.cs",
        "Assets/ResourceLoading/EffectsLoad/EffectsManager.cs",
        "Assets/ResourceLoading/HurtObjectLoad/HurtObjectManager.cs",
        "Tools/Validation/RuntimeLoadingTests.cs",
    ]
    sources += ["Packages/com.mcombat.shared/Runtime/ResourceLoading/" + name + ".cs" for name in (
        "AnimationResourceLoaderCore", "AnimationResourceKeyUtility", "AudioResourceLoaderCore",
        "AudioResourceLoadingCore", "EffectResourceKeyUtility", "IndexedResourceLoadUtility",
        "ResourcePoolRegistry", "ResourcePoolConstructionUtility",
    )]
    with tempfile.TemporaryDirectory(prefix="pocketstriker-loaders-") as directory:
        executable = Path(directory) / "RuntimeLoadingTests.exe"
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
