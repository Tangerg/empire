# Painted three-bridge sample

This independently authored playable scene expands the approved whole-illustration direction. It is not the default campaign and does not replace the mountain-pass sample.

Run `res://Scenes/PaintedThreeBridgesSample.tscn` after the standard native build/import. Its explicit scene definition composes the existing engine, session, tactical screen, AI and persistence. F5/F9 share the Save/Load callbacks and use only `user://three-bridges-sample/battle.json`.

## Art and tactical authoring

The native mother image is 1672×941 and is uniformly scaled by 0.65. The logical grid registers image-space axes of (40,0) and (0,24), with origin (16,12). This registers feet and movement to the painting; it does not compress the image. Camera pan and uniform zoom reveal the larger environment.

`three-bridges-level.json` is the sole owner of terrain, bridge cells, outside margins, players, units, interaction cells and objectives. `three-bridges-art.json` binds its exact SHA-256 and supplies image registration and visibility-only foreground contours. Road corridors do not have a second runtime mask. Painted rocks, inaccessible banks and house footprints are excluded from walking. Registered open water uses the composed water terrain, not a visual obstacle test. The two village aid-post door cells use the catalog's ordinary capture and owner-turn healing rules. The western swordsman intentionally starts at 68/98 HP as an authored wounded arrival, making recovery a useful opening choice rather than requiring artificial runtime damage.

The first art source is `arch-three-bridges.png`. Its approved generated edit removes baked people, their weapons/shadows and two flags, while retaining the three bridges, four houses and river composition. Local details were regenerated, so this is not a pixel-exact edit. Canonical mother-image SHA-256: `b95e877c9fb999f3e7e7d46d5a272ab35b4a4b1cfe8d97d1e1189b3fa11d136f`.

Foreground houses, rail sections and the selected tree use exact original pixels from this same mother image. Their polygons own visibility only. Canonical cell references address capture or depth registration; no polygon carries navigation or cover rules. Static painted houses and bridges are not destructible. The separately authored low barricade has its own ordinary structure lifecycle and does not imply all background objects can be destroyed.

## Acceptance status

The Linux Godot sample has been run through actual cloud-desktop input. In the production initial formation, the pre-injured swordsman reached the western door, progressively captured its aid-post, and recovered from 68 to 83 HP on the next friendly turn. F5 saved that state; after walking away, F9 restored the unit's position, health, house ownership and destroyed barricade. Normal enemy AI attacked the barricade through its damaged state into low rubble. Northern stone-bridge walking was checked in actual play.

The southern stone bridge and wooden bridge were additionally inspected with an isolated initial formation beside their approaches, then ordinary movement and actor-body clicks through the real session/screen. These controlled placement checks are visual registration evidence, not a claim of a complete legal campaign march. The production unit seeds were not replaced by those fixtures. The acted indicator is a small badge rather than a translucent square over the character.

The focused native scene check uses the composed domain rules to establish all three connected bridge footprints and both banks, foot denial on registered water/outside, positive-HP barricade blocking/half-cover, actual attacks and destruction, and post-load movement into the cleared rubble cell. It also checks progressive capture, owner-turn healing, exact save/load continuation, same-image foreground UVs, polygon admission and borrowed-texture lifetime. This is distinct from manual gameplay evidence; GUI walking onto rubble was not separately recorded.

Only the two registered house doors are interactive aid-posts. The other painted houses, farm props and trees are scenery; no claim is made that every visible object can be entered or destroyed. Bridge rails and houses use finite foreground sections, not a full per-pixel depth model. The scene is a separately launched playable sample, not a replacement for the default campaign, a full battle victory, or a macOS execution result.
