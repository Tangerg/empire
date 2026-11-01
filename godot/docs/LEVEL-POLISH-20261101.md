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
| 03 赤石围城 · 101×52 | Control link neutralized OR HQ captured; T4 allied reinforcements; node/shield/T15-engineer routes break link; limit 18 | Independent ground + 95 standing instances. Actual normal-AI T4 victory and settlement; 89 city-support samples repaired, 15 independently classified samples plus ground/prop material coherence remain open. |
| 04 黑旗营地 · 50×30 | Three clues, then at least one clue-carrier (莱娅 or 米蕾) withdraws | Formal independent ground + 139 instances, 33 house foundation regions and 11 actual furniture feet checked; actual normal-AI T6 three-clue withdrawal victory recorded. Empty doorway vs real adjacent guard; clues, withdrawal and carried roster. |
| 05 焚村令 · 50×32 | 莱娅 and at least two civilians survive seven rounds (normally human T8); enemy reinforcements T4; limit 11 | Formal independent ground + 58 objects; fresh C05 1499 assertions, depth-strip front/back negative controls, 26 original physical groups reviewed. New real normal-AI T8 defense/settlement complete from explicitly text-reconstructed C04 entry, not original autosave bytes. All three civilians survived; Bran loss/wounded result retained. Controlled cloud performance review is not passed. |
| 06 白河夜渡 · 50×32 | All four escorts reach north bank; pursuit T4, bridge warning T6, limit 13; warning is not physical bridge destruction | Production remains painted + north-shelter picking fix. New real run paused T1 after10 friendly actions, no enemy turn; operation authorization pending. Independent99-object preview remains unbound: focused static root/depth/picking checks pass, seven bridge-support contacts and remaining wall/forest responsibility gaps unresolved. Historical victory is not new evidence. |
| 07 无主之城 · 96×64 | Granary and militia-camp capture achievements latch; not simultaneous ownership; 莱娅 survives; limit 15 | Whole map + 55 occlusion objects / 16 capture points. Corrected map's complete normal-AI victory remains open; historical T9 route skipping enemy actions is not victory evidence. |

For every map: verify actual scale and ground/building/actor integration, picking
and passage, objective/reinforcement timing, camera/HUD obstruction, input feedback,
save/load, and actual win/loss settlement. C06–07 production painted-in objects remain an explicit
art/interaction debt; C06 independent preview is not final production acceptance. Work will
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


### Southwest foot-width closure (isolated placement fixture)

A further explicitly synthetic placement fixture copies T3-start and changes only
knight 4's coordinates from (16,22) to (12,29); it is **not** part of the real
battle history. Native rendering at normal 1.25 zoom shows the boot contact on
dry grass, with the tall shoreline boulder behind/right rather than beneath the
actor. Normal GUI select → Down → Enter → W moves to adjacent road (12,30),
with the normal moved/done result. Both actual frames show supported dry feet.
This closes the narrow edge question above without further painting or changing
rules. Evidence: `c02-foot-fixture/native-002.png` (standing), `native-003.png`
(adjacent move), `c02-foot-fixture-NOT-GAMEPLAY.txt` (exact fixture construction),
and the isolated saved event trace (unit 4 at 12,30, done, HP 116).

## Batch 7 — C03 layered candidate and doorway registration (not full acceptance)

C03 now uses an assembled ground with independent walls, eight canonical-site
buildings, a tower and pass-through decorative pines. Original level source,
101×52 rules grid, origin (13,17), axes (16,0)/(0,18), world scale 1.5 and all
mission/rule facts are unchanged. The old wall alpha incorrectly covered legal
north-gate cells (65–66,12) and main-gate cells (65–66,24). Wall placement now
leaves both road openings genuinely transparent, rather than allowing clicks
through a visibly solid wall. Eight door contacts are registered to their real
canonical sites; roofs remain occluders but not selectable facade regions.
An occupied headquarters correctly preserves unit-first picking at that same
cell, not an unconditional object-pick assertion.

