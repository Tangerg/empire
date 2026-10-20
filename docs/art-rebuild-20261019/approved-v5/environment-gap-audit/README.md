# Gray Flag environment gap audit

Read-only capture of the actual production `sceneLayers` for all 16 campaign levels. No repository/Git edits, new art, test-suite rerun, or live-browser claim. The parent subsequently reports the 524 checkpoint passing and pushed; these artifacts retain their own exact input/projection hashes.

## Result

- **Only four actual drawings still use legacy environment RGB:** two wood and two stone vertical bridges, all in c01-12. They are two original atlas slots, not four new topics
- **423 dirt-road cells across the other 15 levels are already approved art.** Their 423 edge companions are intentionally transparent. The remaining issue is repeated cell sampling. c01-01 already has the continuous 84-cell road/settlement material
- **190 walls across six levels use three approved sprites:** 61 straight, 64 corner, 65 gatehouse. The remaining problem is hash-selected disconnected shapes and false gate appearances, not old RGB
- **Active world bindings are complete for the audited ground families:** 625 forest blends, 303 earth/shore blends, 759 stone-route cells plus their 759 edge drawings, 162 water/bridge occupancy cells, all active ground tones, and 14 horizontal crossing drawings
- All 106 currently emitted temperate cliff modules use approved sources. All 16 highland module slots are also replaced but currently unused. The separate 32 automatic elevation-edge slots are unused and must not be activated by that fact

The current authority is the final environment manifest plus typed renderer consumers and `progress.materialStages`. The older `environment-atlas-manifest.json` labels are historical inventory, not completion status.

## Counts by actual level

| Level | Repeated approved dirt cells | Approved water occupancy | Approved horizontal decks | Old vertical decks | Approved wall sprites |
|---|---:|---:|---:|---:|---:|
| c01-01 | 0 (84 continuous) | 0 | 0 | 0 | 0 |
| c01-02 | 33 | 28 | 3 | 0 | 0 |
| c01-03 | 34 | 0 | 0 | 0 | 29 |
| c01-04 | 36 | 0 | 0 | 0 | 0 |
| c01-05 | 24 | 0 | 0 | 0 | 0 |
| c01-06 | 32 | 28 | 3 | 0 | 0 |
| c01-07 | 60 | 0 | 0 | 0 | 35 |
| c01-08 | 51 | 0 | 0 | 0 | 30 |
| c01-09 | 22 | 0 | 0 | 0 | 8 |
| c01-10 | 26 | 0 | 0 | 0 | 0 |
| c01-11 | 8 | 0 | 0 | 0 | 0 |
| c01-12 | 6 | 90 | 4 | 4 | 0 |
| c01-13 | 66 | 0 | 0 | 0 | 68 |
| c01-14 | 8 | 16 | 4 | 0 | 20 |
| c01-15 | 8 | 0 | 0 | 0 | 0 |
| c01-16 | 9 | 0 | 0 | 0 | 0 |

## First bounded end-to-end batch: the two southern stone crossings

**Target:** c01-12's vertical runs `(6,9)..(6,11)` and `(17,9)..(17,11)`. Both are three cells long, with deck pivots `(208,336)` and `(560,336)` world pixels. Each meets a three-cell horizontal run at row 10. Each connected component is a five-cell plus, with NESW 15 at its center. The four existing images at these two nodes must be considered together; registering the two vertical pictures alone reproduces the rejected doubled-parapet junction.

Use the existing approved source:

`packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/crossings-fortifications/stone-bridge-vertical/stone-bridge-vertical-master.png`

- SHA-256 `49d5a96e9a051aecb6196d8cb5f9312beb60e31cf6eafcb0f2bc483f543c0e03`
- Complete native canvas 1254²; independently inspected deck pivot `(627,594)`; endpoints `(627,202)` and `(627,986)`
- Uniform scale `96/784 = 0.12244897959183673`; full logical canvas 153.5510204²; logical pivot `(76.7755102,72.7346939)`
- Existing crossing exporter rounding yields independent **154² / 307²** PNGs. This is intentional independent rounding, not an assumed 154/308 pair
- West strong-alpha bounds: `(171.755,277.592)..(244.735,400.898)`; east is translated +352 X. These bounds include parapets/supports, not just the traversal deck
- Keep the native alpha-1 edge residue. Do not bbox-fit, threshold alpha, recolor, rotate, stretch one axis, or invent bank landings

The existing horizontal source is `masters/approved-v5/bridge/bridge-master.png`, SHA `8185b37a95725c9f6840dd1f8a15fc647706b86f9666f7e7b3dc2142d858ac15`; retain its current registration from `registered-crossings-v1/manifest-crossing-v1.json`.

### Required engineering change

Give the existing crossing consumer one topology owner for each connected crossing component. Compose the two approved directions with explicit complementary junction masks: open the horizontal parapet where the vertical deck crosses, and open vertical side parapets where the horizontal deck meets. Preserve the complete source-derived appearance outside those port windows. This is a deterministic presentation derivation from approved sources, not a new RGB commission.

Exact source rail-band cut coordinates need measurement and native/full-map review in the isolated implementation. A whole-tile erase, hiding one whole direction, moving a bridge, or merely placing both full pictures on top of each other is not a completed junction. If masking exposes missing support art or cannot produce a clean readable joint, stop and request one isolated junction source; do not paint filler or claim source registration alone solved it.

