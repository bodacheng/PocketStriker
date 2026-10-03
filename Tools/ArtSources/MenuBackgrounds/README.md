# Main menu background artwork

## Current static pixel gradient

The current source is `StaticPixelGradient-v1/approved-red.png`: the approved
Library version 3 red-to-near-black square-pixel portrait, **942×1670**. All six
runtime textures are byte-identical copies of this source. Dedicated UI materials
preserve Red's original RGB and render the same pixel layout as Green, Blue,
Light/gold, Dark/purple or Null/neutral. Their fixed display-sRGB palettes and
asset GUIDs are recorded in `StaticPixelGradient-v1/manifest.json`.

Use the static installer from the project root:

```sh
python3 Tools/Art/build_menu_static_gradient_backgrounds.py
python3 Tools/Art/build_menu_static_gradient_backgrounds.py --apply
python3 Tools/Art/build_menu_static_gradient_backgrounds.py --check
```

The first command previews its twelve texture/importer changes without writing.
The installer retains original GUIDs and a verified twelve-file runtime backup.
It never redraws, recolors, rescales or otherwise changes the approved PNG pixels.
Point sampling keeps the square blocks clear; horizontal Repeat extends the
same rows for wider screens, while vertical Clamp retains both gradient ends.

The six authored MainMenu backgrounds use `ScrollingBackgroundAspectFill` in
static-gradient mode with a full-height, centered UV view and disabled local
`OffsetScrolling` components. Waiting, element switches and reactivation do not
advance a scroll phase. Other UI and fighter animations keep their normal flow.
Fresh rendered, stationary, navigation, reload and input evidence belongs under
`Logs/MenuStaticPixelGradientReview/`; older dynamic reports remain historical.
The complete prior integration's 28 files are frozen under that directory's
`Before/` manifests. Texture-only restoration does not restore the prior Scene,
Aspect code and material bindings; see `StaticPixelGradient-v1/README.md`.

## Preserved dynamic Pattern-v7 pipeline

The preceding source set is `Pattern-v7/`: the approved 941×1672
`RedGrid-Preview-ColorBlock-20261003.png` supplies the large continuous stepped
diamond grid with a solid square inside each diamond. The other five themes
come from ImageGen color-only edits using that approved Red as their reference.
The builder preserves native source geometry, surface variation and colors;
it does not redesign, draw, recolor or flatten the artwork. Exact approval,
prompts and generation provenance stay with the source set.

All preceding complete sources remain preserved. `Pattern-v6/` contains the
previous isolated hollow diamond on a quiet square; `Pattern-v5/` contains the
previous gentle lattice with small game motifs; `Pattern-v4/` contains the plain
diamond calibration; `Retro-v3/` contains soft painted fields; and
`Multiverse-v2/` contains the cut-paper draft. The builder does not overwrite
any source artwork.

Source images stay outside `Assets` so Unity imports only the finished textures.
With Python, NumPy and Pillow installed, run from the project root:

```sh
python3 Tools/Art/build_menu_backgrounds.py --source-set Pattern-v7 --report Logs/MenuColorBlockReview/build.json
```

For Pattern-v7, one complete **native 260×262 rectangular repeat unit** is
cropped at `(350,580)` from each full portrait source. It contains two staggered
diamonds with their solid center squares; it is not a single isolated diamond.
The source manifest can record an explicit `runtimeCrop` for each theme, but
every crop must remain 260×262 and lie inside its source. All six current sources
use the same crop. Luminance translation measurements independently give a
260-pixel horizontal period and 262-pixel vertical period for all six colors;
their native square-center phases differ by at most one pixel. Light is
941×1671 and the others are 941×1672. The shorter Light source is not resized.

Opposite boundaries are matched only inside a 12-pixel band on both axes.
The interior remains byte-identical to its selected source crop; no background
feather is used to interrupt the grid. The finished native unit is copied 6×6
into a 1560×1572 PNG, with no resampling. There are 36 identical repeat units
and 72 filled diamonds in that runtime texture. The report separates
`repeatUnitCount` from `filledDiamondCount`, records the source/crop, observed
translation periods, unit dimensions/hash, `strictUniformUnits`,
`nativeInteriorUnchanged`, exact opposite edges and exported-pixel verification.
`tileFinishingMode` is `native-grid-unit-repeat`. Original generated source
pixels outside the crop remain preserved in the source PNG; the runtime image
is an exact repeat of the selected complete unit rather than a pixel copy of
the full approved portrait.

