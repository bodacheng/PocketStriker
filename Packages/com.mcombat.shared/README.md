# MCombat Shared

This package contains gameplay modules that should be treated as MCombat-owned shared code.

PocketStriker consumes these modules through thin adapters so MCombat can remain the source of truth for shared battle behavior. PocketStriker currently references this package from MCombat through `Packages/manifest.json`.

Current modules:

- `Account`: shared player-account state model used by PlayFab/login flows.
- `AI`: shared AI decision throttling and condition-response registration rules.
- `CombatGroup`: boss/group battle unit-count rules extracted from MCombat's group fight flow.
- `Combat`: shared team/layer/spatial helpers and team-elimination winner tracking.
- `CombatHit`: shared hit-detection definitions and pure damage utility rules.
- `Log`: shared fight-log aggregation and hitbox outcome counters.
- `Skill`: shared skill config models, attack-type mapping, and skill-set validation rules.
- `Utility`: shared lightweight container/reflection helpers and CSV table parsing utilities.
