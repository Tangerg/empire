# C01 miniature native-pixel role prototypes, v2

Original pixel constructions at 64 × 64, not downsampled paintings, franchise assets or archived character studies. Rebuild with `python draw_roles.py`, verify with `python test_roles.py` (Pillow). No resizing produces runtime textures. Preview scaling is nearest-neighbor only.

## Exact scope

The current C01 level has 19 units across **9 unit types**, not 19 professions. `manifest.json.unitRoles` covers every type explicitly:

- Laiya: gray flag + sword, brown-haired flag-bearer.
- Swordsman/Torren: short sword and round shield; no flag substitution.
- Archer: hood, visible bow gap, back quiver.
- Banner-guard: upright long spear; not a captain or another shield.
- Gravekeeper: long robe and lantern staff, no generic shield.
- Knight: mounted lancer; Roderick: mounted captain with crest and red cape.
- Legion-shield: pale long shield; Cain: crest and gold command trim.

Current `content-packs.json` explicitly marks knight and Roderick as mounted/cavalry. No movement class was modified to fit the art. Their horse silhouettes now match the **current** canonical roles, despite older notes that described the knight as foot infantry.

## Texture contract

Only `role-views.png` is shipped for idle poses; unused single-frame `role.png` copies were removed after verifying the C# consumer reads `viewsTexture`. Each view texture is **128 × 64**, front-right idle at `[0,0,64,64]`, independently authored back-left idle at `[64,0,64,64]`. Both have local anchor `(32,56)`. Sprite3D uses nearest and frame-region selection; do not interpret the whole pair as one figure. Manifest v2 names the view texture and regions; there is no unused single-frame texture field.

Back views are separately constructed with nape/helmet back, rear straps/quiver and reordered equipment, **not reflected front images**. These are two oblique views, not all four/eight compass directions. Lighting and small weapon-hand details remain art-review work; the mechanical no-mirror test is not an anatomical quality guarantee. Canonical unit state, team, facing and movement remain owned by the gameplay/view consumer, not the texture generator.

All 18 frames use alpha 0/255 only, transparent perimeter and bottom contact pixel y56. Infantry crown-to-sole is around47px, raised flag/spear and mounted lance crest increase total bounds while staying in64px. Stable pivot does not mean every heel has identical gait, as these are idle frames only.

## Honest limits

This is a distinct functional first-pass roster, not final character polish. No strike/cast/hit/death/celebration or walk animation is authored or approved. Existing combat event/release ownership must not be changed or represented as authored by these idle frames. Do not silently give a named NPC a different role; all later levels must retain their own art. PixelSize and native-screen silhouette/occlusion require scene acceptance. Keep temporary fallback labeling in demonstrations.

`test_roles.py` verifies all19 actual C01 units map to these9 types, all18 frames reconstruct exactly, each texture hash matches, all boundaries/alpha/contact pass, and rear frames differ from a horizontal mirror. It does not launch Godot or certify camera-facing/in-engine animation behavior. QA reports and all previews are written outside the worktree to an external diagnostic directory (`--out PATH`, default system temp `empire-c01-role-previews`). Running either generator/test does not write `.pyc` caches.

## First action-sheet experiment (unbound)

`python draw_actions.py && python test_actions.py` builds six separate infantry sheets and `action-sheets.json`. This does not replace idle metadata. Supported: flag-bearer, swordsman, shield, spear, archer, cleric. Each has front-right/back-left physical attack (cleric uses `cast`), three impact poses and four defeat poses. Total140 authored frames. Arms, torso, weapons, kneeling and prone body are redrawn from explicit pixel joints; no whole-sprite rotation/scale/warp or color-flash-only action substitutes. Back heads/straps remain separately drawn; equipment projection is direction-aware.

Metadata supplies per-view exact rectangles, durations, visual release index and final-defeat hold. `referenceReleaseMs` matches existing source contracts: generic75ms, Torren160ms, archer320ms, gravekeeper300ms. This metadata is not a new gameplay timer. The existing BoardView receipt remains the sole action/event owner; bridge may sample the visual clip against it. This package has not bound these sheets or tested synchronization in Godot.

Defeat keeps the same frame pivot `(32,56)` but intentionally sprawls below the standing contact, up to y62 for dropped equipment. Do not “correct” this by reanchoring each death frame. Standing attack/impact feet remain stable. Prone shapes, torso transitions and named identity still need visual polish; this is functional pose readability, not a final smooth animation set. Movement, celebration, cavalry and captain-specific actions remain unavailable. Never imply all common states or every unit are complete.

`actions-native.png`/`actions-2x.png` show front-facing ordered keyframes; the two-row per-role sheets include rear-facing variants. Tests prove exact reconstruction, binaryalpha/clear per-cell perimeter, distinct poses and release metadata matching the current contract, not in-engine animation success.

## Packaging verification

Runtime images are exactly nine `*-views.png` and six `*-actions.png`, with existing `.import` companions retained where present. No preview/GIF, duplicate single-role texture, test report, or compiled cache is shipped in this directory. Source and tests remain alongside manifests; diagnostics go outside the worktree. Original image hashes were compared before/after packaging and are unchanged.

`draw_roles.py`, `test_roles.py`, and `test_actions.py` accept `--out /absolute/external/path`; in-worktree output paths are rejected. Tests reconstruct previews from runtime sheets, so they do not depend on preexisting QA images. `draw_actions.py` writes only its deliberate runtime sources/manifests and creates no diagnostic directory. Archived prior preview files are outside the checkout.
