# MCombatShared in PocketStriker

`Packages/com.mcombat.shared` is a Git submodule of
`https://github.com/bodacheng/MCombatShared.git`, pinned to
`5e962289405dd8a4078538350915e444fad1e4d4` (2026-09-14).
The upstream `origin/main` was fetched and verified on 2026-09-27.
Unity Package Manager imports it through `file:com.mcombat.shared` in
`Packages/manifest.json`. Its package version remains `0.1.20`; the parent Git
repository's gitlink and `mcombat-shared-lock.json` record the actual revision.

## Cloning and opening

```sh
git clone --recurse-submodules https://github.com/bodacheng/PocketStriker.git
# For an existing clone, after pulling changes:
git submodule update --init --recursive
Tools/open_unity.sh
```

Use Unity **6000.5.1f1** with the **iOS Build Support** module. The launcher
opens with iOS selected; normal interactive opens also default to iOS.
A sibling MCombat checkout is not required. The upstream package's `UPSTREAM.md`
mentions an obsolete local path; use its Git URL instead.

## Reviewing an update

```sh
git -C Packages/com.mcombat.shared fetch origin
python3 Tools/sync_mcombat_shared.py --revision origin/main
# After reviewing the proposed revision:
python3 Tools/sync_mcombat_shared.py --revision COMMIT --apply
git add Packages/com.mcombat.shared Tools/mcombat-shared-lock.json
```

The updater defaults to review only. `--apply` changes the submodule checkout
and provenance lock; it refuses local modifications and preserves the `.git`
link. The parent repository pins the selected commit when the gitlink is staged
and committed. An optional `--source /path/to/MCombatShared` can supply committed
revisions from another local checkout. It never edits `Assets` or that source
checkout. Keep consumer-specific fixes in this project's adapters.

The package's `SourceSync~` directory is retained verbatim and ignored by Unity.
Do not run its upstream `sync-to-projects.sh` blindly: it targets two projects
by default, removes files under managed roots, and its manifest still names
language converters that have since moved into the package. Review source
changes and apply project adapters explicitly.

## Migration applied

- Moved 25 duplicate scripts from `Assets` to their upstream package locations.
  Every moved script retained the exact same `.meta` GUID, preserving scene,
  prefab, and ScriptableObject references.
- Adopted renamed fight gauge/boundary fields and the shared group-size limits.
- Migrated the common-settings animation duration to the typed list while
  preserving the existing human animation value (`0.15`). The legacy gang-battle
  story table remains `gb_short_story`, selected through `StoryFile`.
- Preserved PocketStriker's WASD defaults in a project adapter. Camera rotation
  uses the shared camera's `PlayerPrefs` setting and its new enabled default.
- Routed unit icon loading through the shared helper while retaining the
  project's Addressables loading and release policy.
- Kept the Windows account-binding adapter on PlayFab CustomID. Upstream's
  `SourceSync~` variant depends on a Steam integration absent from PocketStriker.
- Kept the reviewed client-only PlayFab DTO/error-handling adapters in `Assets`;
  the package snapshot remains the original committed upstream source.

Run `Tools/validate_unity.sh` after imports and consumer changes. The exact
upstream submodule should stay unchanged; keep project-specific behavior in
`Assets` adapters.
