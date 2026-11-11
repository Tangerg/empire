# Independent gravekeeper recoil source and offline registration review

The four original recoil paintings and their offline registration are accepted at uniform scale **0.0688**. This is source-art acceptance only, not native playback, build, legal-gameplay, full-gate or performance acceptance.

## Visual findings

The reviewer opened actual original pixels for the canonical gravekeeper gait, accepted breathing and casting atlases, generated recoil source, extracted recoil atlas, both-facing native-size contact sheet and its 3× nearest-neighbor enlargement. Individual both-facing diagnostic frames were also inspected across breathe-rest → onset → peak → recover → ready → breathe-rest and cast-ready/rest handoffs.

The grey swept hair, mature full beard and brows, dark scarf/coat, cream sleeves, leather belt/pouches, silver flask and brown boots preserve the established keeper identity. All four drawings have complete shovel blade/shaft/cap, attached hands, lantern loop/body, torso, limbs and boots. The shovel stays in the character's right hand (screen-left in original facing) and the amber lantern in his left, with the expected mirrored convention. No cropped or floating equipment was found.

The onset lowers the head and tightens shoulders/arms, the peak leans the upper body backward with bent knees and elbows, recovery braces forward, and ready returns near upright. Changing face, torso, shoulders, knees and equipment articulation establish genuinely drawn poses rather than a rigid transformation of one image. The recoil reads at native display size in both facings. The four pictures are modest and distinct without resembling a fall or a cast.

One scale of 0.0688 is applied to every crop. Boot anchors share source y=638; the native nonzero-alpha bottom is 0.6192–0.688 pixels below the logical support anchor, a 0.0688-pixel range across the action. This is antialiased painted sole extent, not a change in root/support placement. Both-facing contact and sequential still comparisons show no visible foot/size jump between accepted idle, new recoil-ready and cast-ready/rest. The peak widens naturally through arm/torso articulation rather than per-frame fitting.

## Exact pixel verification

`verify-source.py` independently reconstructs each significant component using a radius-three Euclidean binary-dilation disk, rather than invoking the author's extraction script or using its global distance-transform ownership result. It confirms:

- Exactly four significant components; all four tiny >16-alpha pixels in three extra fragments are retained by the fringe mask.
- 612,081 retained original nonzero-alpha RGBA pixels and exactly 12 transparent pixels on all four sides of every crop.
- Only 98,383 detached low-alpha pixels removed, with maximum discarded alpha 7/255. Every source pixel above alpha16 is retained.
- Each standalone pose PNG and the complete 1965×576 atlas exactly equal independent source reconstruction. No recoloring, repaint, per-pose resizing or rotation is present in runtime pixels.
- Source, references, prompt, crops and atlas hashes agree with the packet. An in-memory one-channel visible-pixel mutation is rejected.
- Declared onset/peak/recover/ready spans are40/110/90/80ms (320 total); offline GIF frames match their separate diagnostic-sequence metadata.

See `independent-source-verification.json` for precise bounds, anchors, hashes and counts. Production manifest/bytes must still be checked after integration. Static diagnostics and GIF frame-duration metadata were reviewed; continuous realtime video playback was not claimed. Native scene crowding, feedback occlusion, actual event ordering and saved outcomes remain for native review.
