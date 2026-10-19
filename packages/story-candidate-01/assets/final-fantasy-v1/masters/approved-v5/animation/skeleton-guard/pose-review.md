# Skeleton-guard four-pose review

C01-UNIT-SKELETON-GUARD binds only c01.skeleton-guard, with eleven initial enemy actors across c01-03, c01-11, c01-12 and c01-16. Retain original idle/walk-a/attack/walk-b slots,32x48 original frame,anchor[16,47],footprint[1,1]. Original third cell was near idle and both original walks had similar contact. Reconstructed opposite walks and an original-left-hand controlled spear sweep; original-right-hand plain wooden shield retained in all poses. Existing rule weapon c01.undead-blade/回声残刃 uses slash while original illustration consistently depicts spear/shield. No sword invented, rule remap, fifth frame or gameplay fix claimed. Independent C01-FX-01 remains separate.

## Identity and symbols
True ivory-bone skeleton with exposed skull and jaw, two dark eye sockets containing small amber points, triangular nose cavity, round low steel helmet with ridge bands and brown chin strap; blue-gray scarf, ragged charcoal/slate armor and tunic, pale torn shoulder scraps, brown belt/pouch/rope; two wooden chest name tags and a small belt-side wooden token; exposed knee/shin bones and brown open-toe wrapped footwear. Original screen-left hand grips one wood-shaft spear with angular steel head, tan neck bindings and gray butt cap. Original screen-right arm holds one plain pale wooden tall rectangular shield with steel rim and rivets. Shield has no cross, stripe or heraldry.
Original two name plaques have simple dark holes/marks. Idle simplifies these into small diamond-like dark insets, not a new faction/rune symbol. Amber eye centers confined inside skull sockets. Skeletal negative spaces must stay genuine transparent alpha.

## Geometry
Native idle/walk-a/attack1586x992; walk-b1585x992. Preserve all native files; only1transparent right column is needed for walk-b common-source export. No resizing/cropping/alpha cleanup.
Shared44/860;canonical sourcefoot[843,979].
Attack sourcefoot[704,949] against idle[843,979] gives source delta[139,30],whole-canvas offset[7.111627907,1.534883721]logical. All padded complete canvases fit104x64 at origin[8,4],explicit limit8. No independent fit.

### idle
Original skeleton species, round steel helmet/chin strap, blue-gray scarf, ragged slate armor, two wooden chest name tags, left spear/right plain rectangular shield, exposed knee/toe bones retained. Eye amber confined to dark sockets. Chest tag dark slots simplified as small diamond-like insets, not new heraldic symbols; small belt-side wooden token is source-derived. Native and white QA inspected; transparent leg/arm gaps, full spear and shield.
Sourcefoot [843, 979]; exact sole midpoint [842.8214285714286, 974.0]; full-canvas offset [0.0, 0.0]; visible source bounds [393, 23, 1198, 980]; full logical canvas [8.0, 4.0, 89.144186, 54.753488].

### walk-a
Clear screen-right forward planted contact and screen-left rear boot lifted with bent bone knee. Same skull/helmet scale, slate/scarf costume, two chest tags, left spear/right plain shield. Native and white QA inspected; no external eye halo. Genuine transparent leg/arm gaps. Entire steel point, butt, shield and bone toes fit native canvas.
Sourcefoot [834, 967]; exact sole midpoint [833.5235628041089, 933.5]; full-canvas offset [0.460465116, 0.613953488]; visible source bounds [417, 20, 1213, 968]; full logical canvas [8.460465, 4.613953, 89.604651, 55.367442].

### attack
Original left hand sweeps spear diagonally right across upper body; right arm retains plain wooden shield. Clear braced stance, exactly two arms/hands and one continuous gripped spear. Current corrected candidate reduces excessive initial spear span; actual projected span remains approximately1130px versus880idle and needs shared-scale coordinator review. Native and white views inspected, eye amber confined, complete tips/feet and transparent leg gap.
Sourcefoot [704, 949]; exact sole midpoint [704.4014266586638, 947.0]; full-canvas offset [7.111627907, 1.534883721]; visible source bounds [309, 57, 1435, 950]; full logical canvas [15.111628, 5.534884, 96.255814, 56.288372].

### walk-b
Clear screen-left planted supporting boot and screen-right raised rear boot, genuinely opposite walk-a contact. Same skull/helmet, two chest tags, source-left spear/source-right plain shield. Native and white views inspected; entire spear and shield fit, amber bounded in sockets, no external aura. Unobstructed skeletal gap has true alpha. Native1585x992 needs only1transparent right column in common1586x992; perimeter retains native alpha1 specks, with0pixels above1.
Sourcefoot [855, 976]; exact sole midpoint [854.6615868408321, 945.0]; full-canvas offset [-0.613953488, 0.153488372]; visible source bounds [393, 22, 1207, 977]; full logical canvas [7.386047, 4.153488, 88.530233, 54.906977].

## Pending native continuity gate
Attack candidate approximate projected spear span1134px versus875idle,865walk-a,877walk-b after an imagegen-only correction of excessive first attack. Projected2Dspan is diagnostic, not proof of identical3Dlength. No independent source/weapon fit was performed. Coordinator shared-scale native sequence review must judge continuity before formal binding.
Do not call exact physical spear length invariant from these2Dimages. Coordinator explicitly requested retaining corrected attack until native shared-scale quartet review. No further regeneration performed.

## Provenance and alpha
Attack selects native attempt02; excessive-length attempt01 preserved with exact prompt/raw/QA. Other poses first native successes. Original complete board/strips and reference crops saved. Raw/master/native hashes and exact prompt hashes verified. Native and white views inspected; black and white composites saved. All4leg-gap7x7probes fully transparent; eye glow confined to dark sockets. All native perimeter alpha>1 counts zero. No repository writes.
