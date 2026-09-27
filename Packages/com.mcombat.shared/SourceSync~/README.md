# SourceSync

`SourceSync~` contains project-assembly assets that must stay identical in MCombat and PocketStriker, but cannot live under `Runtime` because they depend on project-local types such as `Soul.Behavior` or scene/prefab references. The trailing `~` keeps Unity from importing this tooling/source mirror when the repository is mounted directly as a package.

Run `sync-to-projects.sh` after editing these files. The script copies the shared `Behaviour`, `P3`, `Camera`, `TheNineSlot`, `SkillStoneBox`, `SimpleDragAndDrop/Scripts`, `Remote/Stone`, `Remote/API/Dto`, `UnitBox`, `POS_Sys`, `ResourceLoading`, `EffectsSystem`, shared `BOWeaponSystem`, `PlayFab`, and `UI` subsets, selected `MainSceneSystem` process roots, `DummyLayerSystem/LayerDefine/FrontLayer`, and `Structure/SingleThreadProcesser` trees plus selected reusable files into both projects. A project that mounts this repository directly as its package is left in place instead of being overwritten.

Selected single-file sync entries also cover project adapters and small shared UI helpers such as `AIStory/AIStoryProjectContext.cs`, `FightSceneSystem/FSceneProcess.cs`, selected Gotcha and tutorial layer partials, `FightingSceneUISystem` cell helpers, language converter helpers, and editor-only `MasterDataTool` PlayFab catalog exporters. These are intentionally listed as individual files because their sibling directories still contain project-specific code.

`manifest.json` is the single source of truth for managed roots, single-file sync entries, per-root project-local exclusions, and obsolete project files to delete.

`Behaviour/BtnUI/MobileInputsManager.cs`, `UnitBox/UnitFilter.cs`, `DummyLayerSystem/LayerDefine/StoneListLayer.Project.cs`, the `UnitBox/SSLevelUpManager` execution/update-all project partials, selected `BOWeaponSystem`, `PlayFab`, `UI`, `FightSceneSystem`, `FightingSceneUISystem`, `Language`, and `MasterDataTool` adapter files are intentionally project-specific. The sync script also preserves project-local `.meta` files so MonoBehaviour GUIDs and execution-order settings do not drift between existing Unity assets.

Run `verify-source-sync.sh` to confirm that all non-excluded files under the managed asset roots match `SourceSync~` and that embedded packages are in sync.
