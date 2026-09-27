# PocketStriker Unity 6000.5 migration

Reference project: MComat `ios`, commit `80dccbe027196438f4c7ae34f519ea8da292ad56`.
Shared package: MCombatShared `5e962289405dd8a4078538350915e444fad1e4d4`.
Both remote refs were fetched on 2026-09-27. MComat's application files were read only.

## Integration boundary

The shared package is now a real Git submodule, imported by Unity Package Manager
through `file:com.mcombat.shared`. Its 821 upstream files and metadata were checked
against the prior hash manifest before conversion. Local package updates now use
Git checkout and preserve the submodule's `.git` pointer. See [shared package workflow](MCombatShared.md).

The editor version, registry dependencies, native plugins and vendor tools are
aligned with MComat; details and exceptions are in [dependency parity](DependencyUpgrade.md).
Package Git dependencies are pinned to MComat's resolved commits. Steamworks and
unrelated sample assets are not added to PocketStriker.

## Portrait and project identity

- Portrait is the default orientation, with landscape and upside-down rotation disabled.
- The default desktop/web viewport is 540 × 960; the mobile canvases retain 1080 × 1920.
- iOS is selected by the launcher and by ordinary interactive editor opens. Explicit CLI
  targets and manual target changes within an editor session remain supported.
- PocketStriker's scene/prefab GUIDs, mobile account binding, product identifiers,
  PlayFab/AdMob settings, stage modes, touch layout and safe-area calculation are retained.
- Existing stronger PocketStriker guards remain: inventory/config compatibility,
  pending receipt validation, UI duplicate/lifetime handling and startup error recovery.

## Applicable MCombat improvements

- Coalesce concurrent animation, audio and hitbox loads, publish only completed animation
  series, cache label lookups, limit skill preload concurrency and size pools from the
  incoming fight rather than the cleared previous teams.
- Dispose old battle/health/UI subscriptions, reuse combat decision buffers and avoid
  stale physics overlap entries and repeated collision allocations. Bind hurt/rotation
  tweens to their fighter lifetime so scene reentry cannot animate destroyed transforms.
- Cancel or discard stale async previews, reuse stage/icon caches and release generated
  editor textures. Keep portrait-specific preview sizing and navigation.
- Update Ads callbacks/lifetimes for the new SDK and wait for PlayFab login before
  initializing IAP. Preserve receipt validation before acknowledging a purchase.
- Enable incremental GC as in MComat, bring in newer cloth and URP shader fixes, and
  use Unity 6000.5 EntityId APIs.
- Bring in build version consistency checks and localized iOS display names. The
  development upload path's version now matches the existing 3.0.0 resource version;
  no remote content is uploaded by validation.
- Use Unity's public Sign in with Apple capability API in the project postprocessor,
  preserving the upstream shared package. Link AuthenticationServices to UnityFramework
  and verify the exported Apple entitlement. Plain localized app names no longer carry
  Smart String metadata, which iOS app metadata does not support.
- Normalize localized InfoPlist variants after Unity Localization runs, and raise older
  Pod deployment targets to the app's existing iOS 15 minimum during CocoaPods installation.
  This keeps fresh exports compatible with the installed Xcode 27 toolchain.

MComat's landscape UI, Steam login, story/event progression, extra monster roster,
main/subunit gameplay changes and weaker legacy guards are deliberate differences,
not performance changes to copy over. Shared-package source remains unmodified.

## Validation

Commands and report paths are in the [README](../README.md). The checks distinguish
Editor execution, iOS player-script compilation, Xcode export, native compilation and
physical-device validation. Online login, actual ad delivery and purchases are outside
the local no-account smoke run.


### Recorded results — 2026-09-27

- Unity 6000.5.1f1, active target iOS: editor import and player compilation succeeded.
- Project validation: 3 enabled scenes, 493 Addressables entries, no missing-script or
  validation errors.
- Final portrait startup/reentry smoke: two 20-second fights, one fighter per team,
  frames 298–1498 and 1510–2710. Pool double-return/re-rent checks passed. Actual Editor
  viewport was 642 × 1156; requested/default viewport remains 540 × 960. No Unity errors
  or destroyed-target DOTween warnings were emitted in the final smoke.
- iOS development export succeeded with no captured Error/Exception/Assert. Portrait-only
  orientations, Apple sign-in entitlement and AuthenticationServices linkage passed.
- Xcode 27.0 / iPhoneOS 27.0, generic iOS device, Debug, code signing disabled:
  **BUILD SUCCEEDED**, no compilation errors. SDK/template/deprecation warnings remain.
  CocoaPods resolved Google Mobile Ads 13.9.0 and UMP 3.1.0; all 24 Pods configurations
  respect the app's iOS 15 minimum. Export: `Builds/Revival/iOS`; unsigned app:
  `Library/RevivalXcode/Build/Products/Debug-iphoneos/PocketStriker.app`.
- Interactive Editor was opened on iOS, the portrait title/fight was visually verified,
  and Play was stopped with Scene1 open. Console showed 0 errors (2 warnings).
- Regression suites: 92 dependency checks, 56 runtime loading checks, 15 PlayFab contract
  checks, 13 shop lifecycle checks, 13 UTC/version consistency checks, 4 real submodule
  workflow tests, and 7 Podfile generation checks. Ruby hook tests also verify lower,
  equal, higher and inherited deployment targets against the actual Pods project.
- Repeated locale normalization was checked three times against the exported project:
  exactly one en/ja/zh_Hans entry each, validated by Unity's Xcode DLL, plutil and xcodeproj.

Raw reports and logs are under `Logs/Revival`. Local validation's release content-state
file, shader-prefilter caches and editor history were restored; real Unity/URP schema
upgrades were retained. Unreferenced FBX-import material byproducts were moved to a
backup under `Logs/Upgrade6000.5.1`.

Physical iOS device installation and real Apple/PlayFab login, ad delivery and purchases
were not exercised. No CDN upload, store publication or code signing was performed.