This is a **recoverable visual candidate**, not completed C03 artwork or gameplay
acceptance. v3 corrects vertical wall top/side thickness and calibrates building
scales against old painted bounds without moving doors. v4 corrects horizontal
wall top depth; its west/south seam receives a final 10 source-pixel extension
of the lower end piece, away from either gate. Ground-detail v2 was requested at
2× size but the generator returned 1642×958: it is not an upscale. Ground still
looks softer and busier than independent props; this remains a material-fusion
debt and is not hidden by passing geometry tests. Rejected variants and exact
prompts/provenance are retained in `docs/art-provenance/c03-layered-v1`.

Actual C03 entry resumes the real C02 settlement with Elin still dead. Current
real play is only T1: engineer moved (61,23)→(65,23), done, HP86. No full C03
victory is claimed. Manual save and byte-identical recovery copies precede each
art reload. Evidence: `../empire-evidence-20261101/c03-before/native-002.png`,
`c03-after-v3/native-002.png`, `c03-after-v4/native-002.png`, and
`c03-pre-v5-saved-FIXTURE.json`. These carry the controlled prior-C01 entry marker;
they are not fresh C01 or continuous-campaign completion evidence.

Verification before the final seam adjustment: build zero warnings/errors;
focused core structure-lifecycle/Redstone-route checks pass; CameraIntegration
277 assertions; native CampaignNavigation 1834 assertions, zero failed groups,
including eight actual facade/roof contracts and actual wall alpha/pick checks
at all six gate contacts. Settlement branches in that suite are explicit UI
fixtures, not actual mission results. Final seam regression and real 1.25 city,
node and second-entrance inspection remain to follow this candidate checkpoint.

### Whole-city inspection and continuous vertical wall correction

Normal 1.25 city inspection exposed repeated diagonal seams along the east wall
that the opening crop did not show. A new generated orthographic-axis module
keeps its top and dark side face but has parallel long edges; eight placements
replace ten overlapping slanted placements. Main/north gate alpha clearance is
unchanged. Normal native frames: `c03-after-v7/native-002.png` (opening),
`native-003.png` (city/node), `native-004.png` (north entrance/headquarters).
The historical north horizontal wall is already segmented in the source image;
this is not a newly broken perimeter. The short internal wall looks detached
from the newly registered garrison door and remains a specific composition debt.

An intermediate local v6 layout omitted required explicit null/false placement
fields and was rejected on load and by the native suite. Those fields were
restored, not made optional; original manual save hash remained unchanged.
Corrected v7 CampaignNavigation passes 1786 assertions, zero failed groups.
The count is lower because there are two fewer wall instances sampled against
six gate contacts, not because facade/gate contracts were removed. Ground/prop
material fusion and full C03 combat remain open.

### C03 physical footprint correction (after candidate review)

The candidate's new assertion that every building anchor must coincide with its
interaction cell was wrong. Interaction cells may be outside the physical body;
moving the body to them exposed impassable terrain as grass. It has been removed,
not retained as a product contract. Each building now fits its reviewed physical
area, while all eight original selection cells and map/rule data stay unchanged.
The southwest low shed was also the wrong silhouette for its wood-house site;
it now uses the independently generated small grounded timber cabin.

`data/scenes/c01-03-foundation-review.json` records all eight sites, 172 reviewed
wall cells, their attribution basis and the frozen previous placements used as
negative control. The four western houses have distinct canonical wall islands.
Eastern houses share components with city walls/tower: only canonical wall cells
inside their historical material silhouettes are attributed here, not the entire
connected defense network. Those silhouettes include roofs and are **not** a
certified ground foundation polygon. Boundary cells need actual opaque material
intersection at the fixed 3×3 footprint samples; requiring every center opaque
would over-enlarge roofs and ignore legitimate partial-cell foundation geometry.
The independent old-placement raster check exposes 57 reviewed cells; the native
suite also requires that the frozen old assembly fail the overlap contract.

