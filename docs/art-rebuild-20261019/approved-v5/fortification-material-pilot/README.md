# Four-state masonry material pilot (diagnostic only)

Base: `c142e009db2961a68a4de1d4ee51da9cfe55e1bc`. No application, terrain, scene profile, deployment, navigation, rule, objective, collision or source-image file is changed by this commit. The pilot lives in `tools/art`; applications do not import it.

## Review result

The four supported states now share exact filled top interfaces and opaque exterior-side interfaces. Their geometry is derived from actual cardinal wall occupancy. The 16-source-pixel/world-unit comparison reads as masonry at play size; the 4-pixel comparison is visibly too coarse. This is a topology/material experiment, **not approval of a finished fortress kit**: the conservative lift leaves a narrow east face, and the top-heavy two-cell-wide masses still resemble raised masonry platforms. The 16-density source core repeats every 64 world pixels and has real edge color differences. No seamless-material claim or texture repainting is made.

Review the full scenes as well as the open strips. `production-vs-dense-pilot.png` compares the actual production scene to the 16-density diagnostic. `density-full-comparison.png` compares both material scales in the same actual scene. The primary full images explicitly label every wall cell as pilot P or unchanged legacy L.

## What the actual consumer proves

- c01-13 is still 26×16, with 68 wall cells, 21 cardinal components, 60 connections and 152 exterior edges
- Masks use N=1, E=2, S=4, W=8. Only 5/10/7/13 are implemented: 3/4/10/10 cells, respectively, totaling 27
- The other 41 wall drawings are byte-identical legacy pieces, individually marked in the diagnostic. They are not mapped to the nearest supported state
- Before: 24 random straight props, 18 corners, 26 gatehouses. After: 27 material modules plus 11 legacy straights, 13 legacy corners and **17 legacy gatehouses**. Nine inappropriate gates disappear only because those cells belong to a supported pilot state; this does not close the remaining gate problem
- There is no gate/entrance rule on these wall cells. Pilot materials contain no door, breach or gate identity
- All 24 actor markups, 18 building markups, serialized battle state, level data and editor projection remain identical. Only the `scenery` layer differs
- All 27 existing PNG inputs remain byte-identical; the diagnostic adds only the two native material masters. Source hashes match both the base commit and the read asset files
- This is actual BoardView/SvgBoardSurface output rasterized offline with the repository's CSS/facing normalization. It is not live browser/GPU evidence

`actual-topology.json`, both `provenance.json` files, `verification.json` and `source-hash-verification.json` contain the exact coordinates, per-cell current bindings, hashes and invariants. The actor-wall diagnostic measures overlap with only the 27 changed wall pieces in actual depth order; it does not certify visibility through unrelated buildings or trees.

## Actor-clearance finding: production blocker

The real initial-pose delta check found increased overlap with the changed wall pieces on five actors:

| Actor | Before | After | Increase |
|---|---:|---:|---:|
| Laiya | 2.1933% | 24.9693% | 22.7760 percentage points |
| Torren | 0% | 4.7505% | 4.7505 points |
| archive-guard-1 | 8.8851% | 16.1839% | 7.2987 points |
| archive-mage | 0.5828% | 13.4960% | 12.9132 points |
| central-archive-mage | 0% | 0.1577% | 0.1577 points |

These are alpha-weighted source-sprite overlaps with the 27 changed walls drawn **later** in the actual shared depth order, excluding badges/rings and other scene occluders. All 24 actor sprites are measured and their mask crops are verified untruncated. They are not a gameplay visibility metric or animation guarantee. The complete wall/actor atlas and full-frame closeups are retained. This is a second production blocker alongside partial topology, shallow side readability and source repeats; counting 24 DOM actors is not sufficient clearance. No actor is moved and no fade/reveal rule is added by the pilot.

## Geometry and source contract

