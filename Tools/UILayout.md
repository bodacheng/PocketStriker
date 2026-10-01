# UI layout coverage

## Full-screen backdrops

`UILayer.fullScreenBackdrops` explicitly marks background graphics and modal click
catchers that cover the entire root canvas, including the notch and home-indicator
insets. Their original parents still control visibility, fades and sorting;
text and controls retain safe-area layout. Four tutorial dimmers and the tutorial
click catcher are included, along with loading and modal screens. Startup uses a
separate full-canvas black background and empty safe-area hanger.

Run `PocketStrikerUILayoutValidation.ValidateBackdrops` to check 20 registered
rectangles across 14 screens on four phone/tablet shapes, including 1206×2622.
The report is `Logs/UILayout/backdrops-report.json`. Tutorial validation also
raycasts the top and bottom unsafe edges on all six pages; Tutorial Layout uses
the actual safe-area parent to verify the explanations and controls remain inset.

## Battle HUD proportions

`BattleHUDPresentation` lays out the live `FightingStepLayer` within its safe
area. At a 1200-unit safe width, pause is 76×72, AUTO is 164×72, player
portraits are 108×108 with 108×12 HP bars, and the three skill controls form
a 156×156 row at the bottom right. Dash and guard are 100×100 above that row;
Dream Combo keeps a 112×112 charge gauge. The movement pad is 216×216 with a
68×68 thumb knob. Dash and Dream Combo retain their elemental effects and
transparent touch targets, with no captions or UI boxes. Dream Combo's charge
ring remains visible. Other button skins use the regular rounded-rectangle MCombat
Shift assets also used by preparation. Pause uses two drawn bars, independent
of font glyph support. Generated gems have a 14-unit inset;
existing elemental particle effects shrink with their controls.
The joystick, skill row, dash/guard and Dream Combo move together 48 reference
units upward, multiplied by the same safe-width scale. Their centers are now
208, 162, 322 and 330 units above the safe bottom, respectively. The reserved
bottom control area rises from 344 to 392 units; the battle camera's middle
area starts above it. Control sizes and transparent effect-only touch targets
stay the same.

World status uses compact 94-unit bars with warm enemy and green player
colors; Group battles use 60-unit bars and omit the floating EX chips. World
widgets follow the current camera directly, hide outside its viewport, and
avoid the fixed portrait rail. Rotation reserve units do not display floating
HP bars. Player markers are independent of the portrait grid.

Run `PocketStrikerBattleHUDValidation.ValidateBatch` without `-quit` for local
HUD geometry, input/raycast checks and rendered previews. Its report and PNGs
are saved under `Logs/UILayout/BattleHUD`. The fixture uses production layout
and UI components with local posed fighters, without a live account or battle
simulation. Run `PocketStrikerGroupBattleValidation.ValidateBatch` for the
restored adventure Group data, count expansion, clone ownership and automatic
control contracts. Live startup is checked separately by
`PocketStrikerValidation.SmokeStartup`.

