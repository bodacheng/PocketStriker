# Dependency parity with MComat

Updated 2026-09-27 from the existing `/Users/daisei/MComat` checkout and its resolved package cache. MComat was read-only. Versions below describe the project dependency baseline before this upgrade and the selected target; they are not claims about the newest public releases.

| Dependency | Previous baseline | Target |
| --- | --- | --- |
| Unity | 6000.4.0f1 | 6000.5.1f1 |
| PlayFab Unity SDK | 2.230.260123 | 2.242.260805 |
| Google Mobile Ads Unity | 9.0.0 | 11.4.0 |
| Google Mobile Ads Android | 23.0.0 | 25.4.0 |
| Google Mobile Ads iOS | ~> 11.2.0 | ~> 13.7 |
| Google UMP Android | 2.2.0 | 4.0.0 |
| Google UMP iOS | not explicitly pinned | 3.1.0 |
| UniTask | 2.2.3 in Assets | 2.5.11 via pinned UPM Git |
| UniRx | 6.2.2 in Assets | 7.1.0 via pinned UPM Git |
| External Dependency Manager | 1.2.187 in Assets | 1.2.188 via pinned UPM Git |
| DOTween | 1.2.250 | 1.2.765 |
| MagicaCloth2 | 2.1.4 | 2.18.3 |
| OmniShade | legacy shader snapshot | 1.9.5 shader/runtime snapshot |
| Burst | 1.8.29 | 1.8.30 |
| Localization | 1.5.11 | 1.5.13 |
| Unity Purchasing | 5.3.1 | 5.4.3 |
| Recorder | 5.1.6 | 5.1.7 |
| Collab Proxy | 2.11.4 | 2.12.4 |
| Collections | 6.4.0 | 6.5.0 |
| Mathematics | 1.3.3 | 1.4.0 |
| URP | 17.4.0 | 17.5.0 |
| Test Framework | 1.6.0 | 1.7.0 |
| UGUI | 2.0.0 | 2.5.0 |
| Project Auditor Rules | absent | 1.0.3 |

UniTask is pinned to `ceac8d6946b1125fe782cd171fbcb245b567dbf9`, UniRx to `6baeccf6c544c155497164327cca72f28163a578`, and EDM to `b38da4950d8439d50e15c71168d70e16fea731b0`. These match the commits resolved by MComat rather than leaving moving Git branches in the manifest.

## Integration details

- Removed the obsolete Assets copies of UniTask, UniRx and EDM to avoid duplicate assemblies. Of 427 prior metadata GUIDs audited in the migrated package trees, 400 remain in the new packages; the 27 removed GUIDs had no external serialized asset or assembly references.
- Existing vendor asset GUIDs remain stable. Existing PlayFab title settings, PlayFab editor preferences, AdMob application IDs and DOTween settings remain local to PocketStriker. The client CloudScript DTO adapters and asynchronous IAP validation safeguards are retained.
- The shop opens while catalogs are pending, refreshes offers after catalog completion, and discards purchase-history callbacks from an earlier visit or catalog request. Failed catalog/history loads leave the shop navigable with stone offers hidden. Product prices and purchase buttons react to store readiness and product availability.
- Ads callbacks are dispatched on Unity's main thread. Native ads are initialized only in Android/iOS players; desktop and editor flows do not issue native ad requests. Destroyed ad hosts dispose late-arriving ad objects, and rewarded ads reload after dismissal.
- GMA's old iOS static library is replaced by its device/simulator XCFramework. The Android bridge is upgraded and 65 obsolete EDM-generated AAR/JAR artifacts were removed.
- Android Gradle dependencies are taken from GMA 11.4.0's own dependency XML. MComat's checked-in Gradle template still pinned older Ads/UMP/Billing versions, so those stale pins were not copied. IAP 5.4.3 manages Billing 9.0.0 and its Kotlin compatibility dependencies through Unity's self-declared Android dependency system.
- The GMA editor assembly retains references to the shipped GoogleMobileAds assemblies. MComat's extra references to absent Placement assemblies were not introduced.
- Copied Unity 6000.5 EntityId/API compatibility fixes for Asset Usage Finder, NoSuchStudio ReferenceAnalyzer and crosstales. Kept PocketStriker's null-list protection and health-bar `UNITY_EDITOR` guard where its existing code is safer.
- PlayFab Editor Extensions stays at 2.207.250117; apparent source differences were only whitespace, so its local helper files, cached CloudScript and account preferences were retained. AppCenter stays at 4.4.0: its differing loader AAR has identical archive member contents, so the local artifact was retained. YamlDotNet, IngameDebugConsole, DynamicStarrySky and Ultimate Joystick runtime sources already match MComat. Extra demo scenes, art and prefabs are not dependency upgrades and were not imported.
- PocketStriker-specific dependencies with no MComat counterpart are retained. Steamworks was not added to this mobile-oriented consumer merely because MComat uses it.

