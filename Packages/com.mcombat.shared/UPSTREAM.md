# Upstream Policy

This repository is the source of truth for the MCombat shared package.

Current local package repository:

```text
/Users/daisei/MCombatShared
```

Current local consumers:

```text
MComat/Packages/com.mcombat.shared -> git submodule /Users/daisei/MCombatShared
PocketStriker/Packages/manifest.json -> file:/Users/daisei/MCombatShared
```

PocketStriker-specific code should stay outside this package and connect through adapters.

MCombat consumes the package as a submodule at `Packages/com.mcombat.shared`. PocketStriker consumes the same package directly with:

```json
"com.mcombat.shared": "file:/Users/daisei/MCombatShared"
```

When a remote repository is available, replace both the MCombat submodule URL and PocketStriker package URL with that remote.

Keep old `Assets` copies removed in both projects to avoid duplicate type definitions.
