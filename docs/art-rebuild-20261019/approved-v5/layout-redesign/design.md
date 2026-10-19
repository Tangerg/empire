# c01-01 双子丘陵: playable layout redesign

Status: implementable design specification, not a runtime screenshot or finished art. Coordinates are zero-based (x right, y down). The complete authoritative recommendation is `layout.json`; `layout-design.svg` is an explicitly labeled vector design diagram. No production repository file was changed by this design task.

## Decision

Retain the 27×17 square4 board and all 19 actors / four rule buildings, but replace the boxed garden composition with a forest pass opening from the lower west toward the upper east. The village is a staggered pair of objectives in the middle, not two buildings on a ruler. The two hills have different silhouettes and three tactical fronts remain: northern crest, village lane, southern bypass.

The reference principle is clear terrain masses and useful negative space. It is not hex geometry, a copied encounter, or a background painting pretending to be a battlefield.

## Reference observations

Official Steam screenshots inspected locally: `/tmp/legends-refs/08.jpg`, `00.jpg`, `06.jpg`, `09.jpg`; source list `/tmp/legends-refs/urls.txt`.

- 08: a few unequal woodland/rock masses frame a broad fighting clearing; the dirt/grass boundary wanders across the scene and props collect into purposeful corners
- 00: the traversable field remains legible around a central obstacle; foliage and water establish a natural diagonal instead of four equally decorated borders
- 06: broad quiet colored ground supports actors; boundary masses intrude with different depths, and small grass islands create rhythm without regular tiling
- 09: even an enclosed arena varies its prop clusters and keeps open combat lanes; do not copy its palisade perimeter for this woodland encounter

Primary reference: https://store.steampowered.com/app/1904860/Legends_of_Kingdom_Rush/

## Exact game layout

Use `layout.json.terrain` as the map rows. It contains 126 forest, 197 plain, 27 elevation-1 hill, 25 elevation-2 mountain, 80 road and four building cells. The outer row is deliberately not a complete forest ring.

Terrain legend: `T` forest, `.` plain, `h` hill/elevation1, `^` mountain/elevation2, `-` road, `q` existing camp building, `v` existing village building. Keep the current character-to-terrain catalog; do not invent new blocking rules.

```
   012345678901234567890123456
00 TTTTTTTT....TTTTTTT...TTTTT
01 TTTTTTT......TTTTT.....TTTT
02 TTTTT..h^^h..TTTT.......TTT
03 TTTT..h^^^^h..TT.........TT
04 TTT...h^^^^h..TT..........T
05 TT...-hh^^hh...............
06 T...--.hhhh--.......----q--
07 ....-.....--v-....-----.---
08 ..q--.-----..------....--..
09 --------..-....v.....---..T
10 -----...TT.....--------..TT
11 ....--..TT..-----hh^^h...TT
12 T....-...----....h^^^^h.TTT
13 TT...-----......h^^^^hh.TTT
14 TTTT.......TT....h^^^h.TTTT
15 TTTTTT....TTTT....hhh.TTTTT
16 TTTTTTTT..TTTTT.......TTTTT
```

### Routes and pockets

- Main approach: west road enters (0,10), bends at (3,9), (7,8), (10,7), passes the first village (12,7), curves through (13,8) and (18,7), then reaches the enemy camp (24,6) and continues off the eastern edge at (26,6)
- Southern bypass: fork (3,10) → (5,13) → (9,12) → (12,11) → (16,10) → (22,10) → (23,8) → (24,6). This offers a flank under the southeast ridge, rather than an equally long straight second avenue
- Northern infantry spur: west dirt approach (4,9) → (4,6) → (5,5), climb low h cells around the compact crown; eastern descent via h(11,5), road(11,6)/(12,6), village(12,7)
- Exact orthogonally connected path cells are in `routes`. Use that map as the road rules. Natural visual rounding is allowed inside the existing route footprint, never across unrelated selectable cells
- Quiet tactical pockets: west staging x1–7/y7–12, village contest x10–17/y6–10, enemy approach x17–25/y4–9. Decals here should be sparse; do not fill every vacancy with a prop

### Four buildings

| Role | Original | New | Owner |
|---|---|---|---|
| Gray camp q | (1,3) | (2,8) | 1 |
| Village grainhouse v | (12,8) | (12,7) | 0 |
| Village waystation v | (14,8) | (15,9) | 0 |
| Vilsa camp q | (25,11) | (24,6) | 2 |

The two village cells retain their actual capture/economy/heal semantics and visibility. Decorative ground must not hide either one or pretend the second is absent.

### Actor deployment

The order below matches existing `player` and `enemy` arrays; `deployment` strings are supplied directly in JSON.

