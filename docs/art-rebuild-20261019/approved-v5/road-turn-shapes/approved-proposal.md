# c01-01 curved-corridor proposal, before production edits

Baseline branch: `dot/road-turn-shapes-20261019`, created clean from main `5d59a23ac5bd44571e3cdae37a44f624330865e1`. Production files remain unchanged at proposal time.

Review `proposal-full-labeled.png`. It substitutes the proposed technical road mask into the captured production BoardView SVG, preserving the camera and every other layer. It is explicitly a diagnostic proposal, not an already integrated runtime capture.

## Geometry

- Derive the current 84 painted cells and all 99 cardinal neighbor edges from the existing material binding. There is no independent visual route list
- Decompose this graph into chains between junctions/endpoints. Remove only redundant collinear control points
- Replace each degree-two 90-degree elbow by a quadratic centerline turn. Maximum tangent cut is22 logical pixels, limited to half of each adjacent straight segment. Short opposing elbows therefore become joined S curves rather than two separately rounded cell corners
- Sweep a32px-wide corridor with round ends. This yields a true centerline curve and roughly30–38px outer turning span, unlike the previous9px per-cell edge rounding
- Junction connections stay where the real graph joins. Explicitly preserve filled2×2 apron interiors; no checker/grass seams inside a broad apron
- Border ports extend to the exact board edge. Singleton and closed-cycle cases are explicit, not accidental graph traversal omissions

## Measured current-map safety

`selected-proposal-safety.json` records the provisional runtime-independent raster checks:

- All84 radius8 center disks remain fully opaque
- All99 shared22px port cores remain fully opaque
- The two existing enclosed grass regions remain open; the road remains one connected region
- No paint on the village notch `(11,8)/(12,8)`, hill/mountain, woodland, water or bridge terrain
- Extra paint above the128 alpha threshold:2.75 square logical pixels, confined to plain cell `(6,12)`; maximum raster extent including alpha is1 logical pixel outside the road-cell union
- Proposed production hard bound: at most2 logical pixels of plain-only shoulder outside that union; protected notch cells are excluded. Occupied road cells remain the only core coverage authority
- Old painted area64046 square logical pixels; proposed80455.75, a25.6% increase. Almost all the growth is inside existing road cells. Single lanes widen22→32px and adjoining double lanes54→64px

This width increase is an explicit tradeoff, not a hidden change. It permits broad continuous turns while retaining every existing safe center and junction interface. The28px candidate leaves unwanted narrow seams in broad aprons; the36px candidate adds unnecessary exterior paint and was rejected. Global morphological closing/opening was also rejected because it filled broad side regions without a sufficiently clear route contract.

The common22px maximum cut has a useful safety bound: a90-degree quadratic's largest displacement from its removed vertex is `22/(2*sqrt(2))≈7.778px`. Adding the protected8px center radius stays below the16px half-width. This is a geometry bound, not a substitute for raster and actual-map tests.

## Implementation boundary after review

Scene-owned corridor width/cut/shoulder/notch configuration, c01-01 play only. Existing material/source/UVs remain untouched. The actual map derives the graph on render. Use self-contained SVG alpha geometry with the existing world-aligned earth image, no painted texture, noise, new terrain or actor relocation. Editor and other levels retain the current path.

Tests should cover every edge exactly once, degree-two chain simplification, all turn rotations, open chains, cycles, singleton, junctions,2×2 and3×3 aprons, protected center/port areas, bounded plain-only shoulders and bridge exclusion. Add actual84-cell mask/raster coverage, before/after state/layer hashes for all16 levels, matching source hashes, complete same-camera frames and native/2× crops. Rerun typecheck/full tests and all60 replay streams against this baseline.