Validation: build zero warnings/errors; format verify passes; native full
CampaignNavigation **2797 assertions, zero failed groups**; focused actual
standing-visual probe **172 reviewed cells, zero failed overlaps**, canonical
level SHA unchanged. The probe alone has an explicitly unconfigured dynamic
node diagnostic; the complete native suite supplies that presentation and passes.
All eight actual door pixels still pick their original site (or existing occupied
HQ unit priority), roofs remain non-targeting and six north/main gate contacts
remain clear. This is geometry/input validation, not victorious gameplay.

Real saved T1 human-done state was retained across the artwork reload; repeated
F5 changes only the two savedAt timestamps, not campaign/battle facts. Evidence:
`c03-foundation-safe-reload-FIXTURE.json`, `c03-foundation-v1/native-002.png`
(opening), `native-003.png` (southwest timber house), `native-004.png` (city store
and southeast house); final probe/source and full native log retained alongside.

Remaining city-defense registration debt is explicit: a separate scan of all
494 canonical wall cells finds **104** with no standing material at the same nine
samples. Shared city-wall/tower support bands and east-wall rear margins are not
claimed fixed by the eight-house correction. This count cannot be directly
compared with the old 228 **center-only** scan. Full-city physical attribution and
ground/prop material coherence remain open; C03 overall acceptance is not complete.
C02 cannot inherit this wall-foundation test: its map has zero wall terrain cells.
C04 has a separate positive-area foundation audit; do not substitute center-only
opacity for that established rule.

### C03 normal-AI manual conclusion (foundation revision 5b6982aa)

Actual native GUI play finished at **turn 4, victory**, by breaking the gate chain.
Torren traversed the original main opening `(65,24) → (66,24) → (67,24) → (68,24)`
through normal move/attack input. Enemy AI acted normally, including artillery
and bow damage; no damage-avoidance reload or engine-dispatched battle replaced
this play. Four oathbound units converted to allies. Normal settlement reports
**16 friendly survivors, 4 enemies remaining, 10 engagements, no new friendly
fallen**, and advances to `aftermath-03`. The campaign carries the actual prior
C02 settlement; C01 prior history remains a controlled entry fixture, so this is
not a claim of continuous whole-campaign play. The on-screen fixture marker stays.

Evidence root: `empire-evidence-20261101/`; index
`c03-manual-play-evidence-index.json`, terminal `c03-terminal-FIXTURE.json`
(SHA-256 `cff835cd0432e02c28952d2e1b55dd3a815a17d200f50fdb4aa295089c86cd18`),
settled `c03-settled-autosave-FIXTURE.json`, and
`c03-terminal-runtime-FIXTURE.log`. Native captures in `c03-foundation-v1/`:
005 T2 start, 006 T2 human end, 007 T3 human end, 008 final forecast,
009 actual victory, 010 normal settlement summary. Frames 001–004 were scene
review, not battle progress. Forecast 33 is potential damage; terminal hit 21
is capped by the target's remaining HP. North-gate traversal, the garrison/tower
mouse overlap, HQ-capture alternative and T15 support route are not manually
covered. Wall-support and material-coherence debt above remains open. Subsequent
art-only reproductions must not be represented as a second complete playthrough.

### C03 city-wall support checkpoint — functional, not visual acceptance

The responsibility review separates 104 previously unsupported nine-point wall
samples into specific wall, enclosure, tower and rock associations. This batch
corrects **85 city-wall samples**: the garrison wing's front was accidentally
25 source pixels too high; horizontal wall modules were too short for their
registered blocked bands; long vertical walls had lost their side shoulders.
The wing contact is restored, horizontal runs use fewer full-height modules,
and independently generated low stone shoulders meet the existing vertical
wall sides. No map, passability, selection cell, battle rule or gate is changed.
No fake depth/front offsets or new renderer layer were introduced.

