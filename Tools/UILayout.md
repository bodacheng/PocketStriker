# UI layout coverage

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
- The retained legacy group-battle preparation prefab fits its two wide columns
  into portrait width locally; its title and start button retain their size.
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
text, six-digit rewards and claimed/unclaimed states. It also samples the actual
mode-layout animations. The return button, stage/mode heading and rewards have
independent columns; reusing the layer for arena clears the adventure header.
The 72 cases and 18 previews are saved under `Logs/UILayout/FightPrepare`.
No live account data, models, purchases or network calls are required by either
fixture.

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

`python3 Tools/Validation/validate_runtime_loading.py` additionally covers
battlefield replacements with missing components, null/failed loads, out-of-order
completion and scene exits. A legacy bundle without `BattleGround` can finish
loading using its instantiated transform; it cannot apply the missing component's
placement data. Rebuild and publish Addressables alongside the matching player
to restore the configured placement for such bundles. Local prefab validation
does not certify a previously published remote bundle or an installed iOS app.
