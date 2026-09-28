# Main menu background artwork

The six source PNGs were created with the built-in `image_gen` tool. The source
images are kept outside `Assets` to avoid importing an extra copy of each
texture. Run `Tools/Art/build_menu_backgrounds.py` to rebuild the six textures
used by `MainMenuScene.unity`. The script makes both axes tile seamlessly and
checks the opposite edge pixels before writing the PNGs.

## Shared image generation prompt

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