Allies: campaign-laiya (3,9); campaign-torren (5,8); campaign-elin (3,11); field-cleric (2,10); roderick-reserve (2,12); ridge-scout (5,5); ridge-guard (4,6); west-spear (1,9); village-runner (7,8); camp-guard (2,7); field-knight (6,11).

Enemies: vanguard-1 (17,6); vanguard-2 (20,7); vanguard-archer (21,4); vanguard-cain (24,7); north-shield (13,5); south-sword (22,10); south-bow (20,12); camp-bow (25,5).

Campaign rushed Torren must move from stale (4,0) to (9,4), a real elevation2 north-hill cell; preserve the existing 72 HP cap. The northern scout/guard are separated forward; the south bow occupies the opposing ridge; Laiya begins with support near the camp.

Preserve all stable actor keys, types, facing defaults, commanders, funds, rosters, protect-Laiya+rout objective, `matching-orders` trigger, found_matching_orders signal, and campaign choices. No reinforcements or new mechanics are required.

## Composition and rendering contract

1. No rectangular tree fence. There are five substantial peripheral woodland groupings plus the small south/interior copses. Render lobes of canopy with low understory gaps. Vary silhouettes and depths deliberately, not by tiny jitter on evenly spaced tree anchors
2. `frameMassPlacements` supplies 28 uneven exterior contacts grouped into six frame areas. Replace complete edge loops for this authored scene. The west entry must remain open around y7–11.5; the east entry around y4.5–8.5. The north and south have substantial gaps too
3. Frame contacts are world-cell coordinates: x*TILE/y*TILE, translated by viewport origin. Ordinary `recommendedScenePlacements` still use the current x*TILE/(y+1)*TILE convention. Apply manifest presentationScale exactly once
4. Continue low-detail meadow beyond the rectangular rule field. Remove the dark rounded-rectangle battlefield panel for this scene. Trees, stone shoulders and the irregular painted meadow margin suggest the boundary; do not clip legal cells or introduce a deceptive impassable silhouette
5. Hill terrain is authoritative. The northern crown is compact and lobed; the southeast ridge is long and skewed. Use coherent meadow tops and selected cliff faces along their exposed edges. Avoid one full upright cliff/pillar on every mountain cell followed by another stack of authored cliff modules
6. North ramp: correspond to h(10,5)/(10,6). Southeast ramp: correspond to h(21,11)/(22,12). Do not paint a ramp into mountain-only access for mounted/heavy units
7. Exact 33 recommended existing-prop anchors, revised scene zones, and two farmer positions are in JSON. These retain village/camp life while spreading it into small groups. The recommended anchors are first integration positions, not a claim of verified pixel clearance
8. Rural prop silhouettes stay secondary to four rule buildings and 19 actors. Keep current approved chunky, broad-plane matte outlined style. No new tiny-detail realism. Do not vary character scale to compensate for scenery clutter
9. Keep square movement, targeting, selection and terrain feedback truthful. Never hide the rule lattice by rendering a different spatial plan. Under-unit props obey footpoint depth; interactive/highlighted actors must remain readable when moving into forest

## Implementation touchpoints

- `packages/story-candidate-01/src/levels.ts`: TWIN_HILLS rows, first battle deployments and owners
- `packages/story-candidate-01/src/campaign.ts`: rushed Torren coordinate
- `.../environment-builder-v1/scenes/c01-01.scene.json`: zones, placements, authored frame/region data supported by the chosen scene schema
- `packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts`: currently hardcoded ambient farmers and continuous `sceneFrameForestMarkup` loops; update authored scene ownership without changing unrelated levels
- Existing exact-coordinate presentation tests must be revised for the intentionally new layout and continue testing depth/scale/actor preservation, rather than deleted

A scene-specific composition is authored content, not a second terrain authority. Prefer scene data for frame placements/farmers; map cells remain the single rule owner.

## Validation and stopping condition

Design diagnostics: exact 27×17 rows; 19 distinct actor cells; none starts inside forest or a building; all four buildings connected under simplified foot and mounted costs; rushed Torren on `^`; nearest opposing Manhattan distance 8.

At 17:08 UTC the engineering worker independently reported actual engine validation with zero findings, all 19 actors admitted and all four buildings reachable by every actor's movement class. It also reported 8 occupied 3×3 deployment sectors (old 6), nearest opposition 8 cells (old 12), and correct foot-only access to mountain crests. This is a navigation audit, not completed playthrough/pixel QA.

Required final checks: all original actor/building/objective/roster contracts; current engine tests; both campaign approach choices; movement through every route; select each rule building; live BoardView screenshot with all actors; screenshot with a selected unit near forest and each hill ramp; actual click/hover hit testing; no HUD/layout or minimap regression. Compare the complete scene at play size, not isolated art crops. Stop once the complete playable first level meets these checks; no need to redesign later levels here.
