# Existing ground-transition source audit

2026-10-20. Read-only runtime/source audit; this report is the only repository write. No generation, binding changes, runtime changes, or tests run. Read AGENTS.md and PROJECT_RULES.md. Source and runtime pixels inspected via temporary contact sheets. Paths below are relative to repository root; `F` abbreviates `packages/story-candidate-01/assets/final-fantasy-v1/`.

## Finding

There is no verified unused generic broad matte yard plate in the inspected registered environment cells. Do not treat filenames such as `watch-post-ground` as empty ground: these are complete prop vignettes. Existing low-frequency earth RGB and irregular grass/bank strips are suitable materials, but the strips are exactly the existing edge-decoration system, not an additional supply beyond its legacy placements. Connected earth blending exists but its masks are rounded tile geometry, not organic authored boundaries. The remaining integration problem is placement/contact and coverage geometry, not lack of microtexture.

## Verified active materials and ownership

- Grass: `F/masters/approved-v5/grass-edge/revision-2/original.png`. Registered by `F/edge-decoration-v1/manifest-edge-decoration-v1.json` as `grass-road-edge`; SHA256 matches `8821ec714c7dacf63f62617e1a46058f15616e0be80a45fa49c24c35c0ddc23f`. Runtime `F/edge-decoration-v1/runtime/grass-road-edge.png` (110×41), `grass-road-edge@2x.png` (220×82). Matte broad greens, angular irregular lower lip and dark earth wedges; useful at selected road/grass contacts, not a continuous blurred fringe. Source anchor [1091.5,366.5], source scale 0.047961630695443645, padding 63. Source meaningful bounds [46,48,2138,611].
- Bank: `F/masters/approved-v5/bank-edge/revision-3/master.png`. Same manifest as `bank-edge`; SHA256 matches `f646b69f29854ad06fe1ef83654c4c48f0b6081c4c6fffdddda616b067884be9`. Runtime `F/edge-decoration-v1/runtime/bank-edge.png` (52×75), `bank-edge@2x.png` (104×149). Matte vertical grass/sand strip, irregular fingers at the contact, no water painted into it. Source anchor [514.5,768], scale 0.044630404463040445, padding 68; meaningful bounds [51,49,978,1487].
- Edge owner: `packages/story-candidate-01/src/presentation/candidate-01-edge-decoration.ts`, `candidate01EdgeDecorationPieces`. Placement scale must be >0 and <=1. Explicit flips exist, not arbitrary rotation. Map-size and complete local terrain-support guards intentionally disable stale placements. Terrain support includes complete exported footprint plus one neighbor. Do not bypass these guards or blindly stretch strips across a changed river/road. Calibration is `docs/art-rebuild-20261019/approved-v5/edge-registration-wide.json`; active manifest, not folder name, proves current registration.
- Road earth: `F/masters/approved-v5/earth-material/revision-2/master.png`. SHA256 matches `9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53` in both `tools/art/connector-overrides.json` and `F/world-materials-v1/manifest-road-material-v2.json`. Runtime `F/world-materials-v1/runtime/road-earth.png` / `road-earth@2x.png`. Manifest logical period 1024×1024, raster 512×512; use declared world sampling, not guessed density. Visually strongest broad matte material: a few low-frequency ochre fields, no stones or noisy grain. Current river bank uses this material too.
- Road coverage: `candidate-01-road-corridor.ts`, `candidate01RoadCorridorGeometry` owns reviewed 32/22/2 broad-road and 20/16/0 narrow-trail profiles, terrain clipping, protected cells and graph connections. `candidate-01-road-material.ts` owns material rendering. Connector overrides additionally register original 22-logical-pixel route connectors; do not confuse those legacy connector widths with the active authored corridor profile.
- River coverage: `candidate-01-river-material.ts`, `candidate01RiverMaterialPieces` uses `CANDIDATE_01_ROAD_MATERIAL` for bank then water material for channel. Width >0, bankWidth > water width, bankWidth <=64, difference <=10 (maximum rim 5px each side), strictly downstream knots. Explicit allowed-terrain clipping and occupied-cell rim clipping constrain paint. This is already the clean broad RGB path; boundary geometry is still a smooth stroke and is not equivalent to an organic edge asset.
- Connected earth: `F/world-materials-v1/manifest-earth-floor-v2.json`, source `F/masters/approved-v5/environment/surface-earth/earth-1/master.png`, SHA256 verified `67b614fcc35c14211232dcc48da0a471aa684ae15878187a79344baf7e92623c`. Runtime `F/world-materials-v1/runtime/earth-floor.png` / `earth-floor@2x.png`; logical 256×256, raster512×512. `manifest-earth-blend-masks-v1.json` references `runtime/earth-blend-masks.png` / `earth-blend-masks@2x.png`. `candidate-01-earth-material.ts`, `candidate01EarthBlendMarkup` owns world RGB sampled through existing Blob47 `transition-meadow-earth` coverage. `candidate-01-map-scene.ts` dispatches it. Native transition atlas is 16×3 cells, 47 components, 32×32 logical /64×64 2x. Suitable for coherent existing earth areas, but source is stonier/noisier than road earth and mask pixels show rounded tile joins, not irregular matte fringe. Do not represent it as a new broad soft-blend solution.