`c01-03-city-defense-review.json` records the eight repaired responsibility
groups and five remaining groups. Actual standing-material scan now yields
**19 / 494** wall cells without any material at the same fixed nine samples,
versus 104 before this batch. The remaining 19 comprise HQ west return (4),
garrison east enclosure (6), tower rim (2), Keep east footing (4), and three
retained bottom-ground rock samples; a missing standing sprite does not mean
those three visible rocks are bare grass. Keep east has three old material
witnesses, so that small registration regression remains explicit, not fixed.
The other remaining groups had no old nine-point material witness either.

Native CampaignNavigation: **3526 assertions, zero failed groups**; build zero
warnings/errors. The first invocation refused its non-isolated default user-data
path before exercising tests; corrected invocation used fresh `/tmp/empire-*`
data. Expected storage-error negative fixtures remain in the successful log.
The added defense guard checks all 85 reviewed supports, preserves the existing
six actual gate checks and rejects the frozen old garrison-wing placement.
All 172 house-support checks still pass. Focused full-wall probe has the same
known unconfigured dynamic-object diagnostic, not a production resource failure.

Actual 1180×760 / 1.25 native views: `c03-wall-footing-v1/native-002.png`
(default) and `native-003.png` (city). They reload a copy of the actual T4 start
in a separate XDG directory solely for visual reproduction; the successful
terminal and settlement remain unchanged. The reproduction's initial autosave
was mistakenly populated with the manual-envelope format and correctly rejected;
manual F9 loads the valid copied T4 application save. No battle fact was edited.

**Visual acceptance remains incomplete.** Root review finds horizontal bricks
oversized relative to the vertical wall after the reduced repeat count; the short
wing reads as three large block rows. Next revision must increase source brick
density at the same world geometry, not trade away wall height, thickness or
legal gates to hide the problem. Ground/prop material coherence remains open.
This checkpoint is not a claim of top-tier finished art or a second full victory.

### C03 follow-on masonry density / Keep-side correction

The horizontal sprite is regenerated with smaller staggered ashlar courses while
preserving the established silhouette and world placements. Actual output is
1483×1061 (not the requested 1484×1060); its measured bottom contact is y826,
registered explicitly. No stretched brick texture or wall-height reduction is
used. A low stone side return also restores the Keep east service-yard support,
meeting the south defense wall without occupying its adjacent legal road. It
covers the three confirmed old-material regressions and the adjoining apron
sample; the building/entry itself does not move or grow.

The responsibility contract now includes **89 repaired samples**. The unchanged
494-wall scan has **15** remaining no-standing-material samples: HQ west return
4, garrison east enclosure 6, tower south rim 2, retained bottom-ground rocks 3.
These historical/attribution items and broader ground/prop material coherence
remain open in the acceptance matrix. Native images in the same reproduction
folder: 003 coarse-brick before, 006 finer-brick same-camera after; 005 shows the
Keep side return meeting the south wall. These are diagnostics on a copied T4
start, not new victories. Parent visual review accepts the direction for moving
on, not a declaration that all C03 art is finished.

Final **newly compiled** CampaignNavigation passes **4150 assertions / 0 failed
groups**, including 12 gate/service-road contacts, 89 city supports, 172 house
supports, facade/occupied-unit priority, carried-entry, load and settlement
contracts. Build has zero warnings/errors. An intermediate newly added `Coord`
reference lacked its namespace import; that compile failure is fixed. The run
against the old DLL reporting 3574 is explicitly **discarded**, not final evidence.
Logs: `c03-masonry-density-build.log`, `c03-masonry-density-final-checks.log`,
`c03-masonry-density-probe-final.log`. The live GUI used the new image resources
and unchanged rendering code; no application rule code changed. GitHub listed
no workflow runs for the preceding 381c3bcf checkpoint; local checks are not CI.


## C04 independent composition and physical registration

