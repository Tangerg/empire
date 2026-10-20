# Spacious irregular Three Bridges

This checkpoint expands the actual playable C01-02 battlefield, rather than shrinking the old scene to fit one screen. Images are offline production BoardView/SVG renders, not live browser or GPU interaction evidence. The independently integrated camera uses ordinary-window panning at 40 pixels per cell; the whole-world image is an overview, not the play viewport.

## Images

- [Final whole-world 2× overview](after/runtime-standing-full@2x.png).
- [Ordinary-window opening](after/opening@1x.png) and [panned east-bank viewport](after/eastern-crossing@1x.png), both 1440×900 at 40px/cell.
- [Previous compact checkpoint, uncropped 2×](../river-valley-layout/after/full-board@2x.png). This is a different world extent, not a zoom-to-fit density comparison.
- [Exact capture hashes and shared state verification](after/capture-verification.json).

## Composition and ownership

- The storage rectangle grows from 22×14 to 36×24. Its 715 active cells form broad village, deployment, battery and northeast-garrison pockets, with narrow river approaches, inlets and spurs. The remaining 149 cells are explicitly `c01.outside`, impassable to every movement class, including flying. Terrain is the single owner of the silhouette and collision; there is no second decorative-only map mask.
- Preserve all 20 actor identities, types and stats, nine rule buildings, their ownership roles, three independent crossings, objective identities and engagement-rule semantics. Deliberate placement changes separate villages and armies rather than pile everyone into a larger central clearing.
- The three bridges at rows 5, 11 and 18 each independently connect both banks. The north route approaches the garrison; the middle route offers the truce crossing; the south route supports the escorted convoy and independent artillery position.
- Enemy artillery and its screen occupy the southern bank; the main garrison remains northeast. No AI, combat statistics, aggression, economy rule or victory requirement changed.
- The safety zone moves to the six cells x=23–24, y=19–21 just beyond the southern east bank. It still means escort all three civilians across the river, not march them through a second hostile front to a distant corner. The artillery must still be destroyed, and Laiya protected. The safety marker reads the same scenario zone owner.
- Truce expiration intentionally moves from turn 3 to turn 6 to account for larger travel distances. The same trigger removes the same middle-bridge no-attacks rule and emits the same accepted-truce signal. The suggested completion remains 12 turns.

## Presentation

Roads retain the reviewed native road-earth RGB, 20px trail width and 16px junction rounding. Exact compound cell unions eliminate fractional-camera clip seams without expanding into blockers. River width varies locally within the actual two-cell channel; water is clipped to water/bridge cells, with a bounded sandy rim. Source material and UV scale remain unchanged. Sparse native grass/bank strips meet selected boundaries; no recoloring or filter replaces the approved art. Structures use the existing shared contact-shadow owner at full native geometry. Trees form groups rather than a rectangular perimeter fence.

The final alpha audit includes all 20 real actors: 18 have zero scenery overlap, one 2.0% peripheral overlap and one 0.5%. No actor or building is hidden, removed, shrunk or cropped for proof. The final shield and cleric are fully clear.

## Gameplay and verification

The baseline engine tree is `0466cb37c4ed66994fd38893b350d1c87cc5ded4`; this packet contains no engine changes. All three profiles complete the genuine escort/artillery objective, with all three civilians surviving:

| Aggression | Previous compact turn | Expanded turn | Expanded winner |
| --- | --- | --- | --- |
| .35 | 17 | 10 | Player |
| .58 | 13 | 9 | Player |
| .8 | 11 | 13 | Player |

`after/validation.json` records legal deployment, available opening movement, per-actor movement-class access to every actor/building/safety cell, each independent crossing, first combat, survivor HP and event/state hashes. `after/level.json` preserves the exact reviewed level. `after/actor-clearance.json` records the native-pixel audit. The old map's AI wins are a guard, not the design rationale: wider spacing, separate engagement pockets and voluntary routes are retained.

Focused scene, objective/balance and geometry checks pass (66 tests), and typecheck passes. Another 53 material/backend tests and five raster contracts pass. The complete 60-profile sweep preserves all 57 non-C02 event streams and outcome digests byte-for-byte; only this intentionally redesigned level changes. The former compact .58 victory was annihilation; the new .58 victory completes the escort/artillery objective. Exact traces and comparisons are recorded in `verification/`.

## Remaining limits

This is a functional composition/camera checkpoint, not final visual-style acceptance. Roads still read as fairly hard ribbons, some river bends remain angular, and native building-to-ground integration can improve. Offline proof does not establish live-browser input performance or GPU playback. The generic AI can still stall on some otherwise reachable detours; that pre-existing limitation is tracked separately, and neither rejected navigation experiment is included here.