## Environment and yard cells: registered but generally wrong for generic blending

Authoritative `tools/art/environment-overrides.json`, catalog `F/environment-builder-v1/manifest-environment-builder-v1.json`; runtime `F/environment-builder-v1/runtime/atlas/camps-foundations.png` and `camps-foundations@2x.png`. Catalog cells 144×144 logical /288×288 2x, 4×4 atlas, per-cell ground anchors. Render through existing `environmentCellMarkup` / `authoredPlacementPieces` in `candidate-01-map-scene.ts`, with authored `foundation` layer. Automatic terrain foundations already render at scale 0.3 through `CANDIDATE_FOUNDATIONS` in `candidate-01-terrain-materials.ts`. Check that owner before adding a duplicate foundation.

Verified source paths and source SHA match:

- `F/masters/approved-v5/environment/camps-foundations/village-square-foundation/master.png`: square paving plus well, crates and flag. Not an empty town apron.
- `F/masters/approved-v5/environment/camps-foundations/supply-yard/supply-yard-master.png`: irregular earth island but tent, barrels, crates and tools. Not a generic soil plate.
- `F/masters/approved-v5/environment/camps-foundations/watch-post-ground/watch-post-ground-master.png`: watchtower, stakes, rocks; cannot underlay another tower without duplication.
- `F/masters/approved-v5/environment/camps-foundations/border-gate-ground/border-gate-ground-master.png`: paved crossing and two gateposts; wrong as generic building contact.

All four use registration scale 0.10366826156299841, origin [7,7]. SourceFoot respectively [627,1088], [627,1107], [665,1174], [627,1143]. Additional inspected active sources under the same directory: `keep-foundation/keep-foundation-master.png` (raised stone dais/stairs), `ruined-foundation/ruined-foundation-master.png` (walls/rubble/tree), `forge-workyard/forge-workyard-master.png` (roof and tools). These can be intentionally composed semantic scene objects, not multiplied to hide seams.

Other inspected registered alternatives:

- `F/masters/approved-v5/environment/rural-life/threshing-floor/threshing-floor-master.png`: hard stone-ring circular disk, not loose yard soil. `rural-life` runtime atlas cells112 logical/224 2x; declared foundation layer.
- `F/masters/approved-v5/environment/decals-small/mud-patch/mud-patch-master.png`: prominent dark outline. `cart-ruts/cart-ruts-master.png`: strong trenches. `decals-small` cells64 logical/128 2x. Both unsuitable for broad quiet fusion; more copies would emphasize pasted objects.

## Recommended integration priority

1. Preserve existing approved matte road-earth RGB and its world-coordinate sampling. Improve shared coverage/contact geometry through its existing owner, with clear terrain permission and bridge/building exclusion contracts.
2. Use grass and bank strips only for sparse irregular interruptions at correct contact points. They should break monotony, not become tiled borders or enlarged overlays.
3. Keep building feet aligned to existing authored foundation anchors and avoid duplicated automatic/explicit vignette foundations. Existing complete yard modules are usable only when they genuinely belong as the intended scene object.
4. Do not add microtexture, dense stones, more clutter, generic opacity fades, CSS filters, or alternate material owners to disguise hard seams.

Limit: This is a targeted source/caller audit, not a rendered full-scene acceptance review. No claim that these choices alone resolve the user's visual concern.
