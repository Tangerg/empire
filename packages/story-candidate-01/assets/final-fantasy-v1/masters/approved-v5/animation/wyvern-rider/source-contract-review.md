# Wyvern rider source contract

Canonical C01-UNIT-WYVERN-RIDER exists in manifest-final-fantasy-v1 and scope-ledger, with four64x64frames, frame order idle/walk-a/attack/walk-b, anchor[32,63] and art footprint[2,2]. It was absent from progress.topicStages at assignment; coordinator confirmed this means remaining/unbound. C01-EQUIP-WYVERN-RIDER is separate and does not complete this combat identity.

Original source is third row of native1254x1254 combat-atlas-colossus-ivra-wyvern-dragon.png. Preserve one adult human rider with tan skin, brown short ponytail, bare face and ordinary human ears; blue-gray scarf and undercloth, dark brown leather armor/straps/bracers/boots. Near screen-left hand holds one short spear with torn blue-gray pennant and tiny red patch; forward screen-right hand holds rope reins. Brown saddle/roll and ragged ochre pad with blue-gray side flaps. Original mount is gray-olive, wingless, and four-legged with amber eyes, long angular snout, swept-back head spikes, broad neck plates and tapering upcurled tail. Do not invent conventional wyvern wings or biped anatomy.

No actual unit definition, art binding or deployed actor consumes this topic. docs/story-candidates/candidate-01/README.md:671 describes a recruitable flying troop weaker than a true dragon, but does not implement a rule or override original grounded poses. All poses retain grounded pacing. Spear attack is a clear art-semantic motion of its already-visible weapon, not a gameplay change.

Comparable inspected approved sources: white-stag-rider54/909.5, eagle-scout52/944, ancient-dragon64/940. All are full native1586x992-class canvases with no body-part fitting. Proposed wyvern idle54/880.5 is rider hair y92 to mean near paw soles y972.5. Four bounded paw measurements yield source support[900,981], with the raised forepaw treated as stance projection in x and lowest planted claw as y. Rider boot does not drive ground registration. All poses will retain one exact shared scale and only bounded full-canvas translation from measured support.

Idle generated with built-in imagegen, true RGBA, raw/master byte-identical. Perimeter max alpha1 and no visible haze on light/dark/olive. Whole native1586x992canvas retained. Native identity and family-size comparison sent to coordinator; remaining poses wait for gate. No repository writes or Git operations performed. No live-game completion claimed.

## Coordinator idle gate
At2026-10-20 00:03UTC, coordinator approved grounded identity, exact54/880.5 shared scale, source support[900,981], frame128x80/origin[14,6]. Opposite grounded steps and source-consistent spear action authorized. No wings or flight.

## Final coordinator gate
At2026-10-20 00:13UTC, coordinator approved final quartet, opposite grounded phases and correction03 short-spear projection. Actual planted-contact centroids are recorded separately from calibrated pivots. Preserve idle pivot[900,981], use one constant xcalibration63.4449735449735source pixels for all poses, and derive offsets from the measured planted-contact deltas. Lifted paws are excluded from contact sets. Maximum5.381879181344607logical translation accepted under limit6; all native canvases fit128x80 at exact54/880.5, with1px transparent right padding only for walk-a/b. No source mutation, alpha cleanup, individual fitting, rule change, invented consumer or live-game claim.
