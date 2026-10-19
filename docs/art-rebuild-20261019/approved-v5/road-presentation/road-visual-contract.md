# c01-01 continuous visual road contract

Status: implementation plan only. No production files, images, gameplay or masks were changed/generated for this task. The current c01-01 terrain stays authoritative. This contract follows the phase-one layout and its clearance-only deployment corrections.

## Outcome

The road should read as one winding patch of worn earth through the woodland, with broad connected aprons and rounded bends. It must retain every real road/settlement cell and preserve the three fronts, village access, building entries and current square-grid behavior.

Use the existing approved earth material unchanged, sampled in world space, through a continuous technical alpha mask derived from actual route occupancy. Do not paint a replacement brown texture, recolor the source, invent roadside details, or fake a different road graph.

## Diagnosis and approved source

- Actual road material source: `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/earth-material/revision-2/master.png`
- Source is1254×1254, opaque; SHA256 `9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53`
- `tools/art/connector-overrides.json` is the approved binding. It fixes a **22-logical-pixel core**, a one-HD-pixel shoulder, and a blank redundant edge companion
- `tools/art/README.md`, “Approved dirt-road connector binding,” and `approved-v5/progress.json.materialBindings.dirt-road` record this approval. The large per-atlas scope ledger's empty registered-cell list does not negate the separate approved material/connector binding
- The present atlas repeats the complete earth composition once per32px map cell. All four variants have identical pixels. Whole-field viewing therefore exposes repeated mottled squares even when the port edges are mathematically continuous
- Current NESW masks keep narrow branches at every cell and cannot fill a broad multi-cell apron as one area. This produces square steps, tiny repeated junction geometry and checker-like internal shoulders

Primary runtime locations: `candidate-01-map-scene.ts` groundPieces route connection block, `candidate-01-terrain-materials.ts` DIRT_ROAD, `candidate-01-water-material.ts` for the existing world-aligned material plus occupancy-mask pattern. Keep the current road atlas untouched for every other level.

## Recommended representation

Choose **world-aligned earth + connected-area alpha**, rather than another hand-authored centerline that competes with map cells.

### 1. Exact paint/connection ownership

- Connection occupancy comes from the existing `isRoute(content,map,x,y)` interpretation, including road and settlement thresholds
- Paint occupancy includes actual dirt-road cells and the existing q/v settlement ground. In this c01-01 layout that is80 `-` cells plus4 q/v cells
- A bridge may count as a connection neighbor, but its deck/water is excluded from earth paint. Current c01-01 has no bridge; retain this guard rather than allowing a future road rendering change to cover water
- Forest, hill, mountain, water, wall and unrelated plain cells are outside road paint coverage. No diagonal road connection is inferred
- Do not store a second authoritative list of route cells or control points. Derive occupancy from the live map and the scene's explicit rendering profile. A cached result must be keyed by its actual occupancy, dimensions and mask/source versions
- Enable this profile only on the authored c01-01 scene. Other campaign scenes retain their exact current connector rendering

### 2. Bounded continuous mask

Recommended canonical geometry, in32px world-cell units:

1. Let Ω be the union of the full occupied road/settlement cell squares
2. Inset the **external** contour of Ω by5px, leaving a22px core along a one-cell-wide straight run. Shared road-cell interiors are not separate borders and must not receive an inset
3. Round exposed convex turns with a9px radius; soften inner notches within Ω. A reproducible mask-only construction is `opening(disk9, erosion(disk5, Ω))`, clipped back to Ω. These operations alter alpha only
4. Verify/preserve every route-cell center and every actual orthogonal connection. A protected opaque8px-radius center disk and a22px shared-port core are acceptance constraints, not permission to paint into another terrain cell
5. Keep the approved thin shoulder as at most0.5px of antialias/partial alpha using the same earth source pixels. No new colored outline, dark border, RGB noise or extra edge texture
6. At the actual western/eastern entry edges, keep the existing road port open right to the tactical boundary; do not erode it5px inward or round it into a dead-end cap. This is an image-edge condition, not an extra traversable cell

