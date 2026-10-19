# Border farmer

Original older woman, headscarf, seed pouch and hoe retained. Shared 44/939 full-canvas scale with measured boot registration and 2px transparent padding. Native opposite walks and noncombat pouch-check action verified.

Actual consumers are two c01-01 ambient idle-only scene placements at 11.5/8.65 scale 0.7 and 16.4/9.15 scale 0.68 flipped. No domain actor binding or gameplay occupancy change.

Complete offline BoardView scene review is recorded in formal-scene: both placements changed, the other 250 named pieces remain exact, and actor, building, standing-order and state contracts are unchanged. The flipped placement exposed an existing canvas-centre mirror error with non-centred anchors; the renderer now mirrors about the registered contact. A regression test failed before the fix and passes after it. Actual feet remain at world [368,308.8] and [524.8,324.8]. The prior misregistered scene remains preserved as evidence.

Existing village prop occlusion is still visible; this is not a claim that every ambient actor is unobstructed. Browser/live animation playback remains unverified. The complete capture uses the actual offline renderer, not a generated concept background.