- Pitch 32; complete top footprint `[0,0]..[32,32]` lifted by `(-4,-16)`. The diagnostic uses local cell coordinates rather than a mostly empty 96×96 sprite canvas; the geometry is equivalent to the approved registration guide
- Ground contact is `(16,32)` in each cell, using the existing shared depth order
- Top is `(-4,-16),(28,-16),(28,16),(-4,16)`
- An exposed east side joins `(28,-16),(28,16)` to `(32,0),(32,32)`; an exposed south side joins `(-4,16),(28,16)` to `(0,32),(32,32)`
- Connected sides never get interior faces. With this camera lift, north/west sides are rear-facing: mask 7 has a full cap and no camera-facing side; masks 5/13 have east faces; mask 10 has a south face
- Nominal silhouette overhang is exactly 4 world pixels west and 16 north. Ground contact stays in the occupied cell. No rubble, backplate, gate, shadow paint or decorative footprint is added
- Adjacent implemented collinear side pieces overdraw the **same UV sample** by 4 world units along their shared side axis, strictly inside their existing union. This removes alpha gaps from independently antialiased diagonal cuts. It neither expands the union nor extends toward unsupported legacy states
- UV origin is shared by each actual cardinal component. A flat stock bitmap is projected onto planes; no whole isometric prop is rotated or mirrored
- Both native masters are unchanged 1254×1254 opaque RGB. Core is `[115,115,1139,1139]`
- Density 4: core covers 256×256 world units without repeat; face V window 563..691
- Density 16: core covers 64×64 world units and repeats using an opaque SVG pattern, with continuous component phase. Face V window 371..883. This extends beyond the earlier suggested 435..819 band but remains inside the approved native core
- Physical wall-face height is 32 units, projected to `(4,16)` screen relief. Material scale is preserved in those plane coordinates; the entire suggested source band is not squeezed into one face

Native hashes:

- Stone top: `7086ba504e68824dc94f761bda9e369229d2033d6664e79c82e1753e7d1e775f`
- Stone face: `f239c6dacebdfbaccd9341d4ed0edddeea6325226eb1a568758536165325c31c`

## Verification

- 12 TypeScript tests: real topology counts, all unsupported masks rejected, full caps and exact shared interfaces, visible exterior planes only, projection envelope, continuous UV and affine source windows at both densities, source hashes and source-range refusal
- 5 Python raster tests, covering both densities and native/2× rendering: fully opaque top/side ports, complete two-cell interiors, no extra alpha outside the open strip, real 64px repeat phase rather than 32px stamping, context-derived masks, and deliberate missing-port/anchor/side-seam mutations
- Full-opacity checks are exact: every required strip pixel is 255 and every outside pixel is 0. A one-native-pixel RGB rounding difference of one channel value at a repeated south-face overlap is recorded; 2× repetition and all top-only repetition are exact. This does not relax alpha coverage or continuous UV tests
- Typecheck passes
- The initial side-cut failure is retained as a small raster fixture: butt-clipped surfaces had alpha 195/227 at their joints. A second failure with individually clipped repeating stock tiles exposed the same issue at texture repeat boundaries; an opaque source pattern fixes that boundary without altering source pixels
- No full application suite or 60-run gameplay replay sweep is claimed for this unbound diagnostic. Production code and mission data are absent from the diff. The earlier road checkpoint's full-suite/replay results are not reused as wall-pilot validation

## Reproduce

From a full checkout with installed workspace dependencies:

```sh
WALL_PILOT_DENSITY=4 WALL_PILOT_OUTPUT=/tmp/wall4 node_modules/.bin/vite-node tools/art/review_fortification_pilot.ts
WALL_PILOT_DENSITY=16 WALL_PILOT_OUTPUT=/tmp/wall16 node_modules/.bin/vite-node tools/art/review_fortification_pilot.ts
PYTHONPATH=tools/art python -c 'from pathlib import Path; from render_ground_review import rasterize; rasterize(Path("/tmp/wall4")); rasterize(Path("/tmp/wall16"))'
PYTHONPATH=tools/art python tools/art/review_fortification_pilot.py /tmp/wall4 /tmp/wall16 /tmp/wall-review --occlusion
node_modules/.bin/vitest run tools/art/fortification_pilot.test.ts --maxWorkers=1 --minWorkers=1
PYTHONPATH=tools/art python -m unittest tools/art/test_fortification_pilot.py -v
npm run typecheck
```

`WALL_PILOT_ASSET_ROOT` is an explicit read root for a sparse engineering checkout; the recorded hashes must still match the intended base. `WALL_PILOT_FIXTURES_ONLY=1` generates only the bounded open-strip fixtures. Changing neither application art direction nor scene profile is part of this diagnostic.

## Gate before production

Review the common lift/side readability and the density/repeat compromise first. Then complete and test all required masks, including ends, isolated cells, corners and concave assemblies. Verify real archive entrances, passages and actor clearance on the complete topology. The current 41 legacy cells and 17 legacy fake gates remain an explicit blocker to enabling a partial kit in production.
