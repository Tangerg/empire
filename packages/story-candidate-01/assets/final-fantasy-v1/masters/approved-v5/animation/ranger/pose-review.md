# Ranger four-pose review

C01-UNIT-RANGER is protagonist c01.bran combat-unit art. Original full board, first-row/idle source crops and HD/runtime strips are preserved in references/. Equipment and portrait art were not substituted.

## Identity
Accepted clean-shaven brown-haired adult human in raised charcoal hood/cowl, blue-gray split tunic, olive-gray shoulder caps, brown gloves/bracers/boots, brass buckle, diagonal chest strap and hip quiver/scabbard. Original screen-left hand carries short blade; original screen-right hand holds short wooden bow.

## Geometry
Locked body scale44/873.5 from accepted idle hood crown75 to mean soles948.5. Canonical source foot[813,949]. Common canvas1586x993 requires only explicit maximum2-column/right and1-row/bottom transparent padding in export preparation. All complete registered canvases fit104x64 with origin[8,4]. No native image editing or independent silhouette fitting.

### idle
Accepted clean-shaven brown-haired adult human in raised charcoal hood/cowl, blue-gray split tunic, olive-gray shoulder caps, brown gloves/bracers/boots, brass buckle, diagonal chest strap and hip quiver/scabbard. Original screen-left hand carries short blade; original screen-right hand holds short wooden bow.
Source foot anchor [813, 949]; exact two-sole midpoint [813.367, 948.5]; logical whole-canvas offset [0.0, 0.0]; native visible bounds [408, 75, 1233, 950].

### walk-a
Front screen-right boot advances toe-up with heel contact and visible sole; rear screen-left boot is flat and weight-bearing. Same original blade/bow hands and full costume, quiver and scabbard.
Source foot anchor [799, 957]; exact two-sole midpoint [798.699, 952.0]; logical whole-canvas offset [0.70520893, -0.402976531]; native visible bounds [388, 67, 1239, 958].

### attack
Clear braced full-draw bow shot. Short blade is completely sheathed at original screen-left hip, with only hilt/guard/pommel visible. Original bow hand extends the compact short bow; original blade hand draws the string near the cheek. Exactly two connected gloved hands and one nocked arrow with one shaft, one steel head and one fletching group. V string connects bow tips to drawing fingers, partly occluded by the face. No hand holds a blade while drawing.
Source foot anchor [812, 958]; exact two-sole midpoint [811.574, 957.0]; logical whole-canvas offset [0.050372066, -0.453348598]; native visible bounds [419, 77, 1389, 959].

### walk-b
Opposite contact: front screen-right boot flat and weight-bearing; rear screen-left heel raised with toe contact. Same chunky head/body, original weapon hands, short blade, bow, quiver and scabbard.
Source foot anchor [844, 934]; exact two-sole midpoint [844.443, 927.5]; logical whole-canvas offset [-1.561534058, 0.755580996]; native visible bounds [408, 71, 1252, 935].

## Attack review and existing limitation
First attack had an over-tall bow. It is preserved in attack/attempt-01 with exact prompt/raw/QA. Selected native attempt-02 restores compact original short-bow size while retaining the human stance, sheathed blade and credible single-arrow draw. No programmatic image editing was used.
c01.bran has c01.ranger-bow and c01.border-blade, but the original four-frame strip supplies one attack slot. The accepted primary-bow shooting pose is shared by both weapon actions. No extra slot, gameplay change or weapon-specific animation dispatch is introduced or claimed.
Idle and walks retain the carried short blade and bow in their original physical hands. Attack temporarily sheathes the blade to free its hand for drawing, while preserving the original bow hand.
The selected attack has a single held nocked arrow, connected string, correct original bow hand and a safely sheathed blade. The upper string is partly occluded by the face, rather than disconnected. No extra projectile or effects image is introduced.

## Provenance and QA
Idle and both walks are their first successful native outputs; selected attack is native attempt-02. Both attack attempts retain raw PNGs, exact prompts, requests and alpha QA. All selected native/master/raw hashes and prompt hashes agree. Native alpha=1 specks are retained, with no silhouette alpha>1 on source edges. Black/white QA and measured sole-band ROIs are saved. No runtime or repository writes.