Implementation can export reusable white/alpha eight-neighbor masks, following the existing water-area pipeline. The erosion/opening influence radius is5+9+9=23px, less than one32px cell, so a3×3 occupied-cell neighborhood is sufficient for the central cell. Build the256 neighbor masks from that canonical geometry, crop the central cell, and independently downsample from one common supersampled world grid to1×/2×. Do not independently fit or warp every cell after masks are built.

This is preferable to a global blur: it cannot spill road paint across a legal-terrain boundary, keeps exact route topology, and fills the interior of2×N aprons instead of decorating them with a row of small crosses.

The morphology recipe is the proposed geometry, **not a claim that unimplemented parameters already pass**. If a fixture fails the22px connection/center constraints, adjust the single mask definition and verify again; do not add scene-specific patches that conceal a failed topology test.

### 3. World-space material sampling

- Reuse the full approved source with no crop, paint, hue adjustment or local texture warping
- Use one logical1024×1024 material canvas at world origin(0,0), consistent with the meadow's broad world scale. c01-01's864×544 field fits inside one period, so there is no repeated material seam inside this level
- Use the existing independent full-image export pattern, e.g.512px low-density and1024px high-density versions of the same source. At a1024 logical period the latter is1 source pixel per world unit; do not label it native2× world detail
- Mask sampling and RGB sampling have separate owners. Route mask coordinates follow the grid; earth UVs never reset at a tile, bend, apron, material chunk or draw-call boundary
- If rendering is chunked for the existing GPU texture budget, every chunk samples the same world UV phase and carries its own definitions. Do not introduce a visible period at the chunk border
- Source pixels in fully opaque mask regions must equal the declared whole-source resample. Only alpha changes with the road geometry

## Exact c01-01 trouble spots

All ranges below are inclusive cell coordinates, for inspection/fixtures rather than a new route authority.

| Area | Current cells to inspect | Intended reading |
|---|---|---|
| West entry/camp apron | x0–4/y8–10, q(2,8) | One broad entry bulge, gently necking toward the main route; retain tower threshold and camp-guard footing |
| Northern footpath bend | (4,6),(5,6),(5,5) and x4/y6–9 | Rounded narrow approach to the unchanged hill, with no earth over h/^ |
| West main-road elbow | x6–7/y8–9 | One smooth widening and turn, not a2×2 arrangement of individual elbow tiles |
| Northern village threshold | x11–12/y6–7, v(12,7) | Small coherent apron behind the village; preserve the northern approach and village entry |
| Village bypass bend | (10,8)→(10,7)→(13,7)→(13,8) | Read the northward bend clearly; **keep(11,8)/(12,8) plain**, otherwise the rejected ruler-straight avenue returns |
| Southeast village fork | x15–16/y10–11, v(15,9) above | A merged fork/apron that divides visibly into village lane and southern approach |
| Northern enemy approach | x20–22/y6–7 | A rounded widening into the camp approach; no square-to-square material reset |
| Enemy threshold/exit | x24–26/y6–7, q(24,6) | One connected camp apron and open eastward exit; keep q identity and door readable |
| Southeast shoulder turn | x21–23/y9–10 | Rounded approach around the actual hill; **no paint into h/^ at y11+** |
| Lower west bypass | (5,11)→(5,13)→(9,13)→(9,12) | Quiet continuous narrow bends under the inner copse, preserving the forest cells x8–9/y10–11 |
| Lower middle bypass | (9,12)→(12,12)→(12,11)→(16,11)→(16,10) | Round the turns without closing the plain strip between routes or adding a shortcut |

Fully occupied2×2 apron blocks have top-left anchors:
(11,6), (20,6), (21,6), (24,6), (25,6), (2,8), (3,8), (6,8), (0,9), (1,9), (2,9), (3,9), (21,9), (15,10).
Their internal shared corners must not have grass pinholes, repeating borders or a four-tile checker pattern.

