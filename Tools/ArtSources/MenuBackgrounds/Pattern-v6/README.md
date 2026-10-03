# Sparse single-cell menu sources (2026-10-03)

The user found v5 too dense and disliked per-cell differences and decorations in
the gaps. These six square master cells contain only one centered hollow
pixel-step diamond each. Interior and margins have no marks. All older sources
and Red drafts are preserved. This agent did not edit Assets or scenes.

Red used three built-in ImageGen edits: initial simplification from v5 Red,
one surface cleanup, then one outline-contrast reduction. The initial and cleanup
drafts are `red-initial.png` and `red-strong-outline.png`. The parent visually
accepted the softened `red.png` geometry and strength. Each of the five other
themes used one independent built-in edit, with only the softened Red as reference.
No login or other artwork was supplied. No procedural drawing, filtering,
recoloring or denoising was used.

All six final sources are 1254×1254 RGB PNGs copied byte-identically from their
generated originals. `source-manifest.json` schema v2 has **exactly six final
`entries`**, with theme, path, sourceSha, dims, original path/hash, ref path/hash,
exactPrompt and previewPath. The two earlier Red images are separate `drafts`;
they are not counted as final entries. Total built-in calls: eight.

`red-prompt.txt`, `red-flat-edit-prompt.txt` and
`red-soft-outline-edit-prompt.txt` record the Red generation chain.
`{green,blue,light,dark,neutral}-prompt.txt` record the exact variant prompts.
`previews/{theme}.png` are 480×480 sips downscales. Original source bytes, hashes,
reference hashes, RGB dimensions, prompt files and preview dimensions were checked.

Full-image and saved-thumbnail inspection confirms one hollow diamond per cell,
empty center and no stars, dots, crystals, chevrons or neighboring motifs. The
shape occupies approximately 41.7%×40.9% of each cell, near the requested 45%.
Measured bounds differ by at most one pixel across the six independently
generated colors. No claim of identical source geometry between colors is made.
Very weak native background variation remains; the parent allowed weak texture
when visually clean. Exact two-color uniformity is not claimed.

`inspect-master-cells.py` reads PNG pixels and source provenance without editing
images. It produces `pixel-inspection.json` for the final six entries and
`red-pixel-inspection.json` for final Red. The stronger prior Red measurement is
preserved as `red-strong-outline-pixel-inspection.json`. Native generated colors
did not exactly meet the requested 13–16/255 luminance difference; actual values
below were reported to the parent for runtime readability assessment.

| Theme | Source | 480px preview | Dominant outline/base luminance difference |
| --- | --- | --- | --- |
| Red | `red.png` | `previews/red.png` | 21.91/255 (prior strong Red: 33.18) |
| Green | `green.png` | `previews/green.png` | 29.50/255 |
| Blue | `blue.png` | `previews/blue.png` | 24.80/255 |
| Light | `light.png` | `previews/light.png` | 32.46/255 |
| Dark | `dark.png` | `previews/dark.png` | 22.84/255 |
| Neutral | `neutral.png` | `previews/neutral.png` | 21.35/255 |

The outlines remain faded grey moss/sage, denim/indigo, old beige/ochre-grey,
old aubergine/grey-purple or matte neutral grey-silver. Light has no metallic
gold effects; Neutral has no warm gold or elemental hue.

The parent plans to copy an identical cell into a 6×6 runtime array, preserving
exact equality within each color and reducing density from v5's 16 staggered
rows. This source agent did not run Unity or verify the resulting menu, scrolling,
exact repeated-tile equality or chosen density. Runtime source selection and
readability acceptance belong to the parent's actual phone/tablet preview.
