# GrayFlag village scale revision

The native selected asset for the village topic is now one stone-and-timber
building, rather than a compound containing baked miniature people, furniture,
fences and a courtyard. Its authored door contact is the sprite anchor. At the
current camera, its door is approximately 36–40 world units high against ordinary
infantry around 30. This is a visual measure, not a new collision footprint.

Map buildings always render their normal body. Canonical map ownership is rendered
only by the existing detached owner banner; no copied captured frame or missing-role
fallback is required. Interactive structure damage presentation is unaffected.
The native manifest is the authoring owner, not the historical TypeScript export.

First-level decorative miniature houses, wells, grain compounds and people were
removed together with the obsolete village foundation. Sparse fences remain.
No gameplay cell, owner, initial unit, objective, or movement rule changes here.

The earth material is generated RGB, sampled with a 256-unit world period. Road
alpha remains derived from canonical road cells. A continuous inward shoulder
softens coverage without spilling outside the existing allowed/protected-cell
mask. The original texture is preserved as source provenance, not a runtime
fallback. `data/native-scene-art.json` records generated-source hashes and scope.

This checkpoint is not final visual acceptance. The old isolated hill props remain
until the separately developed canonical-elevation surface has a convincing
continuous cliff/slope treatment. Later-level building layouts, the remaining
camp-scale props, and the road's fine-grain realism still need visual review.
