# Wolf-rider source and proposed idle contract

## Exact source and identity
Dedicated full four-frame source board wasteland-wolf-rider-source.png, 1672x941, left-to-right idle / walk-a / attack / walk-b. No equipment icon used to infer rider identity.
One bare-faced, dark-eyed rider with tan skin, long black braid and short face-framing hair, brown stitched fur-edged cap, small gold drop earring, cream/tan laced tunic, blue-gray near shoulder plate in idle, brown bracers and cross-body leather strap, dark trousers and tall brown boots. Seated on a stocky gray-brown wolf with cream cheek/chest fur, pointed ears, amber-brown eyes, large paws and a hanging bushy tail. Rope chest harness and rust-red-bordered saddle blanket remain distinct from wolf fur.
Both hands hold the same short wooden-shaft spear with a steel leaf/triangular point aimed upper-right. Near bare hand is the rear grip at screen-left/lower shaft; far hand has a pale cloth wrap at the forward screen-right grip. A small plain round brown side shield is behind the forward forearm. No sword, bow, antlers or mount armor.
No faction emblem on the plain round brown shield. Keep the small pale gray angular border marks on the rust-edged saddle blanket as observed decorative marks, without inventing runic meaning. Gold drop earring and chest tie knot are costume details, not heraldry.

## Actual consumers
Actual campaign unit: six initial enemies across c01-07 and c01-08. Additionally experience-lab has the allied gray-wolf actor. No distinct built-in consumer found.
- c01-07: raider-1 at [1, 3] (levels.ts:610,613,616)
- c01-07: raider-2 at [5, 6] (levels.ts:610,613,616)
- c01-07: north-raider at [8, 2] (levels.ts:610,614,616)
- c01-08: thief-chief at [10, 2] (levels.ts:627,630,633)
- c01-08: thief-3 at [19, 5] (levels.ts:627,630,633)
- c01-08: south-thief at [12, 12] (levels.ts:627,631,633)
- experience-lab: gray-wolf at [7, 14] (packages/experience-lab/src/index.ts:99)

## Four-pose and rule contract
All four original cells retain the same wolf/rider/spear identity. Wolf gait differences are modest. Original third attack remains a near-idle spear hold rather than a clear thrust. The blue-gray near shoulder plate visible in idle and walk-a disappears into tan cloth in attack and walk-b; the remake should consistently retain the verified idle plate and record this continuity repair.
Mounted wolf plus spear is consistent with actual rules. Attack should be a clear two-hand spear thrust, with the wolf braced or lunging and the side shield retained. Opposite wolf diagonal steps should carry the seated rider, with one shared camera and scale.
Actual wolf-spear rule: 45 pierce damage, mounted/melee tags, default range 1. Unit movement 7, mounted class, cavalry/wasteland tags. Equipment C01-EQUIP-WOLF-RIDER and C01-FX-02 remain separate.
Original art contract is 32x48, anchor [16,47], footprint [1,1]. This describes the legacy art; it is not a reason to shrink the mount/rider to a 44-high standing human. No gameplay footprint or movement rule changes.

## Idle scale proposal
Whole mounted silhouette: 52 logical pixels high, tail-to-snout width 48–54. Seated rider cap-to-boot approximately 35–39; wolf ears-to-paws approximately 28–32. Keep rider head/hands/boots consistent with the approved 44-high human family, and the whole mounted unit below the 56-high juvenile dragon. These are pilot targets only: one measured idle scale and complete native canvas will become the shared quartet contract after coordinator review.
Native landscape 1586x992 proposed; candidate 128x80 export at origin [14,6], subject to actual idle geometry. No per-pose fit or body shrinking to fit weapon length.
Original HD whole silhouettes vary: idle 30x43.5, walk-a 30x40.5, attack 30x40.5, walk-b 29x46 logical. This legacy variation will not be copied as per-pose scaling.

## Action plan
idle: all four wolf paws grounded; rider upright with two hands on diagonal spear; original side shield and cap/braid retained
walk-a: one diagonal wolf paw pair supports while opposite pair steps; rider follows saddle without changing body size
attack: rider leans into clear two-hand spear thrust toward screen-right, wolf braces/lunges; shield retained; no independent FX
walk-b: opposite diagonal wolf contacts from walk-a; same rider, wolf, spear, saddle and shield

Source audit and file hashes complete. Await coordinator idle composition/species-scale acceptance before image generation.

## Coordinator idle pilot approval, 20:02 UTC
Proceed with verified idle identity, unified shoulder plate, small shield, wrapped hand and saddle blanket. Updated whole-combination target is 48–52 logical high and 60–72 wide, superseding the narrower initial width proposal above. Preserve rider world scale and complete wolf tail/legs/spear. Actual native idle measurement will lock shared scale, canvas and contact anchor before the three remaining poses. Rule 1x1 remains unchanged.

## Native idle candidate, 20:06 UTC
Native RGBA 1586x992. Visible alpha >127 bounds [276,56,1273,971]. Cap top y56 to mean near-side hind/fore wolf sole y961.5 gives 905.5 pixels; proposed 52/905.5. Four-paw contact anchor [828,970]. Full canvas fits candidate 128x80 at origin [14,6]. Visible width is 997 pixels / 57.254555 logical, slightly below the initial 60–72 target; this is disclosed for native proportion review rather than changed by independent mount/rider scaling. Original identity, equipment, four wolf paws and full bushy tail are clear. Three negative-space probes are alpha 0. Perimeter max alpha is 1, with zero pixels above 1; original alpha-1 specks retained. Raw/master/native, prompt and reference hashes verified. Coordinator idle review pending before remaining three poses.

## Idle accepted, shared quartet contract locked
Coordinator 2026-10-19 20:06 UTC accepted idle proportions and actual width 57.254555 without forcing the initial 60 lower bound. Locked shared scale 52/905.5, source foot [828,970], full 1586x992 canvas, candidate 128x80 frame and origin [14,6]. Continue opposite steps and clear two-hand thrust; no per-pose fit.
