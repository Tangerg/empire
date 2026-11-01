# Implemented-level polish — 2026-11-01

Baseline: `aff5d905303f55a639ab86e9395022a8da3ed9de`. Scope is the seven
implemented authored maps, not new chapters. C01-08–16 retain historical rules
but their new scene bindings are `runtimeReady=false`; they are not counted as
finished new maps. Chapter one ends after battle 05; 06 and 07 belong to chapter two.

## Acceptance matrix

Historical results are not new-run acceptance. This run has statically audited all
seven bindings and run the four existing C04–07 geometry validators. Actual play,
visual inspection and synthetic fixtures must remain separate evidence categories.

| Map | Objectives / timing | Art contract and priority acceptance |
| --- | --- | --- |
| 01 双子丘陵 · 36×30 | 莱娅 survives; defeat enemies; first death exposes command clue | Independent ground, seven object textures / 97 instances. Recheck cliff/door/adjacent actor selection and HUD. Historical normal-AI victory is not this run's result. |
| 02 三桥河谷 · 41×38 | All three refugees east, designated ballista destroyed; any refugee loss fails; center bridge ceasefire through enemy T3 | Independent ground + 41 standing objects (batch 5); normal escort, refusal feedback, bridge-rail picking, loss/settlement. |
| 03 赤石围城 · 101×52 | Control link neutralized OR HQ captured; T4 allied reinforcements; node/shield/T15-engineer routes break link; limit 18 | Whole map + 64 occlusion objects. Large-map camera, dense buildings, reinforcement landing, allegiance transition and viable normal-AI route. |
| 04 黑旗营地 · 50×30 | Three clues, then at least one clue-carrier (莱娅 or 米蕾) withdraws | Whole map + 57 occlusion objects. Empty doorway vs real adjacent guard; clues, withdrawal and carried roster. Historical erroneous doorway probe has been corrected, not weakened. |
| 05 焚村令 · 50×32 | 莱娅 and at least two civilians survive seven rounds (normally human T8); enemy reinforcements T4; limit 11 | Whole map + 45 occlusion objects. Complete normal-AI T8 defense with carried C04 roster remains an open historical acceptance item. |
| 06 白河夜渡 · 50×32 | All four escorts reach north bank; pursuit T4, bridge warning T6, limit 13; warning is not physical bridge destruction | Whole map + 28 occlusion objects. Historical normal-AI T8 victory; this run not replayed. Recheck covered endpoint actor badges, zoom/load and carryover. |
| 07 无主之城 · 96×64 | Granary and militia-camp capture achievements latch; not simultaneous ownership; 莱娅 survives; limit 15 | Whole map + 55 occlusion objects / 16 capture points. Corrected map's complete normal-AI victory remains open; historical T9 route skipping enemy actions is not victory evidence. |

For every map: verify actual scale and ground/building/actor integration, picking
and passage, objective/reinforcement timing, camera/HUD obstruction, input feedback,
save/load, and actual win/loss settlement. C03–07 painted-in objects remain an explicit
art/interaction debt, not a claim of C01-style independent composition. Work will
progress map by map rather than silently marking static validators as full polish.

Sources: `godot/data/grayflag-campaign{,-levels}.json`, `godot/data/scenes/c01-*.json`,
`FORMAL-THREE-BRIDGES.md`, `THREE-BRIDGES-ESCORT.md`, `REDSTONE-CAMPAIGN.md`,
`BLACKFLAG-CAMP.md`, `REED-VILLAGE.md`, `WHITE-RIVER.md`, `LUOJIA.md`, and their
`art-provenance/*-geometry.json` contracts.

## Batch 1: battle-only minimum client area

The actual 640×480 C01 selected-archer GUI reproduces End Turn overlapping the
command tray. This is fixed through an explicit **960×640 battle minimum**, not
by claiming a new responsive 640 battle layout. Existing art, fonts, HUD geometry
and battle rules are unchanged. Campaign menus, narrative, preparation and results
retain their inherited host minimum. Preparation explicitly explains the resize.