Battle cameras frame the complete model bounds of every living, fielded unit;
rotation reserves are excluded. `AllUnitsBattleCamera` fits perspective against
portrait aspect ratio, safe area and the space between the HUD header and controls,
excluding the player portrait rail. Focus does not narrow the target collection.
Independent profiles use a 32° pitch for duels, 33° for normal multiplayer and
46° for Group battles. Either team's MultiRaid mode selects the multiplayer
profile. Preparing never fits remote loading positions; the first CountDown
frame immediately fits the final models, with no inherited staging distance.
Composition follows fielded root positions with cached body heights, so changing
limb and weapon bounds do not move the entire scene. `BattleCameraStabilizer`
uses a 2% view-relative pan dead zone, 0.26-second Duel and 0.32-second crowd
horizontal follow, and slower vertical follow to absorb contact and hit reactions.
Large deliberate movement accelerates horizontal follow toward 0.12 seconds once
lag exceeds four pan windows (at least 0.8 world units), reducing unnecessary
safety zoom caused by the center falling behind.
A cached neutral silhouette reserves a circular horizontal footprint for each
model, so turning a body or long weapon does not abruptly change portrait zoom.
The solver fits this upright cylinder directly instead of adding the excessive
corners of a square footprint; actual live model bounds still set the safety floor.
A 12% framing reserve, peak hold and 6% zoom hysteresis suppress repeated outward
and inward adjustments. Normal expansion eases over 0.24 seconds; a full-model
safety floor expands immediately for sudden separation, airborne models or a
viewport change, including while paused. Duels retain a 0.45-second inward
exponential time constant and 0.35-second peak hold; MultiRaid/Group retain a
two-second inward constant and 0.65-second peak hold.
Automatic orbit applies only to Duels, with an 8-degree angular dead zone,
smoothed rotation and remembered orbit direction. Separation must exceed 3.2
units to start orbiting and fall below 2.4 to stop. Crowd views retain their
heading; manual orbit remains available. Large frame delays cannot advance
the tracking/orbit filters by more than 50 milliseconds at once.
When a rotation fighter is dead, inactive or temporarily absent while a living
replacement remains, the duel camera holds its established center, yaw and inward
distance instead of moving onto the survivor. It may still expand to keep the
survivor visible; normal tracking resumes when the replacement is fielded. Camera
updates run after animated models and HUD layout. `PocketStrikerBattleCameraValidation`
projects full bounds through the production pose, including 200-unit formations.
`PocketStrikerBattleCameraSmoke.StartBatch` runs the actual fight scene in local
Play mode, covering 1v1, rotation, six-unit combat, 100-versus-100 Group combat and
an in-scene retry. It checks the initial CountDown pose and every model corner
after live camera updates. Its invulnerable fighters isolate camera behavior;
it does not validate battle outcomes.
Its local account fixture excludes shop/login services; reports and screenshots
are saved under `Logs/CameraFraming/Playmode`.

`PocketStrikerBattleCameraStabilityValidation.ValidateBatch` compares all three
production profiles against the previous camera at 30/60/120 fps. Synthetic
animated bounds and root punches isolate unwanted motion; fixed world markers
measure screen travel and camera velocity variation. It also checks narrow-model
quarter/half turns and continuous rotation, deliberate movement/orbit, sudden
separation/height changes, pause-time safety fitting,
replacement holds and fresh-battle resets. The report is under
`Logs/CameraFraming/Stability`; complete live model framing is checked separately
by the Play-mode smoke. These comparisons are not device performance measurements.

For transition regression, run `PocketStrikerBattleCameraSmoke.StartBatch` without
`-quit` and set `POCKETSTRIKER_CAMERA_REVIEW` to a new report label. Add
`POCKETSTRIKER_CAMERA_SIZE=390x844` for the narrow portrait case. Reports, per-frame
camera/model telemetry and timed PNGs are saved in
`Logs/CameraFraming/Transitions/<label>`. Sixteen scenarios cover real portrait
pointer switching, enemy switching, death replacement, temporary target loss,
in-scene retry, scene reload, mixed-model switching and controlled
near/separated/airborne envelopes. Assertions catch delayed inward convergence,
replacement zoom swings and projected model clipping. This is a local Self battle
fixture, not an authenticated match or a validation of jump/knockback mechanics;
retry resets dead fighters, but there is no separate in-match resurrection flow
covered by this fixture.
Use `Tools/Validation/camera_telemetry.py <new report.json> --baseline <old report.json>`
to compare settled transition motion and check that no model corners are clipped.

Models initialize at separate temporary positions while their animation setup
requires them to remain active. Normal starting placement then synchronizes root
and Rigidbody positions. Large Group rosters expand beyond the authored 30-slot
formation instead of indexing outside its array.
Group preparation sizes the arena from the placed models, synchronizing its
physics boundary, sensor, ring and ground. Cached original scales prevent retry
growth; ordinary combat restores the authored radius and scales. Loading skips
battle boundary correction until the countdown so temporary positions remain
isolated. `PocketStrikerGroupFormationValidation.ValidateBatch` checks real scene
formations, model footprints, arena capacity and restoration.
Shape/Local ring particles also scale their original size multipliers and restart
on radius changes; changing only the parent transform leaves World-space glow
particles at the old radius. The validator samples actual emitted particle sizes.

