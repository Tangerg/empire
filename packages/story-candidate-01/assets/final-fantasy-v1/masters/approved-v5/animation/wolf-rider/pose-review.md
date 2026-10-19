# Wolf-rider four-pose review

C01-UNIT-WOLF-RIDER binds c01.wolf-rider, with six initial campaign enemies across c01-07 and c01-08 plus the experience-lab gray-wolf actor. Dedicated original four-frame board, order idle / walk-a / attack / walk-b. Original 32x48 frame, anchor [16,47], art footprint [1,1]; no rule footprint change. Mounted wolf and two-hand spear agree with wolf-spear rules: pierce 45, melee/mounted, range 1. Preserve the original rider face, cap/braid, shoulder plate, small round side shield, pale wrapped forward hand, wolf/harness and saddle blanket. The shoulder plate disappears in the original attack and walk-b; consistent idle armor is an intentional continuity repair, not exact frame replication. Opposite wolf diagonal steps and clear braced two-hand spear thrust replace the original subtle actions. Equipment icon and C01-FX-02 stay independent.

## Original identity and symbols
One bare-faced, dark-eyed rider with tan skin, long black braid and short face-framing hair, brown stitched fur-edged cap, small gold drop earring, cream/tan laced tunic, blue-gray near shoulder plate in idle, brown bracers and cross-body leather strap, dark trousers and tall brown boots. Seated on a stocky gray-brown wolf with cream cheek/chest fur, pointed ears, amber-brown eyes, large paws and a hanging bushy tail. Rope chest harness and rust-red-bordered saddle blanket remain distinct from wolf fur. Both hands hold the same short wooden-shaft spear with a steel leaf/triangular point aimed upper-right. Near bare hand is the rear grip at screen-left/lower shaft; far hand has a pale cloth wrap at the forward screen-right grip. A small plain round brown side shield is behind the forward forearm. No sword, bow, antlers or mount armor.
No faction emblem on the plain round brown shield. Keep the small pale gray angular border marks on the rust-edged saddle blanket as observed decorative marks, without inventing runic meaning. Gold drop earring and chest tie knot are costume details, not heraldry.

## Shared mounted scale
Coordinator accepted the complete mounted silhouette at 52 logical pixels. Idle cap top y56 to mean near-side hind/fore wolf sole y961.5 gives 905.5 native pixels. One locked 52/905.5 scale applies to all full canvases. The actual idle visible width 57.254555 logical was explicitly accepted without forcing the preliminary 60 lower bound. The rider is not independently resized, and the wolf volume is not separately fitted. The wolf paw anchor, not the rider boot, determines ground registration.

## Geometry
Idle, attack and walk-b are native 1586x992; walk-a is 1585x992. Exactly one transparent right column at export resolves the canvas discrepancy; native sources remain unmodified.
Attack foot [753,948] versus canonical [828,970] gives source delta [75,22], maximum logical x shift 4.3070127. Existing limit 4 is insufficient; explicit limit 5 is supported by these measured feet. Every full canvas fits 128x80 at origin [14,6].

## Spear projection caveat
Native visual butt-to-tip estimates give idle spear span 820.41 source pixels, walk-a 821.77, attack 1133.76 and walk-b 807.77. Attack is visibly about 38.2% longer in projected span than idle. It was requested at the same physical length but the output differs; no exact-length continuity claim, hidden rescale or automatic foreshortening justification is made. The braced stance plus extended spear gives attack total width 84.647156 logical, versus idle 57.254555 and walks 61.733849 / 61.791276. Coordinator native shared-scale sequence review must decide whether this projection variation is acceptable or needs a bounded weapon-only correction.

### idle
Native and white-background views inspected. Original bare face, dark braid, fur-edged stitched cap, gold earring, cream laced tunic, near steel shoulder plate, brown bracers, dark trousers and large boot retained. Bare rear hand and pale wrapped forward hand hold one complete spear; plain round side shield remains behind forearm. Four connected wolf legs and paws, large gray-brown head with cream cheek/chest masses, complete bushy tail, rope chest harness and rust-edged saddle blanket are distinct. Broad matte planes and chunky hands/head match the approved family. No rider facial hair, new heraldry, independent FX or ground shadow. Native alpha-1 perimeter specks retained; no pixels alpha >1 on the perimeter.
Source foot [828, 970]; exact four-paw sole midpoint [828.0718930644366, 953.0]; full-canvas offset [0.0, 0.0]; native visible bounds [276, 56, 1273, 971]; full logical canvas [14.0, 6.0, 105.078962, 62.967421].

### walk-a
Clear diagonal walk: near hind and far fore paws support; near fore and far hind are raised and bent. Four connected wolf legs and paws, one full bushy tail, seated rider, both hands on one spear and side shield are visually checked in native and white-background views. Cap, braid, shoulder plate, pale wrapped forward hand, harness and saddle blanket remain coherent. Rider body follows saddle without independent rescaling. No external FX or floor. Native perimeter alpha is zero.
Source foot [822, 935]; exact four-paw sole midpoint [821.957029484742, 917.0]; full-canvas offset [0.344561016, 2.00993926]; native visible bounds [237, 35, 1312, 936]; full logical canvas [14.344561, 8.009939, 105.423523, 64.977361].

### attack
Clear forward lean and two-hand spear thrust while the wolf braces all four paws in a wide stance. Four connected wolf legs, one full bushy tail, two rider arms/hands on one shaft, and the attached plain round side shield are visually verified in native and white-background views. Cap, braid, shoulder plate, face, clothing, harness and saddle blanket remain coherent. Forward hand remains pale against the bare near hand; fine wrap divisions are simplified. No independent impact FX or ground shadow. The projected spear span is materially longer than idle; this variation is explicitly recorded for native sequence review, not claimed as exact length continuity.
Source foot [753, 948]; exact four-paw sole midpoint [752.7648543239072, 916.75]; full-canvas offset [4.3070127, 1.263390392]; native visible bounds [75, 60, 1549, 949]; full logical canvas [18.307013, 7.26339, 109.385975, 64.230812].

### walk-b
Opposite diagonal walk to walk-a: near fore and far hind paws planted, near hind and far fore lifted. Four connected wolf legs and four paws, one full bushy tail, rider following the saddle, two hands on one resting spear, and side shield are clear in native and white-background views. Original cap, braid, cream tunic, near shoulder plate, pale wrapped forward hand, saddle blanket and harness retained. No independent FX, floor or shadow.
Source foot [870, 949]; exact four-paw sole midpoint [870.2379764614652, 909.5]; full-canvas offset [-2.411927112, 1.205963556]; native visible bounds [239, 37, 1315, 950]; full logical canvas [11.588073, 7.205964, 102.667035, 64.173385].

## Provenance and alpha
All four selected outputs are their first native successes, preserved byte-for-byte as raw/master. Exact prompts, requests and original/reference hashes verified. Native and white views inspected; black/white composites saved. Three 7x7 negative-space probes per pose are completely transparent. No programmatic art edits, alpha cleanup or repository changes.

Coordinator shared-scale native sequence review is pending, with spear projection variation explicitly raised.
