# c01-13 actual fortification integration review

## Verdict

The three newly registered stone cells are technically bound and visible in the complete production BoardView. Their scene integration is **unresolved**: a pre-existing random wall selector, single-cell placement pitch and unmatched source connector geometry turn the archive walls into small disconnected ornaments. This is not evidence that the new sources failed registration.

No production code, map, rules, assets or Git state was changed by this review. Keep the three new cells bound. The active implementation remains the first-level layout; a wall-topology change should be separately scoped.

## Complete scene evidence

- `runtime-standing-full@1x.png`: complete actual production BoardView, native logical size, all **24 actors and 18 rule buildings** retained
- `runtime-standing-full@2x.png`: the same complete scene at 2× display density
- `runtime-standing-full.svg`: original actual BoardView/SvgBoardSurface output, production app styles and actor-facing behavior included
- `central-wall-actor-context@3x-nearest.png`: crop of the actual native scene enlarged by nearest-neighbor; exposes the upper/lower central wall breaks and door-versus-unit scale
- `west-wall-actor-context@3x-nearest.png`: actual west-wing wall/unit context
- `actual-atlas-straight@2x.png`, `actual-atlas-corner@2x.png`, `actual-atlas-gate@2x.png`: the three exact currently bound atlas cells
- `runtime-scene-provenance.json`: every actor binding, actual piece/standing contact, draw order, source hashes, completeness counts and unchanged battle-state digest
- `runtime-fortification-measurements.json`: all 68 wall prop records, actual scales, alpha128 world bounds, neighbor masks and 60 adjacency measurements

This is an offline raster of the actual complete scene, not a mockup, generic contact sheet, live browser screenshot or GPU-playback verification. The existing `review_gray_flag_scene.ts` workflow was adapted only in a shared-directory review script to select c01-13. It used the production board harness, board art, state creation and renderer. The repo's `render_ground_review.py --scene-only` flattened equivalent nested SVG viewports for Inkscape and retained the source SVG.

## True caller and semantics

Source locations in the reviewed working tree:

1. `packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts:176–179`: wall material lists `stone-wall-straight`, `stone-wall-corner`, `stone-gatehouse` as interchangeable scenery and base scale **0.38**
2. `packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts:540–546`: hashes choose the variant, add `tileHash(...)*0.12` to scale, and flip independently. No wall-neighbor topology enters that selection
3. `.../candidate-01-map-scene.ts:508–514`: standing contact is `((x+0.5)*32, (y+1.18)*32)`. Wall scenery has no jitter, so adjacent contacts are exactly 32 world pixels apart
4. `.../candidate-01-map-scene.ts:134–147`: effective scale is caller placementScale × cell.presentationScale, then the declared source anchor is placed at the contact. These three cells have no presentationScale override, so effective scale is the caller's 0.38–0.50 range
5. `packages/content-ancient-empires/src/terrain.ts:18`: every `wall` rule cell is IMPASSABLE, full cover, obstructionHeight3, opaque, blocking
6. `packages/story-candidate-01/src/levels.ts:263–280`: c01-13's HOLY_ARCHIVES map supplies `#` wall cells and street/road openings; its battle definition at 724 onward has archive/escort objectives, not gate or entrance state
7. `.../manifest-environment-builder-v1.json:2366–2399`: the three catalog footprints and registered contacts

The generic native contact scale **0.55 is not this caller**. Calibrating only that sheet would hide the actual runtime shrinkage and wall joins.

## Measured scale and extent

All measurements below are source-alpha128 silhouettes after the actual caller transform, excluding effect shadows. World pixels are logical/native display pixels; cell pitch is32.

| Cell | Actual uses | Actual scale | Logical source silhouette | Actual visible extent range | Catalog footprint |
|---|---:|---:|---:|---:|---:|
| stone-wall-straight | 24 | 0.381–0.497 | 83×55 | 31.62×20.96 to41.25×27.34 | 2×1 |
| stone-wall-corner | 18 | 0.391–0.497 | 85.5×50.5 | 33.43×19.75 to42.49×25.10 | 2×2 |
| stone-gatehouse | 26 | 0.382–0.491 | 87×70 | 33.23×26.74 to42.72×34.37 | 2×2 |

Catalog anchor points: straight(48,64.3604), corner(41.4705,76.6858), gatehouse(48,80.2807), in a96×96 logical cell /192×192 atlas raster. Registered scale is applied once; the review found no double-scaling error.

The footprint metadata describes two-cell-wide modules, nominally64 logical pixels across a32px board. It does **not** size or span cells in the current caller: that caller draws one randomly scaled prop per single `#`. Thus current visible width is about32–43px, rather than a deliberately registered64px connecting module. A footprint field alone does not prove a rendered connection.

