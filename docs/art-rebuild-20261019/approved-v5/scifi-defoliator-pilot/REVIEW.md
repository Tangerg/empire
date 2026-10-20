# Defoliator pilot quartet candidate

Four independent built-in native RGBA sources now exist: idle, walk-a, attack, walk-b. No shared catalog/runtime edits. This is an isolated catalog candidate, not a bound animation or scene-ready claim. The earlier raw-only idle stop was explicitly superseded by coordinator direction to continue from valid complete v3 without enforcing zero baseline clearance. All earlier evidence remains unchanged.

## Source and semantics

Original full combat-board-08 row4 and canonical runtime quartet were inspected. One black-helmet orange-visored pilot sits inside the gray curved-canopy industrial biped mech. Exactly two articulated mechanical legs, claw-toed feet, small front pincer and one viewer-right circular saw/nozzle assembly remain. Attack retains the original orange cutting spray action, not the unrelated green-vial equipment card.

Idle v3 restores full claws after rejected v2 clipping. Walk-a plants the near leg forward with the far leg raised/back. Walk-b reverses those contacts, bending the near leg up/back and planting the far leg forward. This is a visible gait change, not a mirror or a repeated stride. Attack raises the tool/nozzle with contained orange spray. Four native sources preserve the same cockpit/body design and broadly matte chunky treatment; no extra anatomy or deterministic pixel editing was used.

## Packing and native review

Unchanged export_frame_overrides.plan passes: one canonical256x64 sheet with64x64 frames, anchor[32,63], footprint[2,2]. All sources1254x1254. Shared whole-canvas width52, origin[8,10]; offsets idle[0,1], walk-a[0,2], attack[1,2], walk-b[-2,2]. Every full canvas fits. All source/native alpha checks pass, raw/master bytes equal, decoded sources and rendered poses differ. No HD contract invented. Native64 white/black/olive views visually inspected; canopy, saw, two feet, opposite gait and orange attack remain distinguishable.

## Shared helper contact: scene-quality HOLD

renderer-contact-proof.ts calls actual runtimeUnitPicture and boardStripMarkup at logical scale1, with canonical anchor unchanged. SVG was rasterized with Inkscape and its known explicit outer viewport clip, matching the prior proof. Sources are never cropped. This is a neutral shared-helper diagnostic, not an actual C02 scene or live gameplay consumer.

Measured support points and per-foot ROIs are in measured-support.json. Lowest opaque planted soles vary idle1187, walk-a1115, attack1098, walk-b1157. Thus the projected minimum supportY ranges57.53–60.22, leaving2.78–5.47px clearance from canonical anchor63. Walk-b's actual planted toe travels forward to projectedX43.88; the contact point is not a body/torso registration marker. Do not erase this travel or claim subpixel contact stability.

Although aggregate support centroids can overlap the central shadow ellipse, individual feet do not necessarily overlap. Visual helper inspection especially shows attack elevated above its shadow, and far feet can miss the generic central ellipse. This remains a real scene-contact quality hold, not resolved by successful catalog guards or centroid arithmetic. No further baseline retries, scale-per-pose compensation, cropping, or guard relaxation were used. Coordinator decides any catalog registration separately; scene-ready status is not earned here.

Reproduce: python docs/art-rebuild-20261019/approved-v5/scifi-defoliator-pilot/measure_support.py, then verify_bundle.py in the same directory. Helper proof: node_modules/.bin/vite-node docs/art-rebuild-20261019/approved-v5/scifi-defoliator-pilot/renderer-contact-proof.ts, then Inkscape rasterization.
