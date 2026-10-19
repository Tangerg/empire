# Battle-mage four-pose review

C01-UNIT-BATTLE-MAGE maps to campaign c01.battle-mage, distinct from built-in mage. Original atlas and HD/runtime strips are preserved under references/.

## Source identity
Short black hair, pointed ears, clean-shaven young adult face, navy steel armor over red cloth, brown belt/pouch, steel knee guards and huge brown boots. Screen-left bare hand carries a compact shortsword; screen-right bronze/gold open rune palm/forearm. Spare steel helmet remains on right hip, not worn.

Old runtime-HD includes stray partial eagle-rider heads at its bottom, from the next atlas row. Native source crop[0,615,1254,895] isolates the actual caster row; unrelated fragments were not reproduced.

## Geometry
Fixed44/893 body scale from accepted idle hair crown54 to mean soles947. Common source1586x992; attack/walk-b each need one transparent right column in export only. All whole canvases fit104x64 with origin8,4. No native master editing or independent silhouette fitting.

### idle
Accepted young pointed-ear armored caster, large black-haired head, broad navy/red planes, lowered shortsword in screen-left bare hand, raised screen-right rune palm, spare helmet at right hip.
Source foot anchor [775, 952]; exact two-sole midpoint [774.697, 947.0]; whole-canvas logical offset [0.0, 0.0]; native visible bounds [304, 54, 1313, 953].

### walk-a
Front screen-right boot toe-up/heel-contact with dark sole visible; rear screen-left boot flat. Correct sword/rune hands and carried helmet retained.
Source foot anchor [733, 952]; exact two-sole midpoint [733.201, 952.0]; whole-canvas logical offset [2.069428891, 0.0]; native visible bounds [290, 57, 1324, 953].

### attack
Clear ranged oath-bolt casting: screen-right rune palm extended forward, five spread fingers with circle/diamond palm mark and angular forearm rune; sword remains low in screen-left bare hand. Braced stance and focused face, spare hip helmet intact. No external glow, projectile or aura.
Source foot anchor [719, 944]; exact two-sole midpoint [719.035, 942.5]; whole-canvas logical offset [2.759238522, 0.394176932]; native visible bounds [224, 57, 1511, 945].

### walk-b
Opposite contacts: front screen-right boot flat and weight-bearing, rear screen-left heel raised/toe down. Two distinct boots, same physical equipment hands and hip helmet.
Source foot anchor [784, 945]; exact two-sole midpoint [783.911, 935.0]; whole-canvas logical offset [-0.443449048, 0.344904815]; native visible bounds [316, 55, 1320, 946].

## Action contract
Existing idle/walk-a/attack/walk-b. c01.oath-bolt is ranged arcane magical damage; action visibly extends rune palm while preserving shortsword, no new action/effect asset.
The cast is conveyed by braced stance, extended open rune palm and focused face, with all rune brightness confined to the physical hand/forearm.

## Provenance and QA
All four are first successful native outputs. Idle also retained under attempt-01. Exact prompts, request metadata, original source references, raw PNGs, measured sole bands and black/white QA are preserved. Every master/raw/native hash agrees. Native perimeter alpha<=1 with no silhouette crossing; alpha1 specks retained unchanged. No runtime/repository edits.
