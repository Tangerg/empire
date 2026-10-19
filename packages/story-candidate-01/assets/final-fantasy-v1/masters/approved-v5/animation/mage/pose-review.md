# Orb-staff mage four-pose review

C01-UNIT-MAGE maps to the built-in mage. It is distinct from campaign c01.battle-mage / C01-UNIT-BATTLE-MAGE. Original native atlas/HD strip and native source crops are preserved under references/.

## Identity
Mature clean-shaven face, full gray curls, plum coat with broad cream trim, teal scarf, blue-gray tunic, crossbody belt and satchel. Screen-left hand retains closed book; screen-right hand retains crooked wooden staff and pale orb. No swapped equipment, invented weapons, external glow or microtexture.

## Geometry
Common body scale44/897.5 from accepted idle hair crown54 to mean soles951.5. Idle/walks are1586x992; native attack1585x992 requires one transparent right column in export, with no resizing. All registered full canvases fit104x64 at proposed origin8,4.

### idle
Accepted chunky gray-curled mage, broad plum coat, huge hands/boots, upright orb staff in screen-right hand and closed book in screen-left hand.
Source foot: [787, 956]; exact two-sole midpoint: [787.37, 951.5]; logical canvas offset: [0.0, 0.0]; native visible bounds: [390, 54, 1295, 957].

### walk-a
Forward screen-right boot toe-up/heel-contact with visible dark sole; rear screen-left boot planted flat. Staff butt remains visible behind the lifted front boot; equipment hand assignment unchanged.
Source foot: [746, 955]; exact two-sole midpoint: [745.515, 948.0]; logical canvas offset: [2.010027855, 0.04902507]; native visible bounds: [370, 55, 1325, 956].

### attack
Clear staff-forward arcane casting stance, extended screen-right staff arm, screen-left hand holding closed book to chest, broad planted boots and focused face. Pale facets stay inside orb, no external glow. Lower staff angles back toward the front boot and naturally meets/overlaps coat/boot at its butt; nothing is clipped by canvas.
Source foot: [592, 940]; exact two-sole midpoint: [592.035, 936.5]; logical canvas offset: [9.559888579, 0.784401114]; native visible bounds: [143, 47, 1518, 941].

### walk-b
Opposite contacts: front screen-right boot planted flat and rear screen-left heel raised/toe down; both boots read clearly. Staff and closed book remain in original hands.
Source foot: [811, 945]; exact two-sole midpoint: [810.869, 937.5]; logical canvas offset: [-1.176601671, 0.539275766]; native visible bounds: [375, 56, 1311, 946].

## Action semantics
Existing idle/walk-a/attack/walk-b order. Attack visibly casts using the orb staff, matching ranged arcane mage weapons; no extra action frame or external effect asset.
The engine has mage_bolt, mage_overcharge and mage_meteor. The original source board action was only weakly differentiated; the remake makes the existing action slot readable through a braced forward staff cast rather than new gameplay or extra frames.

## Provenance and QA
All four frames are first successful native outputs, with idle also retained under attempt-01. Exact prompt text, request metadata, raw PNGs, original sources, measured sole bands and black/white QA are retained. Every master/raw/native SHA256 agrees. No silhouette touches native perimeter; alpha1 specks remain unchanged. No runtime/repository edits.
