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
| 02 三桥河谷 · 41×38 | All three refugees east, designated ballista destroyed; any refugee loss fails; center bridge ceasefire through enemy T3 | Whole painted map + 14 occlusion objects, not independent props. Normal escort, refusal feedback, bridge-rail picking, loss/settlement. |
| 03 赤石围城 · 101×52 | Control link neutralized OR HQ captured; T4 allied reinforcements; node/shield/T15-engineer routes break link; limit 18 | Whole map + 64 occlusion objects. Large-map camera, dense buildings, reinforcement landing, allegiance transition and viable normal-AI route. |
| 04 黑旗营地 · 50×30 | Three clues, then at least one clue-carrier (莱娅 or 米蕾) withdraws | Whole map + 57 occlusion objects. Empty doorway vs real adjacent guard; clues, withdrawal and carried roster. Historical erroneous doorway probe has been corrected, not weakened. |
| 05 焚村令 · 50×32 | 莱娅 and at least two civilians survive seven rounds (normally human T8); enemy reinforcements T4; limit 11 | Whole map + 45 occlusion objects. Complete normal-AI T8 defense with carried C04 roster remains an open historical acceptance item. |
| 06 白河夜渡 · 50×32 | All four escorts reach north bank; pursuit T4, bridge warning T6, limit 13; warning is not physical bridge destruction | Whole map + 28 occlusion objects. Historical normal-AI T8 victory; this run not replayed. Recheck covered endpoint actor badges, zoom/load and carryover. |
| 07 无主之城 · 96×64 | Granary and militia-camp capture achievements latch; not simultaneous ownership; 莱娅 survives; limit 15 | Whole map + 55 occlusion objects / 16 capture points. Corrected map's complete normal-AI victory remains open; historical T9 route skipping enemy actions is not victory evidence. |

For every map: verify actual scale and ground/building/actor integration, picking
and passage, objective/reinforcement timing, camera/HUD obstruction, input feedback,
save/load, and actual win/loss settlement. C02–07 painted-in objects are an explicit
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
