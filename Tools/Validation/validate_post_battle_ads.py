#!/usr/bin/env python3
"""Run production post-battle policy and ad lifecycle with offline SDK doubles."""

import os
import re
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
    assert "watchBtn" not in controller and "watchAdBtnPrefab" not in controller
    assert "public void ShowAds(" not in controller
    assert controller.count("adHost.AddComponent<AdmobAdsButton>()") == 1
    assert controller.count("postBattleInterstitial.LoadAd();") == 1
    assert "postBattleInterstitial.UseInterstitialAd();" in controller
    assert "FSceneProcessesRunner.Main.currentProcess is FightOverProcess" in controller
    result_ui = (root / "Assets/DummyLayerSystem/LayerDefine/ArenaFightOver/ArenaFightOver.cs").read_text()
    assert "extraAdReward" not in result_ui and "AdBtnParent" not in result_ui
    result_prefab = (root / "Assets/Resources/DummyLayerSystem/ArenaFightOver.prefab").read_text()
    assert "m_Name: AdsT" not in result_prefab and "adBtnParent:" not in result_prefab
    assert "bd3ad1a1a63e843e6aa84fb5d6c80ed8" not in result_prefab  # OpenNoAdsPurchase
    scene = (root / "Assets/Scene/fight_scene/FightScene.unity").read_text()
    assert "6c21cee395a034edd99300ad3c2f261f" not in scene

    # Result currencies use the reclaimed width. The two totals/reward columns
    # and the entitlement label fit without the former optional-ad placement.
    blocks = {match.group(1): match.group(2) for match in re.finditer(
        r"^--- !u!224 &(\d+)\n(.*?)(?=^--- !u!|\Z)", result_prefab, re.M | re.S)}
    for row, icon, total, reward in (
        ("5028180958127909996", "8339618498691712972", "3818974927190587253", "6841516807595630361"),
        ("5542000028063766489", "2101606780018456000", "6396035456008389506", "2282221276599505721"),
    ):
        def width(key):
            return float(re.search(r"m_SizeDelta: \{x: ([\d.]+)", blocks[key]).group(1))
        def left(key):
            return float(re.search(r"m_AnchoredPosition: \{x: ([\d.]+)", blocks[key]).group(1))
        assert width(icon) < left(total)
        assert left(total) + width(total) < left(reward)
        assert left(reward) + width(reward) <= width(row)
    assert left("2282221276599505721") + left("5077328966084205415") + width("5077328966084205415") <= width("5542000028063766489")

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
