# Mirelle personal channel

Generated 2026-11-12 with the built-in image generator, using the shipped `painted-units-v1/mirelle-stage01-v2.png` as identity reference. A workspace/backup search found only her old four-pose gait sheet, not a personal casting candidate. The generic gravekeeper channel is deliberately not reused: its character identity is different.

## Direction and processing

Requested six distinct full-body articulated poses in a 3×2 equal-cell sheet: ready, gather, lantern raise, release, recover, rest. Fixed three-quarter-right camera; preserve brown hair, cream hood/apron, charcoal coat, satchel, gloves, boots and brass lantern. Planted feet, consistent scale, transparent margin, no projectile, particles, ground, text or painted shadows. A second generator pass requested background-only cleanup without changing character layout.

Following the requested sprite-gen extraction/curation method, alpha values at or below 8/255 were set to zero to remove faint background residue. Higher alpha and RGB were preserved; no anatomical pose was synthesized by code. Original generated sheets are retained separately in Library. Runtime uses the cleaned 1536×1024 RGBA sheet directly with explicit 512×512 rectangles, not guessed horizontal slicing. Six per-frame foot anchors align the slightly varying stance. Scale matches the original 0.0806452; the existing compositor remains the sole contact-shadow owner.

## Runtime contract

`mirelle.channel` uses 90/100/110/100/100/100 ms half-open pose intervals, 300 ms release, 600 ms total, and disables the generic source accent. Her attack and healing use the same declared action. Recipient impact timing, combat receipts and canonical state are unchanged. Idle, walking, hit, death and celebration remain their prior bindings; this slice does not claim those are now animated.

Native regression covers distinct extracted pictures/padding, intervals, left/right attack and healing, planted root, release synchronization, cancellation, reduced motion and state immutability. Visual acceptance is recorded separately from synthetic receipt tests. Real GUI uses a legal C12 replay checkpoint, not a claim of continuous campaign play. Capture readback is not a frame-rate benchmark.

## Native visual review

At 960×640, actual C12 attack and healing inputs were exercised after undo. The release raises the same brass lantern with an articulated free hand, the planted foot contact stays fixed, and recovery restores Mirelle’s original identity. Attack produced the predicted −19; healing produced +27 with the existing recipient aura. Raw PNG/JSON captures retain the visible pose sequence and original scale/contact values. These captures do not prove every theoretical frame was sampled, and their wall-clock timestamps are not clip timing assertions.
