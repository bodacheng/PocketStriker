# Multiverse login background (2026-10-02)

The selected background is `Assets/AIStory/Art/PocketStrikerLoginMultiverse-v3-abstract.png`.
The `LoginArt` entry in the Config Addressables group resolves to GUID
`c8c2decf086d4248b64216819a8778f8`.
`TitleBgLayer.SetupLogin` and the production 2D startup branch consume the same
key. No online content was built or published.

The previous `Assets/AIStory/Art/PocketStrikerLogin.png` and its `.meta` are
retained unchanged, along with the previous multiverse v2 PNG and its `.meta`.
To restore v2, change only the `LoginArt` entry's GUID to
`8c54ba2dd14e423ea81f0dcc0391e159`; the original image uses
`4ddbfb25f54e4b1e8809f107ff3294da`.

The selected v3 uses one further built-in ImageGen edit of v2, with the exact
prompt in `abstract-characters-v3-prompt.txt`. Character faces, garment details,
armor and mechanical anatomy were replaced with anonymous angular silhouettes
and broad color blocks, while retaining the existing composition, character
placement, skill stones, architecture and title/login negative space. All
visible figures were simplified consistently. The generated 941×1672 RGB PNG
was copied directly into Assets without image processing; provenance, GUID,
dimensions and SHA-256 are in `abstract-characters-v3-source.json`.

Run **PocketStriker → Validation → Login Artwork Offline Smoke** in the stopped
editor (or `PocketStrikerLoginArtSmoke.StartOfflineAfterBatch` in batch mode).
The v3 report and rendered screenshots use
`Logs/LoginArt/AbstractCharacters20261002/after`. This checks the production
`LoginArt` mapping, four phone/tablet dimensions and the account modal without
logging in or making account requests. The v3 run passed all four viewport
sizes (540×960, 375×667, 390×844, 768×1024), with eight pointer clicks,
zero runtime errors and no account requests; the phone and tablet title
screenshots were visually reviewed. The earlier reports described below
remain as v2 history.

The v2 background used the built-in ImageGen tool, with one initial generation and one
targeted style revision. `initial-prompt.txt` and `final-revision-prompt.txt`
contain the exact prompts. The project-local original outputs remain at
`generated_images/exec-75d877dc-9aed-4ab2-aeaa-cd486e88e143.png` and
`generated_images/exec-9718fc47-d848-41ec-912f-3941bfd28d2b.png`. The second
output is the selected asset, copied without image processing, at 941×1672 RGB.

The art was informed by actual saved combat renders in
`Logs/CombatImpact/Closure375`, roster portraits (including the cyborg, wolf
beast and ice golem) in `ExternalAssets/Unit_Icon`, and the actual faceted round
skill stones with white combat pictograms in `ExternalAssets/SkillIcon` and
`Logs/UIArt/Live/final-reviewed/stones.png`. The previous login screenshot is
`Logs/LoginArt/after/title-390x844.png`. The new image makes the amber skill
stone the focal point, groups six era/world archetypes around it, and replaces
detailed landscape portrait rendering with flat angular poster forms. Pale
upper space supports the existing dark title; the dark lower area supports the
existing white start text and login controls.

The new sprite has a custom top-center pivot. `TitleBgLayer` uses each sprite's
normalized pivot as the aspect-fill anchor, so the new image retains its pale
title space on shorter tablet screens while the old sprite keeps its center
composition. Top alignment crops away the poster's dark footer on tablets.
Only top-anchored art with more than 4% vertical crop enables a native navy
gradient in the bottom 26% of the viewport; it reaches full strength at 20%
crop. The upper edge fades out before the measured primary-stone rectangle.
Phone layouts with no vertical crop and the old center-pivot image leave this
gradient disabled. The generated PNG pixels remain unchanged.

The earlier v2 fixtures ran in separate Unity batch editors without `-quit`
(the after entry now targets v3):

```text
PocketStrikerLoginArtSmoke.StartOfflineBaselineBatch
PocketStrikerLoginArtSmoke.StartOfflineAfterBatch
```

These entries use Scene1's actual camera, canvas and EventSystem, but disable
`StartUpPresentation` before its network startup can run. They load local
settings/language and production Resources title layers, resolve `LoginArt`
through Addressables Fast Mode, and open/cancel the actual account modal through
native EventSystem raycasts. They do not press Start, send credentials, log in,
or request account/story/shop/reward services. Baseline explicitly injects the
retained old sprite in memory. After uses the saved production mapping without
an artwork override. The older `StartAfterBatch` includes production login;
it is intentionally not needed for this art regression.

Reports and rendered screenshots are written to
`Logs/LoginArt/Multiverse20261002/{baseline,after}`. Both fixtures exercise
540×960, 375×667, 390×844 and 768×1024, check full-screen coverage, title and
account visibility, modal control visibility and eight pointer clicks. After
also checks that the measured central skill-stone rectangle survives aspect
fill and does not intersect title/account rectangles. Layout assertions and
saved PNGs complement each other; geometry alone is not a contrast or visual
style verdict. These are Editor dimensions; native safe areas and physical
device rendering remain outside this fixture's scope.

SHA-256:

```text
Old: 3317bd239bac9681857c30c5d3f33ceaa6c1fd2fdd7bd72d0b5483fcb52398be
V2: 0a15264249f5fd773c36d9297a97cc5efa07eb5e084d9553685bfd167af3adc2
V3: 4950afbcfc743b00c618255ed84f11d23017c1d9ad5300b884132a82bd961f52
```
