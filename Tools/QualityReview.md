# UI lifecycle and resource cleanup (2026-10-01)

Loading and highlight curtains now cancel superseded fades before accepting a
new request. Delayed highlight requests carry a version so they cannot release
the input mask for a newer operation. Title animation uses an owned material,
reuses it on initialization, and destroys it with the title layer; the source
asset is never animated. Title button handlers bind once per layer.

Run `PocketStrikerUIInterruptionValidation.StartBatch` in a separate Unity batch
editor without `-quit`. This isolated Play-mode regression loads the actual UI
prefabs and checks loading-to-loading interruption, loading-to-download and
normal close, superseded highlight fade, superseded delayed highlight, and title
material reuse/disposal. Set `POCKETSTRIKER_UI_QUALITY` to a fresh report label;
output is `Logs/UIQuality/<label>`. It does not log in or perform a purchase.

The cleanup removed only 30 vendor demonstration scenes and 10 archived menu
images, plus their original `.meta` files. Their textures, meshes, materials,
animation assets, package code, documentation, licenses and directory metadata
remain available. Build/validation tools and output directories were not purged.
The old `OrganizedResources/Unused/unused_resource_report.tsv` remains as a
historical migration record; it is not a live asset catalog.

Before removal, candidates were checked against Unity dependencies of every
retained asset, enabled build scenes, all Resources assets and expanded
Addressables entries. GUID and dynamic string/path searches included source,
configuration, tools and the shared package. No candidate was a plugin or lived
under Resources. This is a reviewed allowlist, not automatic zero-reference
deletion. `PocketStrikerResourceCleanupAudit.Validate` is read-only; supply JSON
input with `POCKETSTRIKER_CLEANUP_CANDIDATES` and an output path with
`POCKETSTRIKER_CLEANUP_REPORT`. Its raw unresolved-GUID list includes legacy
serialized references and is a before/after comparison aid, not a blanket claim
that every listed GUID is a missing runtime component.

Local recovery material is outside the project:

`/Users/daisei/Documents/Codex/2026-09-30/task-2/QualityReview/2026-10-01/`

`quarantine-manifest.json` records all 80 file paths, GUIDs, sizes and SHA-256
hashes. `Quarantine/` preserves complete paths and file contents. Close Unity
before restoring, then run:

```sh
python3 /Users/daisei/Documents/Codex/2026-09-30/task-2/QualityReview/2026-10-01/quarantine.py restore
```

Restore validates every backup and refuses to overwrite a different current
file. Nothing was permanently deleted or uploaded. Removing 187.87 MiB from
the project reduces imported project content; the backup still occupies local
disk space. Uncertain assets and platform integrations remain in the project.
