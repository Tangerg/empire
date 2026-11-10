# Archer authored victory cheer: art candidate v1

Six original painted poses generated with the built-in image generation tool using the official `unit-archer-v2.png` and accepted `archer-breathe-v1.png` as image references. Official artwork, accepted breathe/shot/recoil/fall atlases and Torren salute provenance/contact review were inspected before generation. This source review covers art and provenance only, not native runtime acceptance.

## Files

- `source-row.png`: unchanged transparent generated source (2172×724).
- `prompt-v1.txt`: full generation prompt.
- `../../godot/assets/painted-actions-v1/archer-cheer-v1.png`: six packed original-RGBA components (1934×486).
- `extraction-review.json`: source/atlas/component hashes, bounds, per-pose support anchors, mirrored anchors, common scale, timing.
- `native-size-contact.png`, `native-size-contact-3x.png`: accepted idle plus six frames, both facings, registered to the same terrain root. Diagnostics only; downsampling does not affect original atlas.
- `full-size-contact.png`: full-resolution isolated component review. Component tops are aligned here, not terrain roots; use native contact for registration.
- `anchored-cheer.gif`: nonlooping offline registration diagnostic with idle before and after, both facings. Not captured gameplay.
- `verify-extraction.py`, `verification/extraction-passed.txt`: independent original-pixel, padding and registration checks with successful one-channel mutation rejection.

## Review and recommendation

Use `archer-cheer-v1` / `archer.cheer` with `cheer-ready`, `cheer-lift`, `cheer-high`, `cheer-hold`, `cheer-lower`, `cheer-rest`. Intervals are 100/120/160/260/180/140 ms, total 960 ms, nonlooping. The 420 ms high/hold gesture and 180 ms recovery are readable for a restrained victorious soldier.

All six drawings inspected at source size and in both facing contact rows. Character identity, blue hood/tunic, off-white sleeves, gloves/bracers, strap/buckle, quiver, compact proportions and complete bow remain coherent. Left hand owns bow throughout; right fist is empty throughout. No nocked arrow or projectile appears. Bow is lowered modestly through a small rigid inclination in frames 2–4 and returns upright. Two boots remain grounded. Frame 3/4 fist is beside the shoulder with a slight lift between them; frame 4 smiles subtly. The fist visually overlaps the near edge of the quiver silhouette but contains no arrow and does not perform an arrow draw. These are independently drawn poses, not rotated/scaled copies.

Common presentation scale 0.0805 joins accepted breathing scale 0.072 without conspicuous body-size change in static native-size review. Per-pose support anchors and mirrored x = width - anchor x are recorded. Source has minor natural painted variation in boot contours and bow curvature; no per-frame correction, rescaling, rotation or repainting was performed. Native animation and idle handoff still need review by the integrator.

The extraction keeps significant alpha components >16/255 with >1000 pixels, plus original nonzero RGBA within 3 pixels of each. It removes 15,828 detached near-transparent pixels, maximum alpha 6/255. Every retained pixel is unchanged; four fully transparent pixels pad each component on all sides. The original source is retained intact. This candidate passed visual inspection without regeneration or patched anatomy.