The application owns this policy at committed view activation. Hidden replacement
preparation must not mutate the live window. A larger embedding host minimum is
preserved; leaving battle or unmounting restores the exact inherited constraint.
The application does not shrink the user's window again or change its display mode.
Small normal windows grow and recenter only upon battle entry. Screen capacity is
checked before starting/loading a battle: windowed mode accounts for usable screen
area and decorations; fullscreen uses physical screen size. Insufficient space
leaves progress unchanged with a recoverable notice. Headless contract execution
has no physical screen and therefore bypasses that display-capacity preflight.

Evidence root (outside repository): `../empire-evidence-20261101/`.
Validation and final GUI results are recorded below when complete.

### Batch 1 validation

- Build: zero warnings/errors; whitespace and 305 script UID/resource metadata passed.
- Campaign navigation **330 assertions**, campaign UI **200**, camera integration
  **277**, result flow **154**; all four processes exited 0 without engine errors
  or leaked-object diagnostics. Synthetic terminal fixtures remain explicitly
  labeled, not new actual victories.
- Localization validates **1199 exact messages**, strict keys/formatting and the
  actual native `.po` resource.
- Added lifecycle coverage for inherited minima `(0,0)` and `(1000,500)`, failed
  hidden battle preparation, normal entry, resize clamping without gameplay
  mutation, camp, unmount/remount/free, actual settlement view replacement, and
  failed manual-load preparation preserving current size/minimum.
- First regression attempt exposed the test's missing wait for the existing
  deferred camp callback; corrected to await real dispatch. The intentional
  pre-BindSession failure fixture also explicitly cleans its two not-yet-parented
  native nodes. Final run has no leaked native resources.
- Actual GUI, fresh local campaign with normal rules, opening turn only:
  `before-selected-640.jpg` reproduces overlap; `after-staging-640.jpg` shows the
  advance sizing notice; `after-selected-960.jpg` shows separated controls.
  Native WM resize cannot shrink an active battle below 960×640. Returning to
  camp permits exact 640×480 again (`after-camp-resized-640.jpg`). F9 from that
  small camp restores the real F5 opening save and grows to 960×640
  (`after-load-960.jpg`). No move, attack, turn skip or new victory was claimed.
- Physical GUI environment: 1364×1024 Linux desktop, normal decorated window.
  Actual below-minimum physical monitors, DPI changes, monitor hot-plug and
  exclusive fullscreen were not available for verification. The preflight uses
  the pinned engine's screen/usable-area/decoration APIs; no desktop resolution,
  global scaling setting or fullscreen mode is changed by the application.

## Batch 2: C02 observed framing and objective-paper obstruction

Before independent-material adoption, actual C02 entry-fixture inspection exposed
an avoidable 108px blank strip on the left while the right of the map was cropped.
The initial focus hit scroll zero, exposing scene padding. Registered authored
maps now constrain **first framing only** to their ground bounds on each axis that
already covers the viewport. Initial 1.25 zoom is unchanged; later dragging,
edge-reveal space and resize behavior remain unchanged. This is not fitting the
whole map by shrinking actors. Maps smaller than the viewport are not enlarged.

The objective disclosure already existed, but collapsing it hid text while its
paper and pointer-blocking Control retained a 102px default height. The collapsed
paper now uses its actual title/inset height, releasing the vacant map region;
expansion restores the original minimum or full wrapped content. Keyboard focus
also now uses dark text and a brown outline on paper instead of near-white text.
No new feature, gameplay change or automatic objective hiding was added.

Actual C02 entry uses a clearly labeled fixture: a controlled earlier C01 outcome
allows loading canonical C02 at its opening turn. `NativeCapture` accepts the
explicit `--level-entry-fixture` flag to mark both viewport and native title. This
is not a claim of continuous campaign completion or a C02 victory. Reference
manual save stayed byte-identical during visual review.