## Battle tutorial callouts

Run **PocketStriker → Validation → Tutorial Layout** or the batch entry
`PocketStrikerTutorialValidation.ValidateLayoutBatch` (without `-quit`).
`Logs/Tutorial/Layout/report.json` and 84 PNGs cover the six battle tutorial
pages plus the Dream Combo explanation in English, Japanese, and Chinese at
540×960, 375×667, notched 390×844, and 768×1024.

Battle explanations follow the actual joystick base, attack buttons, player
auto switch, and Dream Combo button. HP/energy uses the combined bounds of the
current player's real health bar and charge container. The layout updates after
language, control-position, and safe-area changes. Wrapped CJK text has explicit
leading space; the validation compares complete generated text against visible
glyphs, rather than trusting `preferredHeight` alone. Panels and arrows must
stay in the safe area, and explanation panels must not overlap.
The three skill explanations read left to right in equal-width columns above
the entire lower control area. Direction and Dream Combo explanations also
move above that area when a side placement would cover a control. The checks
reject panels over any live HUD target, including on narrow phones and with
wrapped Japanese text. Tutorial entry refreshes the HUD before positioning
callouts, including after legacy AUTO animation bindings are rebound.

The preview copies the real battle UI and one player icon without loading
accounts or combat. Three local native skill sprites populate the real buttons;
the Dream gauge begins empty. Runtime particle effects and battlefield models
are omitted; the highlighted rectangles identify the actual controls. The
separate **Tutorial** check still validates click interception and page flow.

`PocketStrikerTutorialPlaymodeSmoke.StartBatch` exercises the actual published
first quest through `FightLoad.Go` and its production tutorial caller. It checks
all six pages, the first click lock while paused, later pages with live animated
models, callout targets, overlay sorting, real raycasts, force-AUTO and the Dream
Combo overlay. Reports and native screenshots are under `Logs/Tutorial/Playmode`.
This smoke uses a local account and Addressables Fast Mode, removes shop startup
services, and makes both teams invulnerable to isolate tutorial UI; it is separate
from combat victory/reward checks and physical-device testing.

Run **PocketStriker → Validation → UI Layout** in the Unity Editor, or call
`PocketStrikerUILayoutValidation.Validate` from the existing Unity validation
runner. The generated report is `Logs/UILayout/report.json`.

The check discovers registered `UILayerLoader` prefabs, rebuilds built-in layout
groups, and evaluates safe-area bounds and the `top` / `middle` / `bottom`
regions at 540×960, 375×667, a notched 390×844 and a portrait 768×1024 tablet.
Text and interactive children must stay inside their assigned region. Repeating
the layout operation must not change its result.

Settings additionally exercises all six tabs, both email states and both device
binding states through explicit preview-only visibility fixtures. The skill
editor also exercises its normally hidden combo explanation and close button.
These fixtures only activate copied UI objects; they never invoke account or
gameplay methods.

Settings content is centered as a group within the middle window, using visible
labels and controls rather than the oversized panel backgrounds. The same
centering helper runs after tab, account/device-state, language and viewport
changes. Validation checks both the center offset and repeated-call stability.

## Screen inventory