Visual assessment in the real scene: gate doors are much smaller than neighboring approved unit bodies. The closed door looks like a miniature fence gate beside a full-size guard, rather than a full-size gatehouse. Enlarging the entire sprite ad hoc would cover streets/actors and still would not align the connected ends. Wall height could reasonably remain lower than an actor, but its connector pitch and doorway proportions need authored calibration.

## Connections: hard failure

There are **68 wall cells** and **60 orthogonal adjacent pairs**:

- **North–south:**35 pairs; **28 have a guaranteed visible gap even between their alpha bounding boxes**. Gap range across all N–S pairs is−5.745 to15.547px. Positive gaps prove disconnected visible silhouettes
- **East–west:**25 pairs; all bounding boxes overlap, by0.042–12.708px. This is not proof of a stone connection: diagonal faces, random corners, independent scale and random mirror do not align connector endpoints. The complete scene visibly reads as separate short fence ornaments

Examples in the shared upper/lower central wall strips (world/native pixels):

| Adjacent cells | Current forms | Gap |
|---|---|---:|
| (13,2) → (13,3) | corner → straight | 12.815 |
| (12,3) → (12,4) | gatehouse → corner | 8.252 |
| (13,5) → (13,6) | gatehouse → straight | 11.013 |
| (12,10) → (12,11) | corner → corner | 11.401 |
| (12,11) → (12,12) | corner → straight | 11.426 |
| (13,11) → (13,12) | gatehouse → straight | 13.614 |
| (12,12) → (12,13) | straight → straight | 6.668 |

The intended upper/lower strips do not genuinely connect at current pitch/scale. The arch/door source is technically bound, but26 random appearances on impassable `#` cells have no gate-specific gameplay meaning. An ordinary street opening between wall groups must remain an ordinary opening unless a gate/entrance role is explicitly authored.

## Conservative replacement proposal, not implemented

1. Preserve every map cell, traversability rule, actor, building, archive zone and escape zone. Read the existing `wall` occupancy as the only geometric authority
2. Remove gatehouse from generic wall variant selection. For c01-13 the correct explicitly authored gatehouse count is currently **zero**. Show a gatehouse only where an existing authored gate/entrance semantic says there is one, preserving that role's state and traversability. Do not infer a new open gate from nearby streets
3. Replace randomized structural form/scale/flip with topology-derived connected wall regions. A four-neighbor mask is useful for one-cell-wide runs, but c01-13 has two-cell-thick walls: derive their **external contour and continuous top**, and omit internal shared edges. Do not treat every cell in a two-wide strip as a decorative T junction
4. Select straight, outside/inside corner, end and junction forms only where their topology and orientation actually fit. Mirror only a variant whose registered connectors support that mirror. Keep a fixed structural connector scale/pitch; random variation may affect small surface dressing, not dimensions or connectivity
5. The current three raster sources are insufficient for complete square-grid wall topology. The existing straight is a diagonal illustrated face; the corner is a V-shaped exposed corner; no complete N–S/end/inside-corner/junction family is declared. A wholesale90° image rotation would rotate lighting and vertical perspective. Supply/calibrate the missing oriented modules or an explicitly registered continuous wall-strip kit before claiming the wall network is solved
6. Register connector endpoints and exposed-face baselines, then verify at exact actual callers with adjacent cells. Catalog footprint and source footpoint are not substitutes for connector endpoints
7. Keep ordinary entrances open at current map gaps. Keep gate art independent from generic impassable wall art. No new movement mechanic, door opening/closing, breach rule or actor relocation is required for the visual topology task

Useful scope fence for the next task: c01-13 complete production scene first, preserving the mission digest. Verify one-cell vertical/horizontal runs, two-wide strips, all four outward corners, ends and existing passage openings. Test that no gate appears without explicit semantics, no image reaches into a legal passage incorrectly, and actor selection remains readable. Then run shared wall-renderer regressions across other wall-bearing levels.

## Source provenance

Reviewed checkout: `/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019`, branch supplied as `dot/art-rebuild-20261019`; current working atlases, not an assumed clean commit. Exact source hashes are recorded in `runtime-scene-provenance.json`.

- Crossings/fortifications2× atlas SHA256: `cc772464cc6d6e524978fa40ff58d11b10f572ccf1b18a2a5fb482b122364531`
- Environment manifest SHA256: `a603074115d9a843becfd27cb24f8ef348129d4565589b6d07569507a6a93c1c`
- map-scene.ts SHA256: `9f73ff00327224528207a8e54e90954e04962fc6e5e8d9d16b539a8c3339dad6`
- terrain-materials.ts SHA256: `7cb8644b2042e74a60784082cd05855d0bef7a296c9146715bec65f3789b3792`

A read-back hash comparison after rasterization found no changed inputs. Total review artifacts are approximately6.7MB; no repository copy was made. No code change, commit or push was performed.
