# Faithful original-scene extraction — approved west-region baseline

2026-10-28 user supplied the exact same original C01 screenshot again (SHA256 bdde7dc447c3582614e891bee39ccaadf7c4f777744a6496e6329808a30d6dc0). Goal corrected: change runtime structure, not visual style. Previous v2/v3 files retained for comparison; their last placement documents saved outside repo in evidence/vector-v3-registration. Neither prior candidate nor this one was published.

Built-in imagegen edits of actual original res://assets/native/scenes/c01-01-painted.png, visually inspected before edits:
- house exec-69abecb2-e9ba-45b1-b351-33a81367dd03.png: original leftmost cottage with directly touching crates/barrel/base shrubs; alpha1374x1145, opaque bounds183,191..1243,972; scale.13, source door foot746,942.
- tree exec-5b4b0168-ca03-4f35-a788-1f5d6b59ede9.png: adjacent little pine with root undergrowth, alpha1377x1142, opaque bounds361,122..1044,1034; scale.106, source trunk foot682,962.
- ground exec-4ec6c815-1fea-4e51-9fd0-583ec4eb2fc1.png: original1536x1024 terrain edited to remove all houses and standing trees, retaining clustered grass, low undergrowth, path and hill layers. This is a generated reconstruction, NOT byte-identical extraction, and was visually approved for the west-region sample.

Only WestOutpost and WestPine independent instances use the new body assets. All canonical instance contacts/selection/rules unchanged. Ground plate necessarily feeds the whole view but is being judged for this small complete west-house/tree/ground/actor region only. Central/east houses and perimeter trees intentionally remain previous independently placed artwork pending approval; no claim of complete visual unification.

Ground visual audit: no remaining house, roof, trunk or large conifer crown found. Low bush clusters remain as ground-level dressing, not separate standing blockers. Original hill lips, scattered rocks/boulders and terrain ledges remain in ground painting; therefore NOT a claim that every raised stone has been independently extracted. Dynamic tree/house occlusion comes only from independent objects, not background UV cutouts. House/tree root shrubs travel with their sprite, while low surrounding bush/grass dressing stays in terrain.

Imported object mipmaps were force rebuilt and LinearWithMipmaps rendering used (no source image manipulation). AssembledGroundChecks145 passed. Native real battle sample and matching125% TerrainPreview captured. Gameplay untouched; no new battle completion claim. User approved this west-region appearance on 2026-10-28 at 12:33 UTC: “这次可以，左边这个效果不错”. This commit preserves the approved baseline before completing the remaining C01 objects.