| Flow | Main layers | Shared layers / additional states |
| --- | --- | --- |
| Startup, title, story, loading | `TitleScreenLayer`, `TitleBgLayer`, `ProgressLayer` | Login, nickname, story skip, loading percent |
| Home | `FrontLayer` | `UpperInfoBar`, `LowerMainBar`; developer skill-test buttons |
| Adventure chapters | `ArcadeTop` | `LowerMainBar`, `ReturnLayer`; three stage rows and the preview skill grid |
| Random boss / event | `EventBattleTop` | `LowerMainBar`, `ReturnLayer` |
| Arena | `ArenaLayer`, `RankingLayer`, `ArenaAwardLayer`, `ArenaNewSeason` | `LowerMainBar`, `ReturnLayer`; award modal |
| Fight preparation | `FightPrepareLayer`, `FightPrepareLayer_gb` | `LowerMainBar`, `ReturnLayer`; rotation, team and evolution states |
| Team editing | `TeamEditLayer` or `TeamSingleSelectLayer` | `UnitsLayer`, `LowerMainBar`, `ReturnLayer` |
| Training | `SelfFightLayer` | `UnitsLayer`, `LowerMainBar`, `ReturnLayer` |
| Unit collection | `UnitOptionLayer` + `UnitsLayer` | `LowerMainBar`, `ReturnLayer`; these layers are complementary, not a modal pair |
| Unit skills | `SkillEditLayer`, `SkillEditTipLayer`, `UnitInstructionLayer` | `LowerMainBar`, `ReturnLayer`; tips and tutorial states |
| Skill stones | `StoneListLayer`, `StoneUpdatesConfirm` | `LowerMainBar`, `ReturnLayer`; filter, sell and upgrade states |
| Summoning | `GotchaLayer`, `GotchaResultLayer`, `DropTableInfoLayer` | `UpperInfoBar`, `LowerMainBar`, `ReturnLayer`; currency header on the summoning page |
| Shop | `ShopTopLayer`, `BuyNoAds` | `UpperInfoBar` with toolbar buttons hidden, `LowerMainBar`, `ReturnLayer` |
| Settings and account | `SettingLayer`, `NickNameLayer`, `AskIfLinkDeviceLayer` | `LowerMainBar`, `ReturnLayer`; account, language, device and support tabs |
| Mail | `MailBox`, `MailDetailView` | `LowerMainBar`, `ReturnLayer` |
| Battle | `FightingStepLayer`, `CountDownLayer`, `InBattleEvolution` | Pause, both auto controls, three side unit icons, skills and tutorial states |
| Battle pause and results | `FightScenePauseSupport`, `ArenaFightOver`, `CommonFightResult` | Win/loss, rewards, again/next and story states |
| Shared modal UI | `PopupLayer`, `HighLightLayer`, `ProgressLayer` | Confirmation/warning, tutorial curtain and blocking loading states |

`LowerMainBar` remains present across normal main-menu process transitions;
`ReturnLayer` is present while forward navigation has a history entry. Composite
checks must include these actual shared layers, rather than comparing only the
three regions of one prefab. Blocking modals intentionally cover the background
screen and should be checked independently.

## Layout corrections

- Preview skill grids in adventure/event pages belong to the upper region.
- Battle side icons, opponent auto control and dream-combo button belong to the
  middle region; their readable sizes are retained. The player's auto control
  remains fully inside the top region.
- Home header actions use a horizontal row, and currency/VIP graphics stay inside
  the safe top edge. Summoning titles sit below the shared currency header.
- Navigation labels stay above the safe bottom edge. Arena ticket content,
  stone filters, skill-editor model preview and purchase icon stay in their
  respective regions.
- Group preparation uses opposite vertical portrait rails, central model and
  skill previews, and a horizontal population selector above the start action.
- The account reset-password button's label stays within the button. The skill
  editor's combo explanation has a dedicated text area and close-button row,
  both inside the middle region, with readable 32-point text.

## Runtime checks and intentional overlays

The static report does not authenticate, contact services, make purchases, or
execute game setup code. Use the runtime game to check loaded lists and localized
text, every settings tab, battle preparation variants, battle controls, evolution,
win/loss rewards, summoning results, and modal open/close transitions. Inspect both
the small-phone and notched-phone layouts, then resize the window to the tablet
and standard-phone cases. Check during stable states and after transitions finish.

Animation waypoints, selected-frame templates, tutorial curtains, effects, and
world-tracked battle bars are not ordinary fixed-region controls. In particular,
summoning star waypoints and result-animation entrance positions can start off
screen. The title background's scrollbar is hidden but remains a programmatic
scroll-value source. These exceptions must not hide an ordinary visible button
or text label from validation.

