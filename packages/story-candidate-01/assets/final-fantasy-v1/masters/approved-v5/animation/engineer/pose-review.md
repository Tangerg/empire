# Engineer four-pose review

C01-UNIT-ENGINEER maps to c01.tasha and c01.engineer. Original source board and HD/runtime strips are preserved under references/.

## Source identity
Brown hair with low rear bun and loose side locks, stern clean-shaven adult face, rounded brown leather work cap with rolled short brim and ear flaps. Ochre apron over dark teal vest and cream rolled sleeves, large brown gloves, broad gray/tan trouser cuffs and huge brown boots. Screen-right glove holds short wood-shaft double-faced steel hammer; screen-left glove holds tan rope coil. Steel double-ended wrench hangs from front belt; brown pouch at right hip. Gray spade shovel and wood shaft stay strapped behind left shoulder.

## Geometry
Fixed44/915 body scale from accepted idle cap crown47 to mean soles962. Every native canvas is1586x992, requiring no padding. All whole canvases fit104x64 with origin8,4. No native master editing or independent silhouette fitting.

### idle
Accepted leather-capped engineer with large adult head, gloves and boots; ochre apron, teal vest, cream sleeves, rope coil, front wrench, hip pouch and back shovel. Hammer in screen-right glove, rope in screen-left glove.
Source foot anchor [819, 964]; exact two-sole midpoint [818.856, 962.0]; whole-canvas logical offset [0.0, 0.0]; native visible bounds [423, 47, 1274, 965].

### walk-a
Front screen-right boot is toe-up with heel contact and dark sole visible; rear screen-left boot is flat. All tools remain attached and in the original physical hands. Broad matte face/apron planes retained.
Source foot anchor [784, 964]; exact two-sole midpoint [784.118, 962.5]; whole-canvas logical offset [1.683060109, 0.0]; native visible bounds [375, 28, 1289, 965].

### attack
Clear forward hammer strike with the screen-right arm extended and short double-faced hammer angled forward. Screen-left glove retains rope coil; back shovel, belt wrench and hip pouch remain intact. Braced stance and focused expression; no external glow or effects.
Source foot anchor [678, 961]; exact two-sole midpoint [677.663, 958.0]; whole-canvas logical offset [6.780327869, 0.144262295]; native visible bounds [194, 20, 1550, 962].

### walk-b
Opposite contacts: front screen-right boot is flat and weight-bearing; rear screen-left heel is raised with toe down. Tool hands, back shovel, belt wrench and pouch retained.
Source foot anchor [850, 958]; exact two-sole midpoint [850.33, 955.0]; whole-canvas logical offset [-1.490710383, 0.28852459]; native visible bounds [395, 37, 1323, 959].

## Action contract
Existing idle/walk-a/attack/walk-b. Engineer has c01.engineer-hammer blunt melee and c01.satchel-charge ranged siege area attack. The shared attack slot visibly strikes with the hammer; no fifth frame or bomb effect is invented.
The hammer action retains the original physical tool hands and all attached role gear.

## Provenance and QA
All four selected masters are first successful native outputs. Idle is also retained under attempt-01. Exact prompts, request metadata, original source references, raw PNGs, measured sole bands and black/white QA are preserved. Every master/raw/native hash agrees. Native perimeter alpha<=1 with no silhouette crossing; alpha1 specks retained unchanged. No runtime/repository edits.