## Validation

Run the static migration checks without Unity:

```sh
python3 Tools/Validation/validate_dependency_upgrade.py
python3 Tools/Validation/validate_shop_loading.py
```

After the upgraded project has imported successfully, run the actual PlayFab serializer contract checks with the editor version selected by `ProjectVersion.txt`:

```sh
python3 Tools/Validation/validate_cloudscript_contracts.py
python3 Tools/Validation/validate_upgrade_regressions.py
```

The shop test compiles the actual shop process and read-only-data client against controlled UI/transport doubles and exercises delayed, failed, overlapping and post-exit callbacks. The upgrade regression test exercises time-limited sales under UTC, explicit offsets and a non-English culture, then invokes the imported `VersionSyncUtility` methods to check LF/CRLF upload paths. Reimport after editing the editor helper; the test deliberately fails against stale compiled code.

Unity import, project validation, iOS export/native build and gameplay smoke results are recorded by the main upgrade workflow. These static checks alone do not verify live purchases, ad delivery or device execution. Android packaging requires the Android Unity module/SDK and fresh EDM resolution; iOS native builds require Xcode and CocoaPods dependencies.

## Debug console migration (2026-09-30)

The official latest release, [In-game Debug Console v1.9.0](https://github.com/yasirkula/UnityIngameDebugConsole/releases/tag/v1.9.0), is supplied through UPM Git at commit `73c1d9c582e0b49d63ddcd6efb48b0059f33d43c`. The legacy `Assets/Plugins/IngameDebugConsole` copy is removed. Input System 1.20.0 supplies the assembly referenced by the upstream package; the project's existing legacy input setting is retained.

`Scene1` now references project-owned console and log-row prefabs under `Assets/Diagnostics`. They use the installed UGUI package's standard TMP Essential Resources and a dynamic Noto Sans CJK font asset backed by the project's existing font. The console fits portrait screens and their safe areas, preserves window resizing, bounds displayed log length, and copies the complete log and stack trace. `Starter` still controls its visibility through `CommonSetting.DevMode`.

Run **PocketStriker → Validation → Debug Console** while stopped to exercise a 22,000-character stack trace, collapsed and expanded rendering, CJK text and complete clipboard copying. The report and preview are written to `Logs/DebugConsoleUpgrade`. After changing the source font, **Prepare Debug Console Assets** refreshes the two project prefabs. The console package itself remains unmodified.

The screenshot's exact 65,000-vertex exception is uGUI's `VertexHelper.FillMesh` limit, not evidence of a 3D character mesh index-format problem. The current loading-tip geometry is well below that limit. Model preparation now reports missing configuration, root `OutsideDataLink`, its `_C` reference, and destruction or exceptions across initialization awaits, preserving cancellation and inner exceptions. `python3 Tools/Validation/validate_model_loading.py` exercises these branches using controlled asynchronous doubles; it does not validate iOS bundle deserialization or prove the original device failure resolved.
