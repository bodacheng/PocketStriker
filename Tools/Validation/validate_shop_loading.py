#!/usr/bin/env python3
"""Exercise real shop callbacks with fake UI/PlayFab transport; never starts Unity."""
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
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    for dependency in (mono, compiler):
        if not dependency.is_file():
            raise SystemExit(f"Missing Unity compiler dependency: {dependency}")
    sources = [
        "Assets/MainSceneSystem/NAGARE/Processes/Shop/ShopTop.cs",
        "Assets/PlayFab/Client/ReadOnlyUserData.cs",
        "Assets/UI/TimeLimitedSaleWindow.cs",
        "Tools/Validation/ShopLoadingTests.cs",
    ]
    with tempfile.TemporaryDirectory(prefix="pocketstriker-shop-") as directory:
        executable = Path(directory) / "ShopLoadingTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            *[str(root / source) for source in sources],
        ], check=True, cwd=root)
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, timeout=30)


if __name__ == "__main__":
    main()
