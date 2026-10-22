# Flat oath-control node candidates

Generated on 2026-10-22 with the built-in image generation tool. These are original, unedited RGBA PNG outputs copied into this directory. No offline cropping, recoloring, resizing or alpha modification was performed. They are asset candidates, not game screenshots or accepted runtime integrations.

## Prompt sequence

1. Active: one low, flat, walkable octagonal warm-grey stone control floor plate, thin bevel, recessed central engraved gold-ochre runes with restrained illumination, outlined hand-painted medieval fantasy style. Style reference: `godot/assets/runtime-hd/narrative-static/arch-redstone.png`, materials only, not its tower composition. Overhead strategy-game view, approximately two-to-one visible screen footprint, transparent margins, no character, text, faction flag, tall crystal, surrounding ground or external halo.
2. Disabled: edit the active original; turn all emissive runes off, dark engraved grooves with non-emissive aged brass edges; preserve intact healthy stone, silhouette, camera and framing. This is intact disabled, not destroyed.
3. Damaged: edit the active original; add readable cracks and small chips within the original footprint; maintain walkable low plate and illuminated but interrupted runes. No rubble outside the rim or new tall elements.
4. Destroyed: edit the disabled original; fracture inner slabs and chip rim inward, visible flat broken center star, all runes off, all low debris within original octagonal footprint. No new objects or illumination.

All four prompts requested exact frame/silhouette consistency and true transparent backgrounds. Actual outputs must be measured rather than assuming those requests succeeded: active is 1729×910, other states 1728×910, and the destroyed variant is visibly larger in raw source pixels. `alpha-inspection.json` records original hashes and bounds at multiple alpha thresholds. Very low-alpha stray pixels exist outside the main visible shape. Runtime region, scale and anchor registration may express the common physical footprint; it must be visually verified and must not hide meaningful silhouette changes. If registration cannot preserve the design honestly, request another image-generation edit instead of modifying pixels offline.

## Original output mapping

- node-active.png: generated_images/exec-9da3f872-6476-4abd-a3b0-ad0c8352cdd6.png
- node-disabled.png: generated_images/exec-934d6819-ff5c-4520-89b3-07c7a770304f.png
- node-damaged.png: generated_images/exec-ccf8dcdc-e7a7-494e-9d0a-5dab6e45ddc4.png
- node-destroyed.png: generated_images/exec-46ac1a6f-e0c3-434a-8c69-b18a47f157a4.png

Captured may explicitly reuse the active physical bitmap. Ownership remains canonical runtime data, not a color baked into the artwork. At generation time, surface rendering and same-cell actor ordering were unverified. The subsequent native integration and real-display checks are recorded in ../../FLAT-NODE-ART.md.
