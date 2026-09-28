# App icon source prompt

Generated with the built-in image generation tool. Source: `source.png`.
Shape reference: `Assets/OrganizedResources/InUse/ExternalAssets/SkillIcon/12.PNG`
(EX1 / SP_LEVEL 1, confirmed in `mst_skill.csv`).

Use case: style-transfer.
Asset type: final square full-bleed mobile game app icon, opaque.
Input images: Image 1 is the previous icon to redesign. Image 2 is the actual EX1 skill gem: use its OUTLINE and broad bevel geometry as the precise shape reference, not its pale figure.
Redesign Image 1 according to these corrections. Preserve exactly NINE bright gemstones in THREE rows and THREE columns, with one dark fighting-action mark embedded inside each. Replace every pointed hexagonal gemstone with the Image 2 EX1 shape: an upright nearly square EIGHT-SIDED gemstone, four equal clipped corners about 20 percent of side length, long flat top and bottom, long straight vertical left and right sides, wide inset octagonal front face and broad bevel facets. Every gem remains fully separate, with slim clear seams; no interpenetration, overlap, connecting light rays or star bridges.
Composition: nine equal gems pack the entire square canvas edge to edge. The outermost gem edges meet or slightly crop at the four canvas edges. ZERO exterior padding, ZERO empty dark rim, ZERO enclosing frame. Only small dark gaps where beveled corners meet, each gem retains its own outline. Front-facing straight-on 3 by 3 composition.
Style/medium: handmade gouache and mineral pigment painting, visible broad dry-brush strokes, soft uneven paint coverage, expressive abstract color fields within broad gem facets. Bright but comfortable jade, turquoise, warm ochre, coral and lavender mixtures; some subtle complementary color within each gem. Painted stones, not photoreal glass; few large facets, not a mass of tiny shards. No neon bloom.
Inside each gemstone put ONE DARK CHARCOAL ancient-ruin human pictograph, like a weathered cave-painting martial glyph. Abstract shadow marks made of a small round head and 4–6 bold bent, tapered brush strokes. Very simplified symbolic bodies, NO anatomy, muscles, faces, fingers, clothing, hair or realistic people. The glyph should contrast clearly with the light gem face, look like old hand-painted dark pigment with a few worn edges.
Make each neighboring pose radically different in overall silhouette and orientation. Top row: (1) low horizontal sweeping kick with one hand braced, (2) tall upward double-arm strike with feet apart, (3) airborne sideways kick with long horizontal leg. Middle row: (4) compact curled crouching guard, (5) a wide diagonal leaping strike, (6) upright straight punch to the left in deep forward stance. Bottom row: (7) inverted handstand split-kick glyph, (8) grounded bent-knee overhead block, (9) tilted spinning kick with one leg vertical. Use iconic simple signs, broad negative spaces between limbs, each glyph sits wholly inside its own gem.
Constraints: exactly nine stones, exactly nine shadow glyphs, clear consistent 3x3 alignment. Do not copy the realistic people from Image 1. No text, no letters, no numbers, no actual hieroglyphic writing, no extra symbols, no border, no watermark, no drawn app-icon rounded-corner mask.

## Export

Run `generate.py` with Pillow. Existing asset filenames and Unity GUIDs are preserved.
Standard icons use the complete full-bleed artwork. Android adaptive layers map the
artwork to the nominal 72 dp viewport in the 108 dp canvas and extend the edge
pixels into the overscan area, without a dark padding border or alpha feather.
The operating system applies its own circle or rounded-square mask.
