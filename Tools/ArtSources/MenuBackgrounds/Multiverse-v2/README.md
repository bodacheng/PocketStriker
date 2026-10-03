# Multiverse menu background sources (2026-10-02)

Six new PNG source images extend the approved multiverse login illustration's
cut-paper / angular block-print language to the main menu. The old six PNGs in
the parent directory remain untouched. This directory supplies source artwork;
runtime texture assembly, two-axis loop finishing, scene integration and actual
menu screenshots are performed by the parent task.

All six files are **941×1672 RGB PNG**. They were generated using the **built-in
ImageGen tool**, one distinct call per theme. There was no CLI/API generation
and no programmatic recoloring. The full-resolution files are byte-identical
copies of the generated originals. `previews/` contains 360×640 sips
downscales for quick inspection.

The Red source was generated first and visually approved before the other
five calls. Red used the approved login illustration only as a shape/print
style reference. Each subsequent call used the approved Red source as its
primary composition/value/style reference, with the login illustration as a
secondary angular shape reference. Existing login pixels, gameplay and
runtime assets were not edited by this source-art task.

The design reserves a broad quiet slate area behind the actual 3D character.
Thick partially clipped octagonal skill-stone rims and rough angular impact
ink distinguish the side margins from the old fine-line polygon wallpaper.
Very subdued peripheral relief fragments mix broken columns, angular mountain
contours and future stepped blocks. There are no characters, faces, UI,
lettering, symbols, complete central gems or neon filaments. The six themes
share the same visual hierarchy:

| File | Accent palette |
| --- | --- |
| `red.png` | Ember, terracotta and copper |
| `green.png` | Jade and verdigris |
| `blue.png` | Cobalt and icy cyan |
| `light.png` | Warm gold, ochre and champagne |
| `dark.png` | Obsidian and muted violet |
| `neutral.png` | Cool slate, graphite and silver; no gold or warm element hue |

`prompts/{theme}.txt` contains each prepared theme specification.
`prompts/{theme}-call.txt` contains the exact prompt submitted to the built-in
tool, including reference roles. `source-manifest.json` records each generated
original path, reference paths, saved source, dimensions and SHA-256.

Source-image QA confirms consistent shape language, an empty central display
field, dark upper/lower ends and distinct theme palettes. The generated files
retain substantial side decoration and some clipped rims at the side edges.
They are **not yet a claim of seamless tiling or actual UI readability**.
Technical edge finishing and button-zone contrast must be verified on the
parent task's actual processed textures and menu renders. The full-resolution
sources are retained so those reversible finishing choices can be reviewed.

SHA-256:

| Theme | Source SHA-256 |
| --- | --- |
| red | `3e16fc485f75cd847617287ada8a83cd21c556e3c8dbbc2eb6bc55472c557a90` |
| green | `850fe6766b78b3d4813c1f23d5a78f93554190a54346f9166e5d4840701ef5e0` |
| blue | `eef7be773602b4a60751926c50c3a2f2861f3c98b686a3db377020a42ed6b921` |
| light | `43f419bae3410d27e2878ee183de18d5365499ad686c85f6c1d88be0131dd690` |
| dark | `713ceefe620f174a5a7a003cc72a6f9d98423cc265ce79ff2059c2a7c25e68a8` |
| neutral | `13c97021a8065b03afdd240d38dca05cf669feafae95000accc6a9bd0dd09a37` |
