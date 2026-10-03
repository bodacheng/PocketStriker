# Approved static pixel gradient

The source is the user-approved **Red-to-Black preview, Library version 3**:
`libfile_49a5b68988b48191ac3c744444912f9e`, approved at
2026-10-03 08:06:51 UTC. `approved-red.png` is a byte-for-byte copy of
`output/artproposals/PixelGradient-Red-to-Black-Preview-20261003.png`.
It is 942 × 1670; SHA-256 is
`d69af0e4378b3b61abd9eb3b1481faa9ce2135c619cae7e42218bf2b3e7b753f`.
The bottom's 1–2 code-value near-black residue was explicitly accepted.

The 2026-10-03 vertical-flip preview keeps every source and runtime PNG unchanged.
All six MainMenuScene backgrounds enable `flipStaticVertically`: the static UV
starts at V=1 and has height=-1, so the complete approved image displays with
near black at the top and the attribute color at the bottom. The horizontal UV
span and centered crop, Point sampling, material palettes, background scroller
state, UI transforms and fighter animations are unchanged. The aspect component
reapplies this direction after activation, resizing and delayed texture binding;
its default flip setting remains false for other backgrounds. This is a display
sampling change, not a rewrite, resampling or rotation of the image bytes.

All six existing runtime PNGs receive the same approved image bytes. No block is
redrawn, cropped, tiled, resized, recolored in the image, or added. Dedicated
`PocketStriker/UI/StaticPixelGradient` materials supply the other five attribute
colors while Red preserves the original sampled RGB. The shared red-channel
intensity is multiplied by a fixed display-sRGB palette: green `(0.12,0.60,0.22)`,
blue `(0.10,0.32,0.78)`, Light/gold `(0.82,0.58,0.10)`, Dark/purple
`(0.48,0.13,0.65)`, and Null/neutral `(0.56,0.56,0.56)`. The shader performs exact
sRGB conversions when running a Linear color-space project. The current project
uses Gamma. Palette properties are Vectors to avoid implicit Color conversion.

Texture GUIDs and existing resource references remain intact. Importers use
Point sampling, horizontal Repeat and vertical Clamp, no mipmaps, no NPOT
rescaling, and no texture compression or platform overrides. The full vertical
gradient is intended to remain visible. Static UV sizing/positioning, disabled
background scrollers and six material bindings are owned by the MainMenuScene
and `ScrollingBackgroundAspectFill` integration; this source copier never edits
those or other animations.

Run the standard-library-only copier from the project root:

```sh
python3 Tools/Art/build_menu_static_gradient_backgrounds.py
python3 Tools/Art/build_menu_static_gradient_backgrounds.py --apply
python3 Tools/Art/build_menu_static_gradient_backgrounds.py --check
```

The default is a dry run with no writes. Before any runtime write, it checks the
approved hash/dimensions, all twelve current assets against their frozen or
already-applied hashes, and all six original GUIDs. It captures an immutable,
hash-verified twelve-file copy under `runtime-backup/` before installing the
static image. The authoritative file mapping, palettes and importer hashes are
in `manifest.json`.

For **runtime texture/importer files only**:

```sh
python3 Tools/Art/build_menu_static_gradient_backgrounds.py --restore-runtime
```

This validates the runtime backup and current expected files before restoring
all twelve originals. It does not restore Scene, material bindings, Aspect logic
or motion configuration.

For **complete prior integration state**, the persistent
`integration-backup/Source/` mirrors the original 27-file Before snapshot plus
the supplemental Scene.meta: all **28 files** are copied byte-for-byte, including
the old runtime PNG/meta, Scene/meta, Aspect and validation source/meta, old art
builder/README, and related shared UI/PreScene control files. These copies retain
the modifications that already existed before this task. The existing twelve-file
runtime backup is kept separately and is never overwritten.

```sh
python3 Tools/Art/restore_menu_static_gradient_backgrounds.py
python3 Tools/Art/restore_menu_static_gradient_backgrounds.py --verify
# Only when full restoration is explicitly intended:
python3 Tools/Art/restore_menu_static_gradient_backgrounds.py --restore
```

The default and `--verify` perform read-only verification. `--restore` validates
**every** original backup and **every** current destination against the pinned
before/after hashes before writing any production file, rechecks all inputs after
preflight, restores the complete set, and verifies the read-back. A later file
edit outside the registered hashes blocks restoration so new work cannot be
silently overwritten. If the authorized static integration changes after the
snapshot, review it and update only that file's `acceptedAfterSha256`/bytes in
`integration-backup/manifest.json` to the final source hash; never change the
original hashes or copies. The original evidence manifests remain under
`Logs/MenuStaticPixelGradientReview/Before/`.

Full restore deliberately leaves the new gradient shader, six materials, this
source set, backup files, and installer/restore tools in place. It restores the
old Scene material references and background motion configuration, so these new
assets cease to affect the homepage while remaining available for review.

Actual Unity screenshots and stationary, state-switch, navigation, delayed-load,
aspect-ratio and input evidence are generated separately by the validation task;
the copier's byte/importer checks do not replace rendered or device validation.