There is no need to redraw a map-wide Bezier route by hand. If an implementation uses contour curves, the only authored numbers should be the shared width/inset/radius profile and explicit boundary entry ports; contour vertices come from occupancy. Do not use the old design route-centerline arrays as an independent runtime geometry source.

## Units, buildings and depth

- Never move actors, rule buildings, terrain, elevation, owner cells, scenario zones or goals to fit this road image
- Preserve the phase-one clearance corrections, including camp-guard(4,7), Roderick(4,11), south-bow(20,12), and the revised southeast ramp/face anchors
- Earth remains in the ground layer beneath ground decals, building foundations, standing props and actors, matching current route layering. It gets no standing foot/depth sorting and never covers a unit sprite
- Do not reposition an actor's registered foot to the road centerline. The terrain cell and unit foot remain exactly where the engine/renderer place them
- At q/v cells, preserve the existing continuous underlying approach and foundation contact. Do not infer a new doorway anchor by eyeballing an image crop or paint a path through a building's solid side
- The current bridge rendering remains independent. A bridge-entry fixture must demonstrate a road core reaching the land/bridge boundary while no road material is painted over bridge-water cells

## Acceptance / invariant tests

1. **Gameplay identity:** before/after level/state digests match for terrain, elevation, owners, all19 actors, four rule buildings, commanders, objectives, triggers and both campaign choice branches
2. **Source identity:** approved master SHA matches; exported material reproducible; RGB interiors equal the declared uncropped whole-source resample. A one-pixel source or UV shift must fail
3. **Scene scope:** all non-c01-01 rendering remains byte/pixel equivalent for the affected path; current connector assets remain unchanged
4. **Coverage:** alpha≥2/255 is zero outside occupied paint cells, including all forest/hill/mountain/water/wall/plain exclusions. Exact village notch(11,8)/(12,8) remains unpainted
5. **Route identity:** each occupied cell has a protected opaque center; every real cardinal route edge has a continuous core; diagonal-only contact never creates a connected route component; no component or closed loop changes because of smoothing
6. **Width:** isolated straight corridors retain22px opaque core apart from defined subpixel antialiasing. Two adjacent lanes merge to a broad region bounded by the same external inset, nominally54px rather than two22px strips plus a grass seam
7. **Junctions:** all14 listed2×2 blocks have opaque shared interiors with no pinholes; all rotated L/T/+ cases and1×N/2×N runs keep consistent ports at1×/2×
8. **Shared pixels:** valid neighboring masks have exact shared-edge coverage; cropping a world-material chunk from the complete field matches separately rendered chunks within the existing renderer's declared antialias tolerance. Test a non-tile-aligned crop as well
9. **Entry safety:** q/v threshold fixtures stay connected; west rows9–10 and east rows6–7 reach the board edge without a fabricated end cap; bridge paint exclusion is tested even though this level has no bridge
10. **Foot/depth safety:** all19 actual actor origins/feet and four buildings retain their coordinates/contacts; no road piece enters the standing layer. Render all actors and include the already corrected camp/SE-hill clearances
11. **Whole-scene QA:** actual BoardView/SvgBoardSurface and live browser/GPU if available, at normal play scale. Compare native and2× crops of west apron, village bend, southern bypass and east camp. No tile-reset mottling, serrated square junction boundary, dark double edging, false road across forest/hill, or loss of building-entry readability
12. **Mutation checks:** deliberately alter one road cell, one diagonal neighbor, one mask pixel and the world UV origin in test fixtures. The relevant guard must fail; a test that merely counts mask files is insufficient

## Scope and stopping point

Implement/verify only c01-01's visual road profile. The approved terrain/map and phase-one clearance are the baseline; shared material/mask code may be reused without changing other levels. No new earth art, asset generation, route mechanic, forest repaint or wall-topology work belongs in this step.

Finish when the real full scene shows connected earth with rounded boundaries and every safety/identity invariant above passes. If22px connections cannot survive the proposed contour smoothing, report that geometry issue before widening logical roads or relaxing coverage limits.
