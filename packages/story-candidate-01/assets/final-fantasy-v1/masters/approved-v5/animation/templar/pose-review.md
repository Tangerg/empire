# Templar four-pose review

C01-UNIT-TEMPLAR binds only c01.templar. Preserve original idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1 and uncovered gray-haired ivory/gold sword-and-shield identity. The shield has one long gold vertical bar, not a cross; the cross-bearing staff belongs to the distinct inquisitor row. Original equipment continuity and rule-to-art discrepancies are explicitly documented.

## Identity
Accepted uncovered silver-gray swept-back hair and short gray beard/moustache, older tan face, ivory/gold armored surcoat and steel shoulder/arm/hip/knee/leg/boot plates. Large bare sword hand screen-left; tall cream shield with one gold vertical bar screen-right. Broad matte chunky head/body/hands/boots.

## Geometry
Native idle/walk-a/attack are1585x992 and each require one transparent right column in export preparation; walk-b is1586x992. Common1586x992. No native resizing, cropping, painting, recoloring or alpha cleanup; full canvas preserved.
Locked44/944.5 body scale, canonical foot[753,973]. Proposal104x64 at origin[8,4] preserves all complete canvases. Attack requires measured limit8; no native resizing or registration-driven regeneration.

### idle
Accepted uncovered silver-gray swept-back hair and short gray beard/moustache, older tan face, ivory/gold armored surcoat and steel shoulder/arm/hip/knee/leg/boot plates. Large bare sword hand screen-left; tall cream shield with one gold vertical bar screen-right. Broad matte chunky head/body/hands/boots.
Source foot [753, 973]; two-sole midpoint [752.83097369807, 968.5]; whole-canvas logical offset [0.0, 0.0]; native visible bounds [335, 24, 1250, 974]; full logical canvas [8.0, 4.0, 81.884595, 50.212811].

### walk-a
Front screen-right steel boot advances toe-up with heel contact and visible sole; rear screen-left boot remains flat and weight-bearing. Same sword and shield remain in original physical hands. Small arm/surcoat motion; no shield disappearance.
Source foot [742, 970]; two-sole midpoint [741.7904893303901, 968.0]; whole-canvas logical offset [0.512440445, 0.139756485]; native visible bounds [270, 23, 1247, 971]; full logical canvas [8.51244, 4.139756, 82.397035, 50.352567].

### attack
Clear planted sword thrust with front bent knee and rear extended bracing leg. Original screen-left connected arm and bare fist extend the compact sword horizontally in front of the shield. Original shield arm remains naturally occluded behind its full shield. One sword, one shield, two arms/hands total; no limb duplication, shield penetration or hand swap. Selected correction restores about330source-pixel blade length, matching the accepted short-sword identity.
Source foot [594, 940]; two-sole midpoint [593.840357624561, 931.5]; whole-canvas logical offset [7.4070937, 1.537321334]; native visible bounds [126, 29, 1349, 941]; full logical canvas [15.407094, 5.537321, 89.291689, 51.750132].

### walk-b
Opposite walking contact: front screen-right steel boot is flat and weight-bearing, rear screen-left heel is raised with toe contact. Same camera and sword/shield hands; not mirrored or repeated walk-a.
Source foot [794, 964]; two-sole midpoint [793.5927409102467, 956.5]; whole-canvas logical offset [-1.910005294, 0.419269455]; native visible bounds [320, 24, 1256, 965]; full logical canvas [6.089995, 4.419269, 79.97459, 50.63208].

## Source and rule discrepancies
Original frame equipment continuity: Visual continuity repaired with coordinator approval. Original board and HD idle/attack carry sword and shield; original walk-a/walk-b remove shield and introduce a second sword. Remake consistently retains the approved idle sword-and-shield identity in all four poses.

Rule weapon versus original illustration: Existing mismatch retained and documented, not a gameplay fix. c01.templar rules list c01.legion-spear and c01.inquisitor-flame, while canonical artwork shows an uncovered gray-haired sword-and-shield veteran. No spear, new symbol or flame is invented, no topic remap occurs and no weapon rules are changed.

Single action slot: Existing shared-action limitation retained. One original attack slot serves both rule weapons. The source-faithful sword thrust remains the shared illustration for those actions. No fifth pose or weapon-specific animation dispatch is introduced.

## Provenance and QA
First native attack made the blade about490source pixels long versus idle about315. Preserved attack/attempt-01. Selected native attempt-02 restores about330source-pixel compact blade while retaining attack body/lunge/shield. Exact edit-call input path snapshot is preserved and hashed in request.json because the selected root master later advances to attempt02.
Idle and both walks are their first successful native outputs. Both attack attempts retain native/raw, exact prompt/request and alpha QA. Selected raw/master/native and prompt hashes all agree; edit input snapshots also verified. Native and white-background views inspected for every pose; black/white QA saved. Native alpha=1 specks retained. No repository/runtime writes.
