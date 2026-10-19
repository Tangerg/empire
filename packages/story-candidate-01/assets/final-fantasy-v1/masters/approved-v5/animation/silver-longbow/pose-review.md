# Silver-longbow four-pose review

C01-UNIT-SILVER-LONGBOW binds only c01.silver-longbow; four c01-15 enemy actors controlled-longbow-1[14,5], controlled-longbow-2[14,9], north-longbow[8,2], south-longbow[8,13]. Original idle/walk-a/attack/walk-b,32x48 frame,anchor[16,47],footprint1x1 remain. Sole c01.longbow is range2-3 direct-line-of-sight piercing ranged weapon. Original third cell was near-idle; reconstructed attack shows one genuinely nocked arrow and two-segment drawn string. Original bow-holding arm remains forward/right; original empty arm draws at cheek. Metal arrowhead is right/forward, rear feathers/nock near left draw hand, shaft above bow grip. Two opposite walking contacts reconstructed. No rule/fifth-frame change; independent C01-FX-03 projectile/trail stays outside sprite.

## Identity
Tan adult pointed-ear elf-like forest guard, short charcoal/black tousled hair, stern dark eyes/brows, clean-shaven, small metal hoop on visible screen-left ear. Dark gray hood down, sage/olive-green chest-laced split tunic, brown broad rectangular leather shoulder plates, brown diagonal strap/belt/bracers with crossed tan bindings, fingerless protection/exposed tan fingers, cream/tan furry sleeve and upper-boot trim, brown trousers and boots. Brown back quiver above screen-left shoulder with pale feather fletching; woven tan left-hip pouch with dark blue tie. Original screen-right hand holds one tall pale wooden longbow at a brown wrapped grip, with tan/brown tip bindings and one dark string joining two complete tips; screen-left hand is free in idle/walks. No nocked loose arrow in idle. Quiver upper ends are feather fletching; points hidden below.
No distinct original sacred/faction emblem. Preserve crossed garment lacing, fur, blue pouch tie and simple original gear, without invented rune, crest or antlers.

## Geometry
All four selected native canvases1585x992; no transparent padding or source resampling required.
Maximum translation is attack foot[756,967] relative to idle[800,956]: source delta[44,-11] gives[2.315789474,-0.578947368]logical at1/19. All complete native canvases fit104x64 at origin[8,4],limit4; no independent body/bow fitting.
Shared44/836; canonical sourcefoot[800,956].

### idle
Selected corrected idle attempt02. Original short dark hair/pointed ears/ear hoop, hood-down green/brown armor and pale longbow retained. Original screen-right bow hand; screen-left hand free. Complete top and bottom bow tips, one taut string, quiver feather ends. Both feet flat and no nocked arrow. Native and white QA inspected. Perimeter gate passes with zero edge alpha.
Sourcefoot [800, 956]; exact sole midpoint [800.0834915257537, 956.0]; whole-canvas offset [0.0, 0.0]; source visible bounds [505, 12, 1270, 957]; full logical canvas [8.0, 4.0, 91.421053, 56.210526].

### walk-a
Walk-a clearly right support/down and left heel raised recovery; original screen-right bow hand and free screen-left hand retained. One upright pale longbow and single taut string; all arrows in quiver. Lower bow end adjacent to foreboot with small natural foreground occlusion, no canvas clipping. Native and white QA inspected.
Sourcefoot [809, 955]; exact sole midpoint [808.5228297517976, 939.5]; whole-canvas offset [-0.473684211, 0.052631579]; source visible bounds [492, 15, 1245, 956]; full logical canvas [7.526316, 4.052632, 90.947368, 56.263158].

### attack
Single nocked arrow drawn toward cheek by original free screen-left arm; original bow arm extends right. Gray metal arrowhead faces right, pale rear fletching adjacent to draw hand, single shaft above bow grip. One V-string connects both actual tip bindings to draw/nock hand. Two arms/hands only; no second relaxed string, floating arrow or independent FX. Bow naturally flexed and visible tip separation reduced; preserve native pose, no independent scaling. Coordinator native review requested for draw anatomy and longbow proportions.
Sourcefoot [756, 967]; exact sole midpoint [755.9897168803418, 962.0]; whole-canvas offset [2.315789474, -0.578947368]; source visible bounds [394, 10, 1379, 968]; full logical canvas [10.315789, 3.421053, 93.736842, 55.631579].

### walk-b
Clear opposite contact to walk-a: left boot down flat and right boot raised with visible sole. Original bow arm remains on right; empty left hand by hip pouch. Long pale bow/string and both actual tips retained, partial lower-limb overlap with raised boot is natural foreground occlusion, not clipping. Arrows remain in back quiver, no nocked arrow. Native and white QA inspected.
Sourcefoot [787, 962]; exact sole midpoint [786.877886964246, 951.0]; whole-canvas offset [0.684210526, -0.315789474]; source visible bounds [498, 11, 1246, 963]; full logical canvas [8.684211, 3.684211, 92.105263, 55.894737].

## Bow review
All full native sources retain a longbow rather than short-bow redesign; no independent scale operation. Diagnostic visible tip separation is approximately925idle,877walk-a,815attack,883walk-b. Draw flex and pose alter projection, and walking foreground boots partly overlap lower limbs; these measurements do not establish exact physical-length invariance. Coordinator native review specifically requested before binding.
Attack arrowhead/fletching/shaft/nock/string/grip direction checked on native and white QA. One front-right metal head, rear fletching by draw hand, one shaft across above bow grip, two string segments meeting at draw/nock hand. No second relaxed string or separate projectile.

## Provenance and QA
Idle selects corrected attempt02; initial perimeter-failing attempt01 retained. Other poses use first native output. Complete original1672x941 board, original strips and unresampled reference crops retained with hashes. Exact call prompts, raw/master/native hashes all verified. Native/white views inspected, black/white composites saved. No repository writes.