139 independent instances replace painted-in tall scene objects. Actual 1180×812
previews at scale .5 and 1.25 use identical canonical initial actors and source
axes; they are explicitly labeled no-gameplay previews, not battle progress.
Evidence: `c04-before-full/`, `c04-before-opening/`,
`c04-after-physical-full/`, `c04-after-physical-opening/`.

The first visual/physical audit caught defects that the prior 278 assertions
missed: undersized southwest supporting piers, table/bed feet inside open entrance
rows, and cart alpha blocking legal (42,16). Fixes retain all gameplay and sites.
Current focused new-DLL native validation passes **332** formal assertions, while
**341** historical painted assertions pass separately via `--legacy-painted`.
The former covers all 33 interior house-support regions, 11 actual feet on the
seven canonical furniture cells, per-item old-placement negative controls,
30 empty openings and real neighboring guard priority. Build zero warnings/errors.
A temporary test compile hit an ambiguous Godot polygon overload; explicit typed
vertices resolve it, and the final results above are from the rebuilt DLL.

Independent read-only old/new native scans cover all 1083 legal cell centers,
plus 182 candidate audit points; 40 fixed entrances/approaches remain Ground,
0 rendering issues and state unchanged. That separate scan froze the old depth
catalog; its empty-board result is not evidence for the final new front-pole
actor ordering. The final focused suite checks that actual adjacent guard case.
Full responsibility/source/probe records are in `c04-physical-review/` beside the
screenshots. NE outside-cell support and subpixel rock fringe differences remain
accurately classified, not changed into new collision rules. Three true side-cloth
pixels now select their real site rather than ground; do not remove real facades
merely to force every walkable center to return Ground.

Normal C04 play from the actual C03 settlement is now complete: T6 primary victory
after all three investigations and subsequent Laiya withdrawal, 9 allies alive,
4 enemies remaining, five normal enemy turns and no combat reloads. Actual tent
entry/body selection and settlement are indexed in `C04-NORMAL-PLAY-20261101.md`;
formal native checks rerun at 332 assertions. C01 prior history still has a
controlled entry fixture; no continuous full-campaign claim is made. The far east
cart was not manually visited. Broader ground/prop stylistic consistency remains
a visual review item across levels, not a reason to equate geometry with finished art.


## Current C05/C06 checkpoint, 11:45 UTC

C05 actual recovered-environment normal-AI completion and precise entry provenance
are recorded in [C05 recovery](C05-ENVIRONMENT-RECOVERY-20261101.md). Controlled
software-renderer results and open optimization boundaries are in
[C05 rendering review](C05-SOFTWARE-RENDERING-REVIEW-20261101.md); they are not a
user-hardware FPS claim or performance pass.

C06 real play is separate from independent material fixtures. The actual entry
comes from the new C05 settlement; realT1 has10 friendly actions recorded, with
no enemy turn or new victory. The blocked normal operation remains paused; none
of the subsequent renderer-only initial-state poses advance or read that save.

The unbound99-object preview now has independent buildings, rails, props, a
restored southwest high cliff, two proven foreground bare trees and one original
front pine with visible root. Recent focused native probes cover1134 legal
centers,180 entrances,180 body samples,20 site actor poses and8 specific tree
front/back poses; these are static acceptance only. Original vs changed-source
provenance, failed baseline and negative controls remain in
`art-provenance/c06-layered-v1/`.

Seven anatomical bridge contacts still need coherent support. Uniform widening
covered water-center negatives; a local envelope bent straight spans; the final
platform candidate failed its seven contacts. None was adopted. West(10,10) bank
height remains uncertain, while east(42,13) is an actual wooden-beam false positive
and must not be “repaired” to satisfy an approximate quad. Existing wall-expression
gaps, hidden/retained forest ownership and cross-level ground/prop material
coherence also remain incomplete. Do not promote the preview or claim fullC06
polish from the passing focused probes. C07 work at this point is read-only
source/pixel preinspection, not a new gameplay run or artwork acceptance.
