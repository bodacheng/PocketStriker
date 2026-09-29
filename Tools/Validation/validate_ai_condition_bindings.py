#!/usr/bin/env python3
"""Check direct AI condition references in compiled iOS player code (before stripping)."""
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
    resources = editor / "Contents/Resources"
    mono_root = resources / "Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    cecil = resources / "BuildPipeline/Unity.Cecil.dll"
    netstandard = mono_root / "lib/mono/4.5/Facades/netstandard.dll"
    player = root / "Library/RevivalPlayerScripts/iOS/Assembly-CSharp.dll"
    shared = player.with_name("MCombat.Shared.dll")
    for dependency in (mono, compiler, cecil, netstandard, player, shared):
        if not dependency.is_file():
            raise SystemExit(f"Run Tools/validate_unity.sh compile first; missing: {dependency}")

    with tempfile.TemporaryDirectory(prefix="pocketstriker-ai-bindings-") as directory:
        executable = Path(directory) / "AIConditionBindingTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            f"/reference:{cecil}", f"/reference:{netstandard}",
            str(root / "Tools/Validation/AIConditionBindingTests.cs"),
        ], check=True)
        environment = os.environ.copy()
        environment["MONO_PATH"] = os.pathsep.join([str(cecil.parent), str(netstandard.parent)])
        subprocess.run([str(mono), str(executable), str(player), str(shared)],
                       check=True, env=environment, timeout=30)


if __name__ == "__main__":
    main()
