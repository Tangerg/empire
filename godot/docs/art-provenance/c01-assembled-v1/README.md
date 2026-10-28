# C01 independent scene assets — 2026-10-28

Generated with built-in imagegen. Existing `assets/native/scenes/c01-01-painted.png` was inspected and used as composition/camera reference, not retained as C01 runtime terrain. The old complete image remains only as an art reference.

- `ground.png`: remove all houses, freestanding trees/bushes/rocks and old object shadows; reconstruct underlying ground; preserve the road network and two terrain plateaus. A second precise-object edit replaced repetitive bright grass flecks with broad quiet moss/olive planes and a few sparse low tufts. This remains a ground-only painting, not a collision/terrain owner.
- `cottage.png`: isolated near-frontal high-oblique timber/limestone village house; warm brown roof, broad matte shapes, no surrounding terrain or cast shadow.
- `granary.png`: sibling timber granary with double doors/loft vent, no chimney; same camera/light/material scale.
- `outpost.png`: sibling squat guard hut with slate-blue hipped roof, plaster/timber walls and simple door canopy.
- `pine-broad.png`: isolated rooted pine, four broad foliage tiers instead of many narrow needle tiers. Upper-left moss light/dark teal grouped shadows; no ground or baked shadow.

All objects retain generated RGBA. The image tool's RGB preview can show a dark/colored matte outside a house although those sampled pixels have alpha zero; native composition, not the RGB-only thumbnail, is the acceptance witness. No script cropped, recolored or blurred these bitmaps. Native object imports use mipmaps for strong minification; terrain remains linear. Source image dimensions and physical foot/depth/contact-shadow geometry have one owner in `data/scenes/c01-01-objects.json`.

The scene contains explicit independent instances. All tree foot contacts were checked outside canonical playable cells; canopy overlap is rendered by the shared depth pipeline. Building selections bind to the unchanged four canonical capturable cells. Decorative scale/contact do not modify walkability, map elevation, save facts or unit rules. Contact shadows are procedural children and move/disappear with their object.

Original generated files (work area):
- clean ground: `exec-3c95a0a0-a4bb-438f-b5b3-f51078f40860.png`
- cottage: `exec-302a9bd5-75a7-4a1a-bef9-2ce4c3bf810a.png`
- granary: `exec-e086d17e-e349-4b35-bcac-0f7708a609f8.png`
- outpost: `exec-d9d55774-1c92-466d-b368-22b1e4729a29.png`
- broad pine: `exec-2a272563-438f-4183-9d02-56ccc3737564.png`

Validation and final gameplay review are recorded separately; a first assembly screenshot is not final acceptance.