Evidence: `c02-opening-FIXTURE-before-1180.jpg` versus
`c02-opening-FIXTURE-after-1180.jpg`; `c02-objectives-collapsed-before-1180.jpg`
versus `c02-objectives-collapsed-after-1180.jpg`. The latter releases the actual
northeast archer; clicking it displays its 78/78 inspection card in
`c02-exposed-enemy-picked-1180.jpg`. Space on the retained disclosure focus
reopens objectives without executing an order. These images still use the
original production C02 artwork, not the newly generated material candidates.

Concentrated checks: native camera **32**, HUD **256**, camera screen integration
**277**, campaign UI **200**. Build has zero warnings/errors. The HUD harness retains
its known duplicate NotifyMouseEntered warnings, but no engine errors/leaks.
Small-window final GUI and publication are recorded with this batch's completion.

Final 960×640 native review: full objective text remains readable when expanded;
click and consecutive Space presses collapse/reopen it, retaining the dark-text
focus outline without issuing any order (`c02-collapsed-keyboard-final-960.jpg`).
The reproducible source of the marked opening fixture is
`tools/godot/c02-entry-fixture/`; generated output is isolated and not a game save
committed as actual progress. Metadata and whitespace checks passed.

## C02 material-preparation contract: independent pointer contours

The independent-sprite catalog now optionally supplies `pointerPolygonPixels`.
Absent metadata retains C01's full-alpha pointer behavior. A contour restricts
only pointer reception and conservative cue overlap, **not material coverage**:
opaque roof pixels still occlude rear actors even when they do not redirect a
click to the building site. Contours are source-pixel geometry translated by the
asset anchor once; normal node transforms own placement and scale. Transparent
pixels and pass-through decorations never become targets. No alternate pick or
depth owner is introduced. Painted and independent material polygons share the
same existing strict degeneracy/self-intersection validation.

AssembledGroundChecks **179 assertions** now include opaque roof coverage vs
facade site selection, transparent pixels inside a contour, cue overlap,
pass-through, scaled parent/instance transforms, and eight malformed contour
cases with borrowed textures retained. PaintedSurfaceChecks and the **8129**
exact broad-phase geometry/UV/depth/pointer comparisons pass unchanged. These are
geometry fixtures, not C02 natural play or four-house final-material acceptance.
Production C02 remains bound to its original painted renderer at this checkpoint.

## Batch 5: C02 independent environment adoption

C02 now uses clean ground plus 41 independently depth-sorted props (four houses,
twelve bridge-front chunks, four broadleafs and 21 pines), reusing C01 pine
materials. Rule/terrain/elevation/roster JSON and source registration geometry
are unchanged. Bridge decks/arches/far rails and low dressing remain ground
material; C03–07 are still unconverted. Whole-map static PNG is retained only
as provenance/old renderer fixture, not the formal C02 rendering source.

Actual native GUI evidence under `../empire-evidence-20261101/`:
- `c02-art-{before,candidate,v2}/native-001.png`: same-camera asset iteration,
  explicitly noninteractive preview; first two candidates are not final approval.
- `c02-layered-stone-move-1180.jpg`: actual legal GUI move/wait of river-scout
  (11,19)→(13,20), with visible stone bridge sorting; F5/F9 restores moved/done.
- `c02-layered-edge-1180.jpg`: actual battle zoom-out confirms tree crowns do not
  extend outside the ground canvas; final open wooden rail visible.

These are controlled prior-outcome **entry fixtures**, not a continuous campaign
win. Full normal-AI C02 escort victory, all three bridge crossing routes and
C03–07 actual gameplay remain open. No objective or unit values changed.

Verification: build zero warnings/errors; CampaignNavigation 550 assertions,
zero failed groups (actual material doors/roofs/padding, all 12 rail pass-through
contracts, wooden aperture alpha, 20 actors/4 sites/2 flags, formal entry + F5/F9);
AssembledGround 179; PaintedSurfaceBroadPhase 8129; CameraIntegration 277; focused escort mission/AI
intent 1/1. Historical source-v1 checks remain separate. Source metadata 305 UIDs
and whitespace checks passed. Full seven-map real-play acceptance is not implied.

