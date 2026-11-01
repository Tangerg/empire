# Barracks front canopy — second formal surface-loader slice

Scope: seven already reviewed canopy members, plus the existing main body, blue stand, cargo and formal fence in an **unbound static preview**. Official C07 remains its painted scene. No commands, AI, campaign saves or real-play victory. This is not a completed barracks foundation or full level. The world geometry, material voids and support curve remain unchanged.

## One runtime owner and deterministic transfer

`c01-07-canopy-surface.json` is the runtime surface/contact owner consumed by the existing `AssembledGroundDocument` compiler. No runtime canopy wrapper, competing curve or new renderer was added. The archived `atomic-canopy-surface-source.json` is offline provenance/input; `TexturedSurfaceExport` deterministically transfers it to explicit surface members. Regenerate rather than separately edit both representations.

Unlike the fence's clip-then-clamp mapping, the frozen canopy renderer triangulates each original member, clips each triangle by the x slab, then applies that original triangle's affine UV map. The exporter preserves this order. Resource origin is (944,125); placement scale is1. Its54 explicit contacts are **structural support projections**, not54 certified physical feet. Original front973,192 and hidden-left/new-design distinctions remain in source notes. Existing3 objects and their sites are unchanged; appended canopy/fence are pass-through and own no site.

## Deliberate UV repair, not original-pixel equivalence

Formal opaque validation exposed one original material edge texel (218,318), alpha239/255. The historical24-step triangle sampler missed it (both diagonal choices minimum252). The guard's .96 is a material-mode requirement, **not a character Pick threshold**.

Only the roof's four UV coordinates move0.2% toward their mean; maximum displacement0.836 source texel. Same PNG, polygon, support, tint and coverage mode. Original conservative scan221270 touched texels had one failure; corrected220379 texels have zero failures and minimum246/255. Fresh formal loader validates all7 members. See `uv-analysis/` for repeatable scans. Frozen original remains separately archived and fails the same production material assertion; we did not relax validation or switch to native-alpha.

GPU images are actual native renders at the same camera/actor pose (viewport1180×812, diagnostic zoom4). Outside72 caption rows, original→inset differs at8326 pixels, maximum channel58, all within roof bounds(507,80)–(708,187), for both actor poses. This is real high-contrast tile resampling, **not identical/invisible pixels**. Root visually reviewed original/formal pair and accepted the bounded UV repair. Dense repeated slate and ground/material/shadow debts remain.

After that deliberate repair, corrected legacy wrapper→formal renderer has exact CPU equality for1086 triangle vertices (position/UV/tint/depth),54 supports and3795 material samples. GPU migration differences are52(front)/55(back) pixels, maximum channel1; not bit-identical. Formal same-support batching has54 Polygon2D nodes versus193 historical canopy polygons. This establishes node reduction, not a full-scene FPS pass.

## Actual-appearance static contracts and negative controls

Torren60,12: head270/270 visible; canopy overlap83/pick82. The remaining973,193 pixel belongs to fence supports26/27, and moving only canopy behind actor does not recover it. Torren60,11:122 canopy overlaps, actor picks0; moving only canopy behind restores5/5 captured witness pixels. Adjacent61,12 remains25/24 with the same fence owner.63,6 has zero canopy overlap.59,11 is illegal terrain and excluded, not covered.

All12 fixed positive/void samples pass. Four deliberately broken **formal-loader** variants exit1 at the intended stage: original UV→opaque material edge; filled hole→material contract; early depth→true-back contract; late depth→front/head contract. A generic exit1 is not accepted. Full logs/results are retained. The earlier diagnostic run before rebuilding the referenced production DLL lacked new texel text; it is not the fresh-DLL evidence.

Data-contract JSON verifies original7 polygons/tints/support and existing3 placements unchanged. Six paused C06/source/entry files retain their v53 hashes; paused state SHA e8add96b2258b1e940ddecef4d6f5bda7414127562f89df47144325509e69bdf. No action-bearing route fixtures are included in this batch.

## Reproduction

Build production `godot/Empire.csproj` first, then an external Godot.NET probe project referencing its freshly built DLLs. Copy archived probe/export scripts and fixture JSONs into that external project, with `data`/`assets` pointing at production resources. Run `TexturedSurfaceExport.tscn` with `C07_TEXTURED_SOURCE=res://atomic-canopy-surface-source.json`; compare output to the checked-in surface. Run `FormalCanopyProbe`, `CanopySurfaceEquivalenceProbe`; negative variants are produced by `make-canopy-negatives.py`. `CityPreview` remains an external static-display helper, never a production composition path. GUI launch script labels all screenshots static, incomplete and not gameplay.

## Final focused gates
Fresh production build and external build:0 warnings/errors. Format verification and indexed source metadata pass. SceneSurfaceChecks14 assertions pass. C01–C06 repeated initial static renders:0 issues, states unchanged. All four targeted negative stages verified. Deterministic re-export is byte-identical, surface SHA256 `f1984898f6781a4829c1fde831c60a90259bb51d42e59a1bfab4cac330c43147`. No all-campaign action gate or full-scene performance claim.
