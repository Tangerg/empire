# Targeted material cleanup (2026-10-27)

Only the battle mage, banner guard and Bran stage 01 sheets were edited using imagegen, from their existing four-frame painted sources. Prompts and source-registration/alpha measurements are alongside this file. No full-scene blur, new combat effects or changes to rules.

Mage fire is a connected silhouette rather than a particle halo; banner mail uses grouped steel planes rather than white rings; Bran cloth uses larger olive planes. Existing dimensions and native world scale remain unchanged. Each frame is independently registered to its prior body/foot placement; changes in `selected-assets.json` are image references, frame bounds where required, and corresponding anchors. Both walking support phases remain separate.

Validation: 12 registered frames have valid alpha bounds and anchors; current AnimationChecks passes 183 assertions. Actual C07 render inspected at 100% and initial 125% camera zoom, also at 960x640. Banner guard committed movement was captured with frame/position/support-plane observations. Source alpha noise is reduced strongly for mage/Bran; the banner's small isolated-alpha count increased (8 to 75), so no claim that every alpha metric improved. At native size its steel highlights are materially less speckled. No claim of manually playing all three attack animations in this pass.

Native evidence remains outside the repository at `empire-final-evidence/ui-cohesion-20261027/`, including `units100.png`, `units125.png`, `selected960.png`, and `third1180/motion-20261027-174210-001/`. C07 original evidence/save files are unchanged.
