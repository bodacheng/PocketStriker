# MCombat Shared

This package contains gameplay modules that should be treated as MCombat-owned shared code.

PocketStriker consumes these modules through thin adapters so MCombat can remain the source of truth for shared battle behavior. PocketStriker currently references this package from MCombat through `Packages/manifest.json`.

Current modules:

- `Account`: shared player-account state model used by PlayFab/login flows.
- `Addressables`: shared Addressables dependency download, asset loading, progress, and handle-release flow.
- `AI`: shared AI decision throttling and condition-response registration rules.
- `AIStory`: shared AI text/image clients, story generation, style configuration, and `StoryInfo` data model.
- `CombatGroup`: boss/group battle unit-count rules extracted from MCombat's group fight flow.
- `Combat`: shared team/layer/spatial helpers and team-elimination winner tracking.
- `CombatHit`: shared hit-detection definitions and pure damage utility rules.
- `Log`: shared fight-log aggregation and hitbox outcome counters.
- `Localization`: shared translation and story-table loading/query logic.
- `Setting`: shared application, combat, PlayFab, and icon settings.
- `Skill`: shared skill config models, attack-type mapping, and skill-set validation rules.
- `UI`: reusable buttons, indicators, effects, rewards, ads, and lightweight view components.
- `Utility`: shared lightweight container/reflection helpers and CSV table parsing utilities.
- `Editor/Build`: shared Android build and YAML configuration helpers.

`SourceSync~` asset roots (ignored by Unity package import):

- `Behaviour`, `P3`, `Camera`, `TheNineSlot`, `SkillStoneBox`, `Remote/Stone`,
  `Remote/API/Dto`, `SimpleDragAndDrop/Scripts`, shared `UnitBox` stone detail/effects/upgrade logic,
  `POS_Sys`, `ResourceLoading`, `EffectsSystem`, shared `BOWeaponSystem`, `PlayFab`, and `UI` subsets,
  selected `MainSceneSystem` processes, and selected reusable layer files.
