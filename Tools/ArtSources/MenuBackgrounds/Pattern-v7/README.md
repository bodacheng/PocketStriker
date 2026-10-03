# Approved color-block menu source set (2026-10-03)

The user approved the warm Red portrait color-block design at
`output/artproposals/RedGrid-Preview-ColorBlock-20261003.png` (Library version 3).
`red.png` here is a byte-identical copy of that approved file and its built-in
original. Red was not regenerated. Its SHA-256 is:

```text
d52cc22932de74214244f71b01dc24bc882853b5a98f8250226f2fa2e0fcf89d
```

The approved composition contains a large continuous shared-edge stepped diamond
lattice, staggered rows, and one simple axis-aligned solid square centered inside
every cell. Each of the five other colors used one independent built-in ImageGen
**colour-only** edit, with exactly this copied Red as its only image reference.
The whole ground, grid lines and center squares changed together into muted
moss/sage, denim/indigo, old ochre/beige, plum/mauve or warm neutral taupe/greige.
Null is warm neutral without yellow, gold or an elemental hue. No stones, icons,
stars or gap decorations were introduced. No previous art draft was referenced.

Full-image and saved-preview inspection confirms the same overall grid design,
spacing, staggered count and plain filled square design visually remain. Exact
cross-color geometric pixel identity has not been established. The slight tonal
variation already accepted in Red was retained; there were no flatten, cleanup
or retry passes for these five edits.

## Original source records

`source-manifest.json` schema v2 has exactly six final `entries`, recording
path, sourceSha, dims, original generation path/hash, only-reference path/hash,
exact prompt, preview and native dimension difference. Red's historical prompt
chain and original creation receipt are preserved separately in
`approval-evidence/`; `red-original-prompt.txt` contains that receipt's final
prompt. `approval-confirmation.json` records the later parent-reported user
approval and generation GO, superseding the historical receipt's pending status.
The original receipt was copied unchanged.

| Theme | Source | Native dimensions | Preview | Exact prompt |
| --- | --- | --- | --- | --- |
| Red | `red.png` | 941×1672 | `previews/red.png` | `red-original-prompt.txt` |
| Green | `green.png` | 941×1672 | `previews/green.png` | `green-prompt.txt` |
| Blue | `blue.png` | 941×1672 | `previews/blue.png` | `blue-prompt.txt` |
| Light | `light.png` | **941×1671** | `previews/light.png` | `light-prompt.txt` |
| Dark | `dark.png` | 941×1672 | `previews/dark.png` | `dark-prompt.txt` |
| Null | `neutral.png` | 941×1672 | `previews/neutral.png` | `neutral-prompt.txt` |

All six are RGB PNGs. Light's built-in output is one pixel shorter than the
approved reference; it was retained without resizing and reported to the parent
and the runtime owner. Every source is byte-identical to its original generated
file. Previews alone use a sips downscale to 640 pixels tall (360 pixels wide).
Hashes, source copies, reference hashes, dimensions, prompts and previews were
verified. `visual-inspection.json` distinguishes static visual QA from runtime.

## Runtime ownership and limits

`runtimeCrop` in each entry records the runtime owner's measured complete native
repeat unit `(350,580,260,262)`. `/root/opening_camera` reported best grayscale
periods of 260×262 for all six original sources and cross-color phase differences
of at most one pixel. The one-pixel Light height difference does not affect this
crop, and no source resize or ROI shift is needed. These are the runtime owner's
read-only measurements; source images here remain untouched. The source agent did not crop, recolor,
draw, resize, denoise or flatten source images, alter Assets/builder/Scene, or run
Unity. The parent and `/root/opening_camera` own actual unit/phase checks, runtime
edge finishing, cell repetition, scrolling and phone/tablet UI verification.
Runtime build and verification remain separate from these source measurements.
All Pattern-v6 and older source sets remain intact.