The production Aspect Fill reference for this native six-unit texture is
`(941/1560,1672/1572)` in normalized UV size. It preserves the approved portrait
span while cropping for each screen: approximately 3.62 grid periods wide and
12.76 staggered rows at 375×667; approximately 3.62 periods wide and 9.58 rows
at 768×1024. The approved Red center maps to runtime UV center
`(0.5772435897,0.5038167939)` with Unity's bottom-left UV origin, so an initial
unit-sized UV rectangle starts at `(0.0772435897,0.0038167939)`.
The builder records artwork geometry; production component and Scene reference
values are maintained separately. Screen density and scroll phase still need
actual Unity runtime verification.

Before the first Pattern-v7 Asset write, the builder protects all twelve
Pattern-v6 PNG/metadata bytes from the frozen
`Logs/MenuColorBlockReview/Before-v6/Source` snapshot. The complete whitelist
and every SHA-256 are checked before that backup is saved. Existing backups are
validated and reused. All six V7 manifest themes, source hashes, decodable
images, native crops and finished-unit invariants are checked before the first
Asset write. An incomplete or invalid source set fails before runtime changes.

For Pattern-v6, the builder still scales the **complete source image** to a 256×256
cell using Nearest sampling, matches opposite edges inside a 12-pixel band on
both axes, and copies that same finished cell 6×6 into a 1536×1536 runtime PNG.
There is no crop, new decoration, procedural drawing, denoising, global palette
change or recoloring. Only complete-source scaling, narrow boundary matching
and exact cell copying are performed. `--cells-per-axis` accepts integers 2–12;
the default is 6 and this option applies to Pattern-v6 and Pattern-v7.

The Pattern-v6 report records `sourceDims`, 256-pixel `unitWidth`/`unitHeight`, `cellsX` and
`cellsY`, `uniqueUnits=1`, `strictUniformUnits=true`, `edgesEqual=true`, and
`tileFinishingMode="single-cell-repeat"`. `unitHash` is the SHA-256 of the
finished cell's row-major RGB bytes, independently checked against every runtime
cell. Source and runtime PNG hashes and unchanged metadata are also recorded.
The builder requires all six sources to exist and decode successfully; all six
Pattern-v6 sources must be square. An incomplete or invalid set fails before
any Assets PNG is written.

Older modes remain available through `--source-set`. Pattern-v4/v5 detect the
actual repetition period, crop complete cycles and perform the prior edge
matching; Retro-v3/Multiverse-v2 retain their calm boundary finishing; and
`legacy-source` retains the original middle-content wrap. Each mode writes the
six existing runtime PNG paths without changing `.meta` files or GUIDs.
Use a new report path for each review; preserved
`Logs/MenuBackgroundReview/build.json` and its Before/After material are not
the output paths for the simplified set.

`MainMenuScene.unity` attaches
`ScrollingBackgroundAspectFill` to each background RawImage: crop UVs to fill
without stretching, preserving the scrolling phase and original scroll speeds.
The builder does not edit UI layout, raycast flags, tint, Canvas order,
materials, component values or element mapping. Null remains a visible neutral
background; `Off()` hides all six.

## Restore Pattern-v6, Pattern-v5 or the original textures

The exact frozen Pattern-v6 runtime PNGs and metadata are preserved in
`Pattern-v6/runtime-backup/runtime/`. The manifest records the twelve expected
runtime paths, byte counts and hashes, plus the frozen source-parity manifest
hash. To restore those exact pixels and Unity GUIDs:

```sh
python3 Tools/Art/build_menu_backgrounds.py --restore-pattern-v6
```

The command validates all twelve whitelisted backup hashes before copying any
file into Assets. It restores runtime textures and metadata; the frozen V6
component and Scene remain separately preserved under
`Logs/MenuColorBlockReview/Before-v6/Source` if a complete visual rollback is
needed. Neither backup creation nor this restore changes those production
files. `--source-set Pattern-v6` separately rebuilds the prior isolated-cell
mode from its complete preserved sources.

