#!/usr/bin/env python3
"""Check the Unity 6000.5 vendor migration without launching Unity or a store SDK."""

import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def main():
    root = Path(__file__).resolve().parents[2]
    dependencies = json.loads((root / "Packages/manifest.json").read_text())["dependencies"]
    checks = 0

    def check(condition, message):
        nonlocal checks
        if not condition:
            raise SystemExit("FAIL: " + message)
        checks += 1

    check("m_EditorVersion: 6000.5.1f1" in (root / "ProjectSettings/ProjectVersion.txt").read_text(),
          "Unity editor version")
    migrations = [
        ("com.cysharp.unitask", "ceac8d6946b1125fe782cd171fbcb245b567dbf9", "Assets/Plugins/UniTask"),
        ("com.neuecc.unirx", "6baeccf6c544c155497164327cca72f28163a578", "Assets/Plugins/UniRx"),
        ("com.google.external-dependency-manager", "b38da4950d8439d50e15c71168d70e16fea731b0", "Assets/ExternalDependencyManager"),
    ]
    for package, commit, legacy in migrations:
        check(dependencies.get(package, "").endswith("#" + commit), package + " is reproducibly pinned")
        check(not (root / legacy).exists(), package + " has no duplicate legacy Assets assembly")

    playfab = (root / "Assets/PlayFabSDK/Shared/Public/PlayFabSettings.cs").read_text()
    check('SdkVersion = "2.242.260805"' in playfab, "PlayFab SDK version")
    dotween = (root / "Assets/Demigiant/DOTween/DOTween.dll").read_bytes()
    check("1.2.765".encode("utf-16le") in dotween, "DOTween runtime version")
    manifest = root / "Assets/GoogleMobileAds/GoogleMobileAds_version-11.4.0_manifest.txt"
    check(manifest.is_file(), "Google Mobile Ads 11.4.0 manifest")
    for path in manifest.read_text().splitlines():
        if path.startswith("Assets/ExternalDependencyManager/"):
            continue  # EDM is supplied through the pinned UPM dependency.
        check((root / path).is_file(), "Google Mobile Ads manifest file: " + path)

    check(not (root / "Assets/Plugins/iOS/unity-plugin-library.a").exists(), "old iOS static bridge removed")
    check((root / "Assets/Plugins/iOS/unity-plugin-library.xcframework/Info.plist").is_file(), "new iOS XCFramework present")
    check(not (root / "Assets/GoogleMobileAds/GoogleMobileAds_version-9.0.0_manifest.txt").exists(), "old ads manifest removed")

    gradle = (root / "Assets/Plugins/Android/mainTemplate.gradle").read_text()
    for name in ["GoogleMobileAdsDependencies.xml", "GoogleUmpDependencies.xml"]:
        xml = root / "Assets/GoogleMobileAds/Editor" / name
        for package in ET.parse(xml).findall(".//androidPackage"):
            check("implementation '" + package.attrib["spec"] + "'" in gradle,
                  "Gradle agrees with SDK dependency " + package.attrib["spec"])
    check("validate_dependencies.gradle" not in gradle, "obsolete ads Gradle script removed")
    check("com.android.billingclient:billing:" not in gradle, "IAP controls its own Billing dependency")

    for meta in (root / "Assets/Plugins/Android").glob("*.meta"):
        asset = meta.with_suffix("")
        if asset.suffix in [".aar", ".jar"]:
            check(not re.search(r"^- gpsr\s*$", meta.read_text(), re.M),
                  "no stale resolver artifact conflicts with Maven: " + asset.name)
    ads_assembly = json.loads((root / "Assets/GoogleMobileAds/Editor/GoogleMobileAds.Editor.asmdef").read_text())
    check(not any("Placement" in reference for reference in ads_assembly["references"]),
          "ads editor does not reference absent Placement assemblies")
    print(f"PASS: {checks} dependency migration checks")


if __name__ == "__main__":
    main()
