# Rural prop scale audit

Checkpoint: `36555c4`. Read-only audit of production art and placements; no catalog,
level, runtime or gameplay changes. The complete scenes are actual initial-pose
`BoardView` / `SvgBoardSurface` output, rasterized offline, with mixed new/legacy art.
This is not a live browser or Pixi/GPU playback claim.

## Recommendation

- **Livestock pen:** change only this catalog cell's `presentationScale` from 1 to
  **1.5**. Its three c01-02 instances currently read more like small flower boxes.
  The bounded change makes them legible small pens without forcing the footprint
  metadata into a 96×64 pixel rectangle. Do not use 1.8: the pen next to the joint
  granary becomes appreciably more crowded against its side awning and right pier.
- **Stone well:** retain `presentationScale: 1`. The authored c01-01 well at 0.58
  is already a reasonable human-scale well. The procedural wells are smaller, but
  a global increase would also enlarge this already suitable authored placement.
  Some procedural wells are largely hidden by existing vegetation; enlargement is
  not an occlusion fix.
- **Wheat drying rack:** retain `presentationScale: 1`. Its authored 0.6 placement
  in c01-01 reads as a low, broad rack beside the granary, with sensible proportions.
- **Roadside waystation:** no scene-scale acceptance claim. No instance occurs in
  the actual c01-01 or c01-02 scenes; its native contact sample is catalog-only.

`footprint` is catalog metadata here, not a renderer instruction to reserve logical
cells or stretch the image to a tile rectangle. The renderer multiplies placement
scale by the cell's `presentationScale` and positions the image about its anchor.

## Exact placements and unoccluded source bounds

Widths/heights below use the supplied source alpha≥128 bounds, before other scene
objects occlude them. Coordinates are world footpoints; screen origin is (74,62).

| Scene / source | Asset | World foot | Current scale | Source-visible size at current scale |
|---|---|---|---|---|
| c01-02 scenery:9 | pen | (626.02629,69.76) | .359 | 31.95×23.69 px |
| c01-02 scenery:22 | pen | (494.96533,197.76) | .412 | 36.67×27.19 px |
| c01-02 scenery:25 | pen | (464.13608,229.76) | .335 | 29.82×22.11 px |
| c01-02 scenery:6 | well | (81.20701,69.76) | .328 | 25.42×27.06 px |
| c01-02 scenery:41 | well | (556.59716,421.76) | .345 | 26.74×28.46 px |
| c01-01 scenery:106 | procedural well | (780.51008,389.76) | .323 | 25.03×26.65 px |
| c01-01 scenery:195 | authored well | (419.2,380.8) | .58 | 44.95×47.85 px |
| c01-01 scenery:196 | authored rack | (518.4,262.4) | .6 | 51.00×36.60 px |

At pen presentationScale 1.5, the three visible sizes become respectively
47.93×35.54, 55.00×40.79 and 44.72×33.17 pixels. The declared pen footprint remains
3×2. No footpoint, facing, depth key or placement changes in this diagnostic.

Nearest context: pen scenery:9 is 30.7px from northern keep terrain:1 and 57.3px
from actor 14 (`shield-1`). Pens scenery:22 and :25 are respectively 31.7px and
38.8px from joint granary terrain:4. This building is **struct-joint-granary**,
not a registry. No actor stands immediately beside those latter two initial poses.
Well scenery:6 is only 6.9px from top-left granary terrain:0; scenery:41 is 38.9px
south of granary terrain:6 and 48.2px from actor 19 (`south-bank-bow`).

## Entrance inspection

`entrance-check.json` records exact pixel comparisons of manually inspected screen
regions, not navigation/collision testing. Both factors leave the northern keep's
doorway/arch region (729,99)-(748,125), 494 pixels, unchanged. Both leave the joint
granary door core (516,218)-(530,241), 322 pixels, unchanged.

The granary's broader doorway/pier region (511,212)-(542,250) changes 44/1178 pixels
at factor 1.5, all at its right edge, x539–541/y229–245. At factor 1.8 this becomes
166/1178 pixels and reaches left to x533. The finite 1.5 recommendation accepts a
small right-pier overlap while retaining the readable door, rather than claiming
that all architectural overlap has disappeared.

## Files and completeness

- `pen-scale-comparison.png`: three matching full-scene crops, current / diagnostic
  1.5 / diagnostic 1.8, each at 1x render pixels enlarged 2x nearest.
- `well-rack-context.png`: matching crops from actual complete scenes, same pixel
  enlargement, with exact authored/procedural scales labeled.
- `c01-02-actual@1x.png`, `c01-01-actual@1x.png`: complete production scenes.
- `c01-02-pen-factor-1.5@1x.png`, `c01-02-pen-factor-1.8@1x.png`: complete diagnostic
  scenes, changing only pen markup scale about its existing footpoint. These are
  **not** current production settings. Corresponding SVG files retain all objects.
- `audit-provenance.json`: every relevant prop, actor, building, source-layer count,
  source file hash and battle-state digest.

c01-02 retains 20 actor nodes, 9 buildings and all 46 scenery pieces in all three
renders. c01-01 retains 19 actors, 4 buildings and all 206 scenery pieces. No object
was selected for omission. All c01-02 diagnostic cases have identical state hashes;
capture asserts state does not change and every actor node is present. No full test
suite was rerun for this read-only artifact generation.

Capture: `npx vite-node tools/art/review_rural_scale.ts`. Rasterize its SVGs with the
existing `render_ground_review.rasterize(output, densities=(1,))` helper. Diagnostic
scale wrappers are only in the capture tool, never in the runtime or manifest.
