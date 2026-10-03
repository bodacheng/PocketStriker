#!/usr/bin/env python3
"""Run production post-battle policy and ad lifecycle with offline SDK doubles."""

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
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    for dependency in (mono, compiler):
        if not dependency.is_file():
            raise SystemExit(f"Unity {version} scripting runtime is missing: {dependency}")

    # Ensure result settlement cannot hide the placement behind a win/reward
    # branch and that direct in-scene retries reset before preloading.
    result_source = (root / "Assets/FightSceneSystem/Nagare/FightOverProcess.cs").read_text(encoding="utf-8-sig")
    result_entry = result_source.split("void EnterProcess()", 1)[1].split("void ShowEventResult()", 1)[0]
    assert result_entry.index("FightScene.target.JustShowAds();") < result_entry.index("switch (FightLoad.Fight.EventType)")
    assert result_source.count("FightScene.target.JustShowAds();") == 1
    preparing = (root / "Assets/FightSceneSystem/Nagare/PreparingProcess.cs").read_text(encoding="utf-8-sig")
    assert "FightScene.FightScene.target.BeginBattleAds();" in preparing.split("public override void ProcessEnter()", 1)[1]
    controller = (root / "Assets/FightScene/FightScene.cs").read_text(encoding="utf-8-sig")
    assert "watchBtn.transform.SetParent(transform, false);" in controller
    assert "FSceneProcessesRunner.Main.currentProcess is FightOverProcess" in controller

    sources = [
        "Packages/com.mcombat.shared/Runtime/Combat/FightEnums.cs",
        "Assets/Ads/PostBattleAdSession.cs",
        "Assets/Ads/AdmobAdsButton.cs",
        "Tools/Validation/PostBattleAdsTests.cs",
    ]
    with tempfile.TemporaryDirectory(prefix="pocketstriker-post-battle-ads-") as directory:
        executable = Path(directory) / "PostBattleAdsTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", "/define:UNITY_ANDROID",
            f"/out:{executable}", *[str(root / source) for source in sources],
        ], check=True, cwd=root)
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, timeout=30)


if __name__ == "__main__":
    main()
