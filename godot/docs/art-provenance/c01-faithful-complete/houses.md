# C01 faithful house completion

Built-in imagegen edits of `assets/native/scenes/c01-01-painted.png`, 2026-10-28. Each house is a generated faithful reconstruction/extraction, not a byte-identical crop. Original camera, warm linework, modest planar shading, single-door stone/timber construction and attached low shrubs were requested. No runtime registrations or rules were changed by the asset worker.

All three deliverables are unmodified 1536×1024 RGBA outputs; transparency preserved. Prompt text is recorded beside this file. The generator preview shows RGB bleed outside the silhouette, but sampled exterior pixels have alpha zero; actual mipmapped game rendering must be checked rather than judging its RGB-only preview.

| Asset | Generated source | Alpha >127 bounds | Suggested door-foot anchor / front |
|---|---|---|---|
| central-house.png | exec-aa60da8b-6859-425b-ac00-2e3452028b69.png | (189,36)–(1318,984) | (831,953) / (831,975) |
| granary-house.png | exec-95d27454-c6dd-4a55-b656-166bf2becb64.png | (235,79)–(1316,911) | (762,877) / (762,905) |
| east-house.png | exec-239d50a8-350c-4627-ac14-3eec3620f61d.png | (151,77)–(1387,924) | (767,899) / (767,916) |

These anchors are visual starting points, not gameplay cells. The scene owner must verify final world scale, doorway picking, shadow registration and actor occlusion in-game. No automatic bitmap cleanup or blur was applied.

Initial central attempt `exec-0f6df8f8-32c9-48aa-902f-a244fff5e162.png` was not selected because the companion reference biased it toward the west-house annex. It remains outside the project; only the three selected outputs above are project assets.
