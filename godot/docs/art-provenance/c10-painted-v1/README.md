# C10: 旧旗下的追兵

This is the existing 25 × 14 chapter-final pursuit map, not an additional level.
The registered 2D painting replaces tall default dimetric scenery. Independent C01 house/tree assets reuse `c01-01-objects.json`; there is no copied source-geometry owner or new renderer. Registration is owned by `data/scenes/c01-10-candidate-ground.json`, not the provenance manifest.

## Authorship and geometry

Ground is generated hand-painted art, guided by the canonical terrain mask and C01 faithful ground style. Final pass shoulders are outside playable cells; all six exit cells remain open. The 1536 × 1024 image is a uniform 2/3 projection of a 2304 × 1536 design canvas. Expanded margins cover exit-focus camera framing without a C10-specific camera branch.

Terrain has 28 mountain, 13 hill, 13 forest, 17 road, four village and five outpost cells. Three continuous rocky formations express the mountain groups rather than independent floating tiles. Mounted/heavy units still cannot use mountains. Soft grassy hills retain their original movement cost. Pale grass channels guide the eye without inventing a road-cost fact. Movement previews remain authoritative. Decorative outside-map rock shoulders have no collision/picking owner.

## Behavioral boundary

The goal is Laiya reaching the northeast six-cell pass, not killing Roderick. The 14-turn limit and Laiya defeat condition are unchanged. T4 Ivra aid has authored morale effect −18, transformed by ordinary resilience (the verified carry produces −16). T6 spawns the original three western pursuers. No health, roster or save is bundled or repaired for screenshots.

## Verification scope

`C10CandidateChecks` covers the source hash, 350 projection roundtrips, nine unique selectable site bodies, 13 forest cells, 21 default actors' rendering/picking, six open exit picks and unchanged state. An optional carried opening additionally checks the 20-unit route with fallen Torren. Minimum measured actor body selectability is 88.9%, above the 70% regression floor. These assertions do not certify artwork pixel semantics or full campaign balance.

The previous C10 dimetric scene is frozen unchanged in `tests/native/c10-occlusion-scene.json`. Two presentation tests load that explicit fixture, preserving original actual-overlap, occlusion-cue and cache assertions while production art evolves.

Manual native review used genuine C07–C09 carry into C10 at 1180 × 812 and 960 × 650, with the same opening save and scale. Opening and objective-focus views keep the exit clear of HUD and avoid exposed background edges. Mountain groups, grassy slopes, forest contacts and site bodies were compared against canonical positions; painted transitions are deliberately softer than cell boundaries.

A private production playthrough issued 179 legal commands, replayed every event/hash, and won by moving Laiya into (23,0) on turn 11. T4 aid and T6 reinforcement boundaries were also loaded through native F9 continuation and their battle-journal entries observed. That event GUI check preceded only the final outside-map shoulder pixel edit; no rules, registration or controls changed afterward. This is not a claim of manually playing the full campaign, verifying transient audio/toasts, performance benchmarking, or exported-build acceptance. Research saves, runners and screenshots remain outside the repository.
