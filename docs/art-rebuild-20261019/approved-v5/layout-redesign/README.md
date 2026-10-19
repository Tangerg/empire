# Gray Flag / Twin Hills layout redesign

This is the implemented phase-one playable layout, not a concept painting. The logical board stays **27 × 17, square4**. Baseline: `17ff6a1`. Source PNG/SVG evidence uses the approved assets available at that baseline; the integrator may have newer artwork, so recapture after integration when comparing final pixels.

## Intent and preserved mission

The western camp opens into a bent village approach, with an independent southern bypass, a compact northern crest and an elongated southeast ridge. Irregular forest masses replace the complete tree fence. Two neutral village objectives are staggered at (12,7)/(15,9); owned camps move to (2,8)/(24,6). `layout.json` records the reviewed design; the runtime scene JSON owns final prop placements and supersedes initial design anchors.

- All **19 stable actors** remain: 11 allied, 8 enemy, same types, ownership and order
- All **four rule buildings** remain: two owned camps and two neutral villages
- Protect Laiya + rout enemies, `matching-orders` death trigger, `found_matching_orders`, campaign choices, commanders, funds, rosters, and combat rules remain unchanged
- Rushed Torren now starts at the real northern summit (9,4), with the same 72 HP cap; destination is legal, elevated and unoccupied
- All starts are legal and unique. Every actor's actual movement class reaches all four buildings. Foot units reach every opposing actor. Mounted units still cannot enter mountain cells; no terrain rule was weakened
- Both villages are reachable by the upper approach and the southern bypass; explicit two-cell maneuver corridors and west/east entrance openings are tested

## Presentation

Two authored hill regions own their decorative landforms. Within those explicit regions only, redundant per-cell cliff sprites and square surface overlays are suppressed. Terrain ids, height and movement costs remain authoritative and unchanged by rendering. Farmhouse facade is clear of the central copse. Both farmers and all 28 irregular exterior tree contacts are scene-owned. Final clearance moves keep south-bow at (20,12) while shifting its decorative ramp/face; camp-guard moves (2,7)→(4,7) and Roderick moves (2,12)→(4,11), preserving their west-front/reserve roles and all identities. The wounded shelter also shifts clear. These two deployment deltas supersede the initial design diagram.

Numeric elevation badges are disabled **only on this mission's normal board**. The existing hovered-cell inspector still reports elevation 1/2, covered by an actual GameController test. Editor canvases and other levels retain numeric badges. Shared piece generation applies this policy before SVG/GPU backends, with parity tests; this is not GPU pixel verification.

The same meadow material extends beyond the logical rectangle to an irregular exterior outline. No legal cell is clipped and no new terrain/navigation rule is introduced. Existing road material and connectivity code are unchanged: the audit checks all 84 road/building-connected route cells and finds zero NESW mask mismatches. Remaining blockiness is the shape of adjoining real logical road cells, not a sampling bug. Continuous visual route boundaries/rounded bends are a separate next phase.

## Actual evidence

- `before/runtime-standing-full.png` and `after/runtime-standing-full.png`: complete initial-pose frame, no actors or scenery selectively omitted
- SVG files: actual `BoardView` / `SvgBoardSurface` output. PNG export uses Inkscape plus the existing `svg_viewports.py` and `normalize_actor_facing` normalization to preserve nested SVG clipping and app CSS facing
- `art-source-comparison.json`: all 27 shared runtime image hashes are identical; two redundant surface sheets appear only before and no image is newly introduced
- `before/full-scene-labeled.png` and `after/full-scene-labeled.png`: labeled complete-frame review boards
- Provenance JSON: exact source hashes, bindings, actor cells, four buildings, draw order, state hash, and capture limitations
- `navigation-audit.json`: before/after start legality, movement-class reachability, objective reachability and pacing
- `road-mask-audit.json`: actual output masks compared to actual map adjacency
- `replay-comparison.json` and compressed full sweep logs: independently executed before and after, not reused fixture assumptions

These are offline production-component captures, **not live browser/GPU screenshots**. Scene `runtimeReady` remains false pending that QA. Natural standing scenery can overlap rear actors. `clearance-review.json` records the measured problems in the intermediate layout and the final corrections; the final complete frame was visually inspected for all 19 actors. No final per-pixel occlusion percentage or live GPU result is claimed.

## Deterministic replay comparison

`npm run --silent sweep` independently ran 60 cases before and after (20 levels × 3 player aggression profiles). All 57 unrelated run event streams are byte-identical. Only the three redesigned c01-01 cases change:

| Player aggression | Before | After |
|---|---|---|
| 0.35 | Player wins, turn 10, 165 actions | Player wins, turn 8, 142 actions |
| 0.58 | Player wins, turn 9, 143 actions | Player wins, turn 7, 119 actions |
| 0.80 | Player wins, turn 9, 143 actions | Player wins, turn 8, 135 actions |

The final layout wins faster for all three profiles, a **balance change**, not an automatic improvement. The earlier intermediate deployment (camp-guard at 2,7 and Roderick at 2,12) lost the aggressive run at turn 7; that result is superseded, not attributed to the final layout. Clearance-driven deployment shifts affect support and opening movement, so the sweep was rerun independently after them. Nearest initial contact is 8 cells instead of 12; occupied 3×3 sectors increase from 6 to 7. Keep the existing mission goal; human playtesting should confirm the faster opening and victory timing are appropriate before calling this a final difficulty target. No combat rule was changed to force a victory.

## Verification

- TypeScript typecheck: pass
- Six focused affected/integration suites: **80/80 passed**, including campaign rush legality, two routes, hovered elevation and renderer contracts
- Full suite before the last two clearance-driven deployment shifts: **900 passed, 1 failed, 901 total / 98 files**. The single failure is the existing 5-second cold public-barrel import timeout in `architecture-boundaries.test.ts`; that file independently passed 104/104. The integrator separately fixed collection-time imports on main while retaining all assertions/default timeout; the integrated full suite must be reported separately
- No test skipped, assertion weakened, combat rule tuned, or gameplay object removed