## Safe-area behavior

All scenes use a distinct safe-area child, including older scenes that stored the
canvas itself as the UI hanger. Canvas scaling remembers the authored reference
resolution and reapplies it when the viewport or device safe area changes.
Repeated initialization cannot compound the notch inset.

The shared region layout preserves the authored header/footer space inside the
safe bounds. Full-screen layers retain full-screen backgrounds while their three
content regions use the same safe height as ordinary pages. The validator checks
both parent choices for the six layers used through the full-screen loader route.

## Stage-card readability

Run **PocketStriker → Validation → Check Stage Cards** for 432 combinations of
normal/evolution cards, 600/750 widths, 200/240 heights, one/four portraits,
three battle modes, three energy states and English/Japanese/Chinese labels.
Stage number and mode sit together at the left; the mode uses colored text with
no filled panel. Enemy portraits are centered vertically against the whole card.
Rewards form a compact group at the lower left, while the optional energy label
sits immediately beside the battle mode. The validator also checks
locked/unlocked transitions, large reward amounts, clipping and click targets.

`Logs/UILayout/stage-cards.json` records the result. Four `stage-cards-*.png`
renders use real card and avatar prefabs with local portrait art; the fourth
sheet exercises the narrow, taller card.

Run **PocketStriker → Validation → Startup Smoke** for two 20-second screensaver
battles, including reload, safe-area initialization and advancing frames. The
report is `Logs/Revival/startup-report.json`. The smoke check preserves an already
open startup scene's unsaved edits and does not log in. It confirms startup and
battle stability, rather than every dynamically populated menu state.

## Populated reward pages

**PocketStriker → Validation → Arena Awards** invokes the real reward-page
population methods with 6 and 16 local reward records at all four device sizes.
It verifies row bounds and scale, repeated population, clipping, and accessibility
of the first and last rewards. The page now has consistent row heights and a
scrolling viewport below a fixed title. Results and images are saved as
`Logs/UILayout/arena-awards.json` and `arena-awards-*.png`.

**PocketStriker → Validation → Check Fight Preparation** invokes the actual
adventure, event, legacy group-battle and arena preparation paths with localized
text, six-digit rewards and claimed/unclaimed states. The current 160 cases and
26 previews use local portrait and gem art, the shared menu background/navigation,
and four phone/tablet safe-area shapes. A Stage 55 evolution fixture shows four
enemies and one hero. Its character uses a local idle-pose model; the remotely
supplied skeletal enemy from the reference screenshot is not loaded.

The standard preparation page places the stage heading and a compact reward row
above the character/skill preview. Enemy and player rosters have separate dark
panels with aligned headings and portraits; the edit action sits in the player
header, and FIGHT has a centered action area below both teams. Element colors
remain visible in softened portrait borders. Slot styling addresses only the
authored frames, preserving gem artwork and status effects. Layout responds to
safe-area, viewport, roster and language changes; the legacy mode animations
retain visibility while their positions are corrected after sampling. FIGHT,
Edit, the battle mode selector and the stage-list button use a shared sliced fill/outline pair imported
from MCombat's Shift UI pack. The symmetric rectangular buttons have modest
rounded corners and a clean thin outline. A warm gold outline marks the primary action; muted
cyan marks the secondary actions. The actual selectable owns the entire new
rectangular hit area, pressed/disabled tints and tutorial guide outline pulse.
Group preparation adapts the original prefab through `FightPrepareLayer.GroupPresentation`.
The two rosters occupy the left and right sides directly below the header;
each player portrait has rectangular minus/count/plus controls. Long rosters
scroll vertically, including drags that start on a portrait. The center contains
two independent character previews and skill grids; population choices and FIGHT
sit above the main navigation bar. Counts come from the live configured limits.

