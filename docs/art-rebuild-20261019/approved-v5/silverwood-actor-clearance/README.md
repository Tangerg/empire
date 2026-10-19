# Silverwood: bounded decorative actor clearance

Status: accepted bounded candidate, not overall art completion. Complete actual
BoardView/SvgBoardSurface exports, rasterized offline; no live browser/GPU pixel claim.
Baseline code: `6ac7f6255a831d58bfe149266b023509f3fddc99`.

[Full labeled before/after](full-before-after.png),
[actual before](before/runtime-standing-full@1x.png),
[actual after](after/runtime-standing-full@1x.png),
[reproducible invariant/alpha report](review.json).

## Exact scope

Only c01-15 decorative composition changes. Twenty-six high-canopy pieces become
15 fern beds and 11 low stumps at their original contact points. Two decorative
hill pieces at (17,7) and (18,7) move back exactly 13.76 logical pixels to contact
Y=248, retaining their existing source, scale, flip and standing shadow. These
changes use existing atlas sources. No new image generation or source repainting.

The exact 28 cells are:
(7,3), (14,5), (15,6), (4,7), (16,7), (17,7), (18,7), (19,7),
(0,8), (5,8), (17,8), (18,8), (0,9), (4,9), (14,9), (15,10),
(4,11), (6,12), (17,12), (22,12), (2,13), (21,13), (1,14),
(3,15), (4,15), (5,15), (7,15), (8,15).

[Placement deltas](placement-deltas.json) record original source IDs, origins,
and replacements. Scene JSON is the owner of the authored presentation. The
optional scene reader now actually accepts missing road/frame/outline fields.
`authoredTerrainScenery` and `authoredTerrainSurface` have separate ownership;
Silverwood retains every original ground piece, including the hill surfaces.
Twin Hills explicitly retains both of its previous surface suppression regions.

## Preserved contracts

- 24×16 rule map, terrain, elevations, collision, cover, LOS, all deployments,
  objectives, triggers and turn limit 17 are unchanged
- 22 actors, eight terrain-building drawings and the mother-root structure remain
  byte-identical in the actual renderer; all original actor source sheets remain
- Victory still protects mother-root and Laiya while eliminating owner 2
- The outer frame and backdrop remain identical; no visibility-dependent fading,
  forced foreground actors, changed hidden-unit state or selection workaround
- All 16 level/state/ground/editor-ground contracts match. Only c01-15 under-units
  changes. The other 15 scenes retain ground, under-units and over-units
- All 26 PNG sources in the before/after capture have identical SHA256 hashes

The sparse checkout uses asset-support links into the integration checkout.
Therefore the full suite is engineering validation against that supporting asset
baseline, not a self-contained immutable branch artifact. The captured comparison
is bound by its explicit hashes. It retains older mixed decorations and the older
woodland-walker actor art; later source integration is a separate capture.

## Actual alpha overlap

Raw actor-sprite alpha covered by later standing/frame pieces, sampled at native
world-aligned pixels. It excludes CSS shadow filters and is not a visual-recognition
score. Every one of 388 original/after drawing panels was checked for clipping.

- controlled-walker: 100% → 0%
- oath-druid: 96.9% → 8.2%
- silverwood-guard: 99.7% → 20.5%
- silverwood-scout: 97.1% → 0%
- silverwood-cleric: 96.1% → 19.8%
- campaign-ivra: 72.3% → 4.9%
- north-longbow: 51.8% → 2.2%

Eight actors originally exceeded 96% coverage. None do after this change.
Nineteen actors are now below 30%; no actor's measured coverage worsens.

Three remain substantially obstructed: controlled-longbow-1 69.8%,
controlled-longbow-2 63.7%, campaign-tasha 54.6%. Their unchanged rule buildings
alone account for 61.3%, 61.3% and 43.6% respectively. They are explicit remaining
work, not evidence of complete actor clearance. Small original shrubs/stumps also
still overlap some legs. No further deployment or rule-building adjustment belongs
in this candidate.

## Verification

- Typecheck passed
- 40 targeted tests across five files passed, including six new exact-cell,
  actual-decoration, surface, frame, contact, shadow and rule-state tests
- Existing hidden-selection and two-backend depth contracts passed (structural
  renderer tests; not live GPU pixels)
- Four Python overlap/order tests passed; the final committed measurement tool
  reproduced all 22 actual candidate measurements exactly
- Review tool passed: 26 PNG hashes, all 16 scene contracts, 22 actor drawings,
  eight terrain buildings, mother-root, unchanged frame and state
- Negative guard: substituting the old alpha result correctly fails on north-longbow
- 60 replay event streams and outcome digests are byte-identical. SHA256 of each
  uncompressed stream: `3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9`
- Full sparse-worktree run: 965 tests passed, two tests failed, one suite failed
  collection. Failures are missing support data: 45 old documentation targets,
  legacy crossing baseline PNGs, and candidate-02 catalog manifest. Original log
  retained in [full-tests-sparse-failures.log](full-tests-sparse-failures.log).
  A complete integrated main run is still required; this is not a full-green claim
- Application builds not rerun for this candidate

## Reproduction

Use `SCENE_REVIEW_LEVEL=c01-15` and `STANDING_REVIEW_OUTPUT` with
`tools/art/review_gray_flag_scene.ts` at the baseline and candidate revisions.
`tools/art/review_candidate_scene_contract.ts` captures the 16 pure projections.
`npm run --silent sweep` emits the complete replay event streams.

Run `python tools/art/review_scene_actor_clearance.py CAPTURE_DIRECTORY OUTPUT_DIRECTORY`
for each capture. It renders all actual standing pieces and the separate outer
frame before measuring later-drawing alpha. No scene piece is omitted to improve
its score. Then run
`python tools/art/review_silverwood_clearance.py docs/art-rebuild-20261019/approved-v5/silverwood-actor-clearance`.

The committed JSON, complete SVG/PNG captures, compressed event streams, and logs
allow the comparison to be inspected independently of this prose.