The exact preceding approved Pattern-v5 runtime PNGs and their `.meta` files
are preserved in `Pattern-v5/runtime-backup/runtime/`. Its `manifest.json`
records the twelve runtime paths and SHA-256 values. To restore those exact
bytes:

```sh
python3 Tools/Art/build_menu_backgrounds.py --restore-pattern-v5
```

The command requires exactly the six expected PNG paths and six metadata paths,
then validates all twelve backup hashes before copying any file into Assets.
This restores the saved runtime bytes; `--source-set Pattern-v5` separately
rebuilds the old mode from its preserved complete sources. Neither command
changes the production Aspect Fill component, Scene bindings or shared package.

The six original source PNGs at this directory root are preserved. The exact
runtime PNGs and their `.meta` files from local HEAD
`5e4ff2bb3f725912018b7105ddd8b0ca67d40185` are backed up in
`Legacy-5e4ff2/runtime/`, with SHA-256 in its manifest. To restore those exact
bytes, rather than regenerate them:

```sh
python3 Tools/Art/build_menu_backgrounds.py --restore-legacy
```

The command validates all backup hashes before restoring the twelve files.
To restore the prior stretch behavior as well, remove the six
`ScrollingBackgroundAspectFill` attachments from `MainMenuScene.unity`.
`--source-set legacy-source` separately reproduces the old technical finish
from the preserved generated source images.
`--source-set Multiverse-v2` rebuilds the preserved intermediate draft.
`--source-set Retro-v3` and `--source-set Pattern-v4` rebuild the other preserved
calibration sources, where a complete six-color source set exists.

## Runtime verification

`PocketStrikerMenuBackgroundValidation.StartBaselineBatch` and
`StartAfterBatch` capture the actual MainMenu scene at 375x667, 390x844, 540x960
and 768x1024. They use locally loaded, authored fighters and actual UI pointer
navigation, isolating authentication, ads and purchases. Reports and native
screenshots are in `Logs/MenuBackgroundReview/{Before,After}/Runtime`.
The Null case calls the real null-focus fallback with a frozen red preview;
the inventory contains no authored Null fighter. Editor screenshots and local
flows do not certify a physical device or a connected account session.
The simplified review uses its own `Logs/MenuBackgroundSimplifyReview` build
and runtime evidence; preceding review material remains preserved.

## Preserved legacy image generation prompt

> Use case: stylized-concept. Asset type: narrow portrait mobile game main menu
> looping background texture. A polished, very minimal abstract low-poly
> environment texture for Pocket Striker's six-element theme set. Deep
> blue-black base with generous calm negative space; sparse large translucent
> faceted shapes, a few very thin elegant lines, subtle atmospheric glow; low
> contrast so white UI and a character remain easy to read. No central logo,
> no large isolated object, no text, no character, no UI, no buildings, no
> stars, no busy particles. Composition evenly distributed from top to bottom.
> Use an understated contemporary game illustration style, soft matte
> gradients rather than saturated neon. Seam guidance: keep all four outer
> edge regions near the same dark base color and free of distinct cropped
> shapes; intended for seamless scrolling tile after technical edge finishing.

Each source used the shared prompt followed by its own sentence:

| Source | Additional prompt |
| --- | --- |
| `red.png` | Warm ember theme: muted terracotta red and copper, abstract curved ember ribbons and a few angular warm facets, subdued and serene. |
| `green.png` | Verdant theme: muted sage and moss green, abstract layered leaf-like facets and soft rounded growth lines, subdued and serene. |
| `blue.png` | Water theme: deep ocean blue and misty cyan, abstract broad fluid curves and softly faceted water ripple geometry, subdued and serene. |
| `light.png` | Light theme: muted champagne gold and soft ivory highlights, abstract broad luminous arcs and softly faceted sunlit planes, gentle halo shapes, subdued and serene. |
| `dark.png` | Dark theme: smoky plum and desaturated violet, abstract broad shadow crescents and softly faceted obsidian planes, very restrained violet edge light, subdued and serene. |
| `neutral.png` | No-element theme: neutral graphite and desaturated slate blue, abstract broad calm polygonal folds and a few faint silver lines, no strong element symbol, subdued and serene. |

The `BackGroundPS` element order is red, green, blue, light, dark, neutral.