Preparation characters use `DedicatedCameraConnector.EnableUIPresentation`:
each camera renders transparent output into its own RawImage above the panel.
This prevents overlay background panels from darkening models. Separate world
slots isolate simultaneous previews, and resize/disable/destroy manages textures.
Other model viewers keep the original camera-stack path unless explicitly enabled.
`PocketStrikerCameraLoadingValidation` checks real 3D output, transparent composition,
two-model isolation and texture cleanup, including a deliberately darkened control.

Tapping a skill gem opens a scrollable localized detail sheet with its name,
category, EX tier, authored introduction and AI activation distance. Preview
closes the sheet and plays that skill on the selected character. All preparation
entry routes bind the detail action, including Arena and both group-battle skill
grids; changing the character or route clears a stale selection. MCombat's
`SkillManagement.md`, `mst_skill.csv`, `skill_ai_attrs.csv` and `skill_name.csv`
provide the reference. The original 84 populated localized introductions are
retained, and the 12 missing dragon skills now have conservative descriptions
of their defined actions. All 96 master skill IDs have English/Japanese/Chinese
names and introductions. AI activation distance is labeled separately from a
skill's hit area, and static analysis damage/HP estimates are not presented as
combat rules. Future absent introductions fall back to configuration metadata.

`PocketStrikerFightPrepareValidation.ValidateDesignBatch` runs preparation,
bundled fonts, global UI and stage-card checks together and returns a failing
exit code if any report fails. Its summary is saved under
`Logs/UILayout/FightPrepare/design-summary.txt`. The fixtures require no live
account data, purchases or network calls.

## Verified result (2026-09-27, Unity 6000.5.1f1)

- 44 registered layers, 236 layout cases, and 88 shared-layer compositions:
  passed with zero errors or warnings. This includes full-screen parenting,
  Settings visibility variants and the skill combo explanation.
- Both legacy safe-area initialization paths passed repeated-initialization checks.
- 432 stage-card cases passed, with four rendered previews. Both combo explanations fit in all three supported
  languages at all four test sizes (24 text-height checks).
- Settings content centering passed all 32 tab/state/size cases with zero center
  offset. The 8 populated arena reward cases passed, including unchanged title
  position and matching title pixels at both scroll endpoints.
- The 72 populated fight-preparation cases passed across three languages, two
  portrait widths, six entry routes and both reward-claim states.
- Startup smoke passed at 540×960: two battles advanced for at least 20 seconds
  each, including re-entry and pool lifecycle checks.
- Existing runtime-loading and battle-mode scripts passed 56 and 745 checks.

All authored screen previews and the added Settings/combo-state previews were
visually reviewed. Dynamic network-loaded lists, all animation frames and device
hardware behavior remain outside this local validation result.

## Battle loading screen

Run **PocketStriker → Validation → Battle Loading Screen** for both the tip
background and progress overlay at five portrait sizes, including 1206×2622
with simulated notch/home-indicator insets. `Logs/UILayout/battle-loading.json`
checks full-canvas background coverage, safe content regions, repeat resizing,
the screenshot's Chinese glyphs, and the three local battlefield prefab root
components. Device-specific previews are saved under `Logs/UILayout/previews`.
The tip layer uses the full-screen loader route; its content still uses the
shared safe-area regions. Both loading prefabs use the bundled OFL Noto CJK font
and nonzero transform scales for reliable text positioning.

`LoadingScreenLayout` gives the title, measured localized body and progress footer
separate safe-area regions. The compact rectangular progress bar stays below
the tip text. `PocketStrikerCameraLoadingValidation` also checks all six loading
tips in three languages at six screen/safe-area shapes.

`python3 Tools/Validation/validate_runtime_loading.py` additionally covers
battlefield replacements with missing components, null/failed loads, out-of-order
completion and scene exits. A legacy bundle without `BattleGround` can finish
loading using its instantiated transform; it cannot apply the missing component's
placement data. Rebuild and publish Addressables alongside the matching player
to restore the configured placement for such bundles. Local prefab validation
does not certify a previously published remote bundle or an installed iOS app.
