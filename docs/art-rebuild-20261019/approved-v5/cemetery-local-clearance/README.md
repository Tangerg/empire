# Cemetery: exactly seven local decorative clearance changes

## Complete comparison and measured result

`before-after-labeled.png` shows the entire actual c01-11 BoardView at the same 852 × 648 camera and the same 24 source PNGs. Both frames have 21 actors and all 8 rule-owned terrain buildings. The comparison is fixed to main baseline `d998fe43532873cc76e27884982dcb82b8690cf4`, before the later Laiya hero registration. It isolates the seven decorative changes. It is offline SVG evidence, not live GPU/browser evidence.

Raw sprite-alpha occlusion changed only for these five initial-pose actors:

| Actor | Before | After |
|---|---:|---:|
| grave-inquisitor | 83.58% | 0.24% |
| Mirelle | 71.59% | 0.98% |
| Tasha | 99.27% | 42.77% |
| echo-3 | 75.83% | 0.00% |
| echo-4 | 95.56% | 0.45% |

Every other actor's measured overlap is unchanged. Tasha improves substantially but does not meet the earlier audit's estimated under30% target: unchanged dead trees at (4,9) and (5,8), neighboring signs and the replacement low fern still cover part of the figure. No extra cell was silently changed to meet that estimate. Five other actors remain above50%: colossus52.42%, Bran57.23%, south-echo65.07%, ossuary-guard61.91%, cemetery-bow62.87%. This is the approved seven-object step, not overall scene clearance completion.

## Exact changes

`seven-changes.json` retains source identities, original visual indices, terrain cell IDs, before/after contacts and original transforms. Those visual indices identify the baseline capture; indices naturally shift after seven procedural entries are replaced by seven authored entries.

Four canopies become the existing low `fern-bed` at placement scale0.48, preserving their exact jittered contact and flip:

- forest(4,8): pine-cluster at (147.34611,293.76)
- forest(14,10): oak-ancient at (464.29394,357.76)
- forest(19,4): oak-grove-dense at (627.10383,165.76)
- forest(2,6): pine-cluster at (80.31995,229.76)

Three cemetery props preserve their source, scale, flip and standing shadow. Only their contact Y moves north13.76 logical pixels, using normal depth ordering:

- graveyard(3,6): grave-cross-cluster (112.53874,229.76) → (112.53874,216)
- graveyard(10,9): grave-dead-tree (338.39988,325.76) → (338.39988,312)
- graveyard(14,9): grave-signboards (464.83064,325.76) → (464.83064,312)

Implementation uses the already shipped scene-owned point-zone/placement contract, plus one c01-11 scene registration. There is no new renderer geometry, dynamic actor-distance test, scale policy or hidden-unit exception. Forest/graveyard terrain semantics, navigation, elevation and collision are untouched.

## Rules and visibility boundaries

The eight `c01.outpost` cells remain at (1,1),(20,1),(5,4),(8,7),(1,8),(16,8),(1,13),(19,13), with owners1,2,0,0,1,2,0,0. They are rule-owned capturable/healing terrain, not disposable scenery. All eight building SVGs and all21 actor SVGs are byte-identical between captures. Torren still correctly stands behind the (1,8) outpost and Laiya; the building contributes26.48% pairwise sprite overlap to his total37.69%.

The three name visit zones remain (5,3),(14,7),(8,12), with their exact owner1 conditions, variable increments and signals. The victory sequence and deployment remain unchanged. These are scenario visit triggers, not identities attached to decorative grave sprites. The existing name-a(5,3) visual-marker gap is not repaired here. None of the seven changed cells is a task zone.

The complete diagnostic fixture uses an empty hidden-unit set so every actor can be audited. It does not assert that all21 enemies/allies are normally visible to the player. The actual BoardView hidden-selected-actor test now also runs on c01-11/echo-4 and verifies SVG/Pixi display-list parity through hidden→visible→hidden→deselected transitions. This is renderer-contract testing with test adapters, not live GPU evidence. No hidden enemy is projected by this patch.

## Validation

- 42/42 focused tests passed across cemetery clearance, existing Silverwood clearance, scene projection, level legality and SVG/Pixi agreement
- 4/4 existing alpha-measurement algorithm tests passed
- Typecheck and four application builds passed
- All16 candidate level/state contracts are identical, with precisely one presentation difference: c01-11 under-unit scenery. Ground, editor ground, over-unit layer and all other levels remain identical
- All60 full replay event streams/outcomes are byte-identical
- No actor alpha area changes; only the five listed overlap fractions improve, none worsens
- Seven exact authored pieces replace seven actual procedural pieces; all unrelated standing pieces remain identical in the focused test
- Both frozen complete SVGs re-render to the stored PNG in every RGBA channel

The full repository suite was not run in the sparse engineering checkout. Complete integrated-main validation remains separate. The focused UI suite emits its pre-existing jsdom canvas warning; all15 backend-contract tests pass. Logs are retained, without widening assertions or timeouts.

## Reproduce

```sh
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-cemetery-clearance.test.ts packages/story-candidate-01/src/presentation/candidate-01-silverwood-clearance.test.ts packages/story-candidate-01/src/presentation/candidate-01-map-scene.test.ts packages/story-candidate-01/src/levels.test.ts packages/game-ui/src/art/__tests__/two-backends-agree.test.ts --maxWorkers=1 --minWorkers=1
python -m unittest discover -s tools/art -p test_scene_actor_clearance.py
npm run typecheck
npm run build:apps
SCENE_REVIEW_LEVEL=c01-11 STANDING_REVIEW_OUTPUT=/tmp/cemetery-after npx vite-node tools/art/review_gray_flag_scene.ts
python tools/art/render_ground_review.py /tmp/cemetery-after --scene-only
python tools/art/review_scene_actor_clearance.py /tmp/cemetery-after /tmp/cemetery-after-alpha
CONTRACT_OUTPUT=/tmp/contract-after.json npx vite-node tools/art/review_candidate_scene_contract.ts
npm run --silent sweep > /tmp/replay-after.txt
python tools/art/review_cemetery_clearance.py docs/art-rebuild-20261019/approved-v5/cemetery-local-clearance
```

For before captures use the recorded d998fe4 baseline. The committed comparison validator accepts compressed SVG/replay inputs. The actual alpha capture helper expects an uncompressed SVG; decompress it beside the PNG when rerunning the raw measurement.

All24 source hashes match the already committed `../graveyard-world-material/sources/` archive exactly. The compressed complete SVGs reference that immutable sibling archive, avoiding redundant source copies. `reproducibility.json` records the captured/relocated SVG hashes and each reused PNG identity. Decompress a full SVG beside its PNG and use `render_ground_review.rasterize(directory,(1,))` to reproduce the stored display-scale frame; `archived-render-verification.json` records exact pixel equality. Sources, actor art and layout are controlled across the pair.