Additional current core verification: `three bridges native replay/save` completed
421 independently selected legal native actions with player 1 primary objective
completion, exact save-state equality and action replay equality. This is the
canonical **independent level all-side AI** test, not the ongoing human-controlled
GUI entry fixture and not a C01→C02 carried-state victory. Log:
`c02-native-replay-save.log`. Default 1.25 opening-camera actual screenshot:
`c02-layered-default-1180.jpg`; the edge image deliberately zooms out and its dark
canvas surround must not be reported as a default-camera regression.

## Batch 6: C02 normal-AI escort completion and post-play foot support

The controlled prior-C01-outcome **entry fixture** was played through the actual
native GUI against normal enemy AI to C02 victory on turn 11. All three refugees
arrived alive at 58/58 HP; Laiya survived at 78/116. Elin, the medic and reserve
guard fell. Settlement records 38 engagements and advances to `aftermath-02`.
This is a real C02 battle, **not** a continuous C01→C02 campaign proof. No combat
reload/restart or enemy-turn skip was used; an earlier legal noncombat undo is
retained in the evidence. This victory used the preceding layered ground asset.

Evidence (`../empire-evidence-20261101/`): `c02-terminal-FIXTURE.json`,
`c02-settled-autosave-FIXTURE.json`, `c02-terminal-runtime-FIXTURE.log`,
`c02-layered-battle/native-020.png` (victory), `native-021.png` (settlement),
and retained turn-by-turn manual saves. They are not overwritten by post-fix QA.

Actual play exposed legal dry-road cells drawn as water in the inherited ground
painting, especially knight (20,23). The replacement ground preserves the affine
registration, all canonical rules/terrain and the three bridge structures. It
repairs northern approach, middle east landing/safety band and southwest return
bank. Small **noninteractive painted culverts** and outflow preserve intelligible
drainage instead of filling the river into isolated ponds; no new tactical
facility, bridge object, route or rule is added. Independent props stay unchanged.

All 293 road and 44 bridge centers were reviewed with nine-point color candidate
screening; this is not a universal material oracle or proof of every possible
walkable cell. Fifteen manually reviewed dry contacts now have a narrow native
pixel regression (135 foot samples); the preceding material fails 92 samples,
while the corrected material passes. Stone/grass at the southwest shoreline is
not represented as a full-width paved road. Source (516,720), cell (12,29), still
has a stone near its left shoulder; no actual actor-foot-width acceptance is
claimed for that particular edge. No further whole-map regeneration is justified
by an ambiguous color alone.

Post-fix actual native evidence: isolated reload of the original T3 manual save
shows knight (20,23) standing on continuous dry earth, with the same unit positions
and bridge depth ordering: `c02-support-qa/native-002.png` (objectives open),
`native-003.png` (collapsed), `native-004.png` (knight inspection). These are local
reproduction checks, **not another full playthrough of the revised material**.
The first isolated launch incorrectly copied the manual envelope into the autosave
slot and showed safe rejection; the manual schema-4 battle loaded through F9, and the
QA-only autosave was corrected to the existing embedded campaign schema.

Selection guidance now displays the existing arrow/Enter precision path even
when a unit is selected; destination guidance warns that single-target commands
execute immediately. This documents existing input behavior, not a change to
confirmation or command rules. Both remain original-size fully wrapped text,
with existing target size and bottom-tray bounds. Actual selection/keyboard
preview screenshots: `c02-support-qa/native-005.png` and `native-006.png`.

Affected validation: build zero warnings/errors; TacticalHud 268 assertions;
CampaignNavigation 820 assertions, zero failed groups; AssembledGround 179;
PaintedSurfaceBroadPhase 8129; CameraIntegration 277. Metadata: 305 unique UIDs. Headless mouse-notify
warnings and the pre-existing environment-catalog diagnostic are not graphical
acceptance. Full seven-map gameplay acceptance remains open; C03's confirmed
wall/doorway registration defects are next.