Maintain the current **absence of `BoardPiece.foot`** for lying decks. The atlas's `(48,91)` standing anchor is not their pivot; current vertical pictures use `(48,48)`. Adding a standing foot would change global depth order. Keep existing standing actors, structures, target gauges and normal ordering.

West traversal ports are N `(208,288)`, E `(256,336)`, S `(208,384)`, W `(160,336)`; east ports are +352 X. Source deck alpha is near 253, so exact technical mask coverage is separate from source-alpha opacity. Supports may retain reviewed overhang into the existing water channel; do not misstate the entire sprite as zero alpha outside its tactical cells.

### Why the north remains separate

The two wood runs `(6,2)..(6,6)` and `(17,2)..(17,6)` have approved native candidates, including the long variant, but remain unaccepted. The failed full-map pilot is retained at `docs/art-rebuild-20261019/approved-v5/vertical-crossing-pilot/candidate/runtime-standing-full@1x.png`.

- `bell-a`, a real targetable `boss_part`, is at `(6,4)`, exactly the west crossing center
- `bell-b` is `(16,4)`, one cell west of the east center `(17,4)`; the mechanism-guard also occupies this coordinate
- `bell-c` is `(11,10)`, away from both southern source envelopes; oath-echo-2 shares its tactical cell
- Boss parts have HP 180, half cover and obstruction height 2, but **do not block movement or vision**. They own the composite objective, not bridge topology or a new gate rule
- The watergate source has 128² frames, anchor `(61.7025472,112.2267872)`, footprint metadata 2×2, and normal/damaged/captured states. It must remain drawn and targetable, without moving it or inventing a closed route

Keep the two wood atlas drawings byte-identical and label them pending. Successful stone integration closes **2/4 active legacy drawings**, not the whole vertical family.

### Acceptance and regression gate

1. Pin the saved all-level projection/state hashes before implementation. Preserve all gameplay state, map/targets, actors and structure markup, and the other 15 scenes
2. Check native source hashes, alpha edges, exact independent exports and deck registration. Test both real plus components, their four ports and missing-port/wrong-pivot mutations
3. Produce complete c01-12 BoardView before/after at 884×616, native and 2×, with all 19 actors, six terrain buildings and three target structures. Inspect both southern approaches, especially Ivra `(5,11)`, nearby hills and east-side units. Retain all foreground and targets
4. Verify SVG and GPU/native gameplay, bidirectional crossing readability, selection/target gauges and repeated redraw/edit-preview behavior. No hidden-info change
5. Focused tests: `candidate-01-crossing.test.ts`, `candidate-01-map-scene.test.ts`, `candidate-01-road-material.test.ts`, `candidate-01-water-material.test.ts`, `tools/art/test_crossing.py`, plus new vertical/junction regressions. Then typecheck, full suite and all 60 replay comparisons at the integrated checkpoint

## Separate wall work

Do not activate the four-state pilot. Its masks `[5,10,7,13]` cover only 71/190 current wall cells; 119 require other shapes. Across the real maps the required masks are `0,1,2,3,4,5,6,7,8,9,10,12,13`. The c01-13 pilot covered 27/68 cells and increased several actor overlaps, including Laiya by 22.776 percentage points. The existing approved top/face materials can support deterministic topology work, but height, contacts, all required masks and actor clearance remain production blockers.

## Inactive catalog debt, not current playable gaps

585 physical legacy slots have no actual current consumer: 94 snow/stone Blob masks, 256 mud-road/forest-trail route+edge slots, 192 alternative water slots, 32 automatic elevation slots and 11 crossing/fortification slots.

- Snow/stone blends can reuse the approved `snow-1` and `old-stone-1` RGB with existing alpha
- Mud/forest trail topology can be reused; earth-2/earth-4/forest-floor-1 are candidates requiring semantic and scale review, not automatically equivalent to the original route families
- Distinct stream/marsh/ice RGB families and geological automatic-cliff plane material have no verified matching approved isolated sources. They remain in the already authorized full-catalog scope; schedule them after active-playable gaps, and commission distinct sources rather than substituting unrelated RGB
- The inactive stone-horizontal slot can reuse the approved horizontal source. Ten other unused crossing/fortification identities still need isolated art under the existing full-catalog request

Physical reconciliation: 186 individual source overrides + 128 approved dirt/edge slots + 287 legacy slots bypassed by typed world material + 585 inactive slots + 2 active old vertical slots = 1188. This is not a canonical-topic or live visual-acceptance percentage.

## Evidence

- `environment-gap-audit.json`: concise family/per-level classification and verified world-source identities
- `southern-stone-batch-plan.json`: exact coordinates, source registration, silhouettes, deferred targets, QA and test paths
- `runtime-consumers.json`: exact actual emitted atlas slots, positions and all-level projection/state/source SHA-256
- `c01-12-context.json`: current map, 19 actors, three rule structures, definitions and eight crossing pieces
- `audit-runtime-consumers.ts`, `audit-bridge-context.ts`, `summarize-audit.py`: reproducible read-only collectors; they only write to this audit directory

Twelve approved world-source hashes and both approved dirt-road 2× output hashes were independently verified. No full-suite or browser pass is claimed by this audit.

## Coordinator scope clarification

Unused does not mean excluded: the user has already authorized all existing artwork. The585 inactive derived slots remain explicit backlog, grouped by source family rather than commissioned as585 unrelated paintings. Active-consumer priority only sets order. The287 bypassed slots still require an audit of catalog/export consumers before calling their old stored bytes fully replaced.
