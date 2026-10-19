# Compact field-post contract and three Black Camp clearances

This checkpoint prepares a map-structure presentation variant and applies exactly
three decorative changes. It does **not** activate new field-post artwork. The
refugee-shelter compound still renders until the separately reviewed compact trio
is exported, verified and explicitly bound.

## Ownership and scope

- Canonical topic remains `C01-STRUCT-REFUGEE-SHELTER`; campaign count stays 404 and
  overall denominator 1,212. No canonical record or artwork is changed here
- The role binding may select `presentationId` from the dedicated variant document
- The resolver compiles to the existing `Candidate01RuntimeAsset`; existing
  `candidate01TerrainPicture`, `structureAssetMarkup`, `CELL_FOOT`, ownership
  banners and depth sorting remain the rendering path
- Strict synthetic tests reject invalid state order, dimensions, HD density,
  pivots, footprints, scales, schema fields, paths, duplicate identities and
  mismatched bindings. No hero-specific appearance metadata is reused

The optional variant owns only visual sheet geometry/pixels. It never owns rule
terrain, HP, occupancy, capture, healing, mission triggers, exits or visibility.

## Exactly three scenery changes

At c01-04 (4,9), the original pure-decor `stone-well` keeps its atlas source,
scale 0.3720204, flip false, opacity and standing shadow. Its foot moves from
[145.34498,325.76] to [145.34498,312], 13.76 world units north within the same cell.
This puts it behind same-cell Mirelle at [144,319].

At (2,10) and (4,12), the original `pine-mature-b` decorations (forest-existing
atlas index 10) become the existing `fern-bed`, scale .48 and flip false. Their
feet remain [76.49765,357.76] and [146.64546,421.76]. Forest rule terrain remains
opaque with half cover, obstruction height 2 and defense .2.

All other scenery, ground, frame and editor ground are unchanged. All 19 actors,
25 capturable terrain buildings, ownership coordinates, evidence sites and exits
remain unchanged. Actor-to-actor overlap and the large shelter compound remain
visible outstanding work; these clearances alone do not establish whole-map
visual acceptance.

## Regression evidence

The full initial state SHA-256 remains
`9af8acbacca70a93dee3902487ed715ddd1f87bc8b80da55305355f12a448a0d`.
All 16 level/state/ground/over-unit/editor-ground fingerprints match the baseline
085a37e; only c01-04's under-unit scenery fingerprint changes.

All 60 replay sweep cases (20 shipped levels × 3 AI profiles) have byte-identical
event streams and outcome digests, 47,815 lines in each capture. Controlled real
engine actions verify field-post capture and heal 15, defense .15/movement costs,
evidence once per site, three-site retreat, evidence-before-exit victory and
Laiya-loss failure. These fixtures do not claim a manually played route.

The exact actor/building/visibility inventory and all 34 field-post cells across
c01-03/04/07/08/09/13 are retained in the presentation regression fixture.

## Source activation gate

The compact source remains independently reviewed and unbound at this checkpoint.
Before binding, use `check_map_structure_presentation.py` against its actual
registered native/HD export. It enforces source hashes, byte identity, complete
full-canvas placement, exact frame/contact registration, transparent perimeter and
alpha>=2 support within [-14,-26,+14,0] world relative to the footing. All nonzero
and alpha>=128 bounds are reported separately. Do not erase alpha=1 native dust.

After activation, replace the explicit unactivated-catalog test with approved
source byte/geometry assertions and confirm only `c01.field-post` selects the
variant. Repeat complete c01-04 1x/2x rendering, all six consumer-level reviews and
actual source-alpha/owner-banner occlusion checks. Browser/GPU visual acceptance
and the remaining whole-map crowding are still separate gates.
