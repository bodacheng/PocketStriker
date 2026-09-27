# MCombatShared in PocketStriker

The embedded package comes from `https://github.com/bodacheng/MCombatShared.git`,
commit `5e962289405dd8a4078538350915e444fad1e4d4` (2026-09-14).
`origin/main` was fetched and verified at that commit on 2026-09-27. The package
still reports version `0.1.20`, so the commit and SHA-256 manifest in
`mcombat-shared-lock.json` identify the actual snapshot.

The checkout used for this import was
`/Users/daisei/MComat/Packages/com.mcombat.shared`. The package's `UPSTREAM.md`
still mentions a retired `/Users/daisei/MCombatShared` path; use the repository
URL and pinned commit above instead. PocketStriker keeps a self-contained
embedded package, so another consumer checkout is not required to open it.

## Reviewing an update

From the project root, use a local checkout of the upstream repository:

```sh
python3 Tools/sync_mcombat_shared.py --source /path/to/MCombatShared
python3 Tools/sync_mcombat_shared.py --source /path/to/MCombatShared --revision COMMIT
```

These commands only report differences. Add `--apply` to import the reviewed
commit. The updater imports committed files, checks the package name, records
file hashes, and refuses to overwrite edits to a previously recorded snapshot.
It modifies only this project's embedded package and its provenance lock.
It never modifies any consumer's `Assets` or another project's files.

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
upstream package should stay unchanged; keep project-specific behavior in
`Assets` adapters.
