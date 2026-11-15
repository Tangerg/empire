# C01 miniature environment: approved PBR palette stage

## Current review: joined market street

The staged N3 market pair, N1 joined cottages, W1 rain shelter and E1 weighhouse come from
[`authoring/joined-market`](authoring/joined-market/README.md). These five named
resources are not outputs to overwrite from the historical module generator.
`manifest.json` describes their current bounds, authoring source and output
identity. Terrain and scene placements remain the canonical C01 definitions.
The build details and acceptance observations below describe previous versions.


Load `res://assets/c01-miniature-environment/battlefield.tscn`. Nine original GLB resources supply eight placed building instances on93 canonical wall cells (one generic lower-row module is retained unused). They are visual manifestations only: no collisions, navigation, ground, roads, gameplay scripts, or house tactical metadata. C01/BoardProjection remain sole traversal/height owners.

## Scale and placement

Godot X=column, Z=row, Y=up;1m per cell, .75m per elevation. Individual root centered on footprint with ground Y=0. `Roof` and `Walls` remain separate mesh nodes for view/picking policy. N3 is split across its true upper/lower wall rows. All full3D AABBs, including roof tiles and beams, stay within occupied footprints. Horizontal .96 modeling clearance preserves actual slope-mouth lanes. Verified exact wall bounds/levels are in placement-verification.json.

## Shipped assets and second stage

Current seven GLBs are the **approved dark-plinth PBR atlas version**, not the earlier17-material outputs. Each uses2 mesh surfaces and1 material, with embedded64×64 basecolor/ORM palette textures. Source face color, roughness and metallic values are encoded per-face; no lighting baked. Only sandstone was intentionally darkened in linear RGB by(.56,.60,.66), giving actual stone foundation courses a colder/darker contact base. No extra decorative rubble/retaining objects were inserted into roads.

The first atlas optimization preserved all304,608 placed triangles, including soft bevels, roof courses, oak beams, inset windows and door planks. The later N3-only roof-detail pass below reduces the placed total to270,764 while retaining silhouette detail. Eight instances have16 base surfaces instead of136, before render-pass multiplication. Actual native FPS remains an integration measurement, not a claim from asset conversion.

Generic short rows have1.02m doors (the4×2 remains E1); dedicated N3 now has1.12/1.04m doors. Older remaining five module types retain .77m doors. Do not claim soldier/door scale is corrected across the full kit. Large-door values and all dimensions/hashes are explicit in manifest.json. Frame-level variation/composition still requires visual review; these assets do not on their own satisfy final art direction.

## Rebuild without silently replacing approved runtime resources

Blender4.3.2 authoring; use external staging folders:

```sh
C01_ASSET_OUTPUT=/tmp/c01-base blender -b -t 2 --python build_assets.py
blender -b -t 2 --python optimize_palette.py -- --source /tmp/c01-base --output /tmp/c01-approved-palette --dark-plinth
```

Validate staged geometry/triangles, then deliberately replace the seven same-nameGLBs. No duplicate runtime resource set is needed. `build_assets.py` supplies geometry and original palette; `optimize_palette.py` is the single approved second-stage encoding/grade. The generator preserves the short-row door variation and palette generation batches. Reproduction targets geometry/material specification; Blender/glTF serialization byte identity across versions is not promised. Approved base hashes/provenance are in generation-provenance.json, current shipped hashes in manifest.json.

`verify_geometry.py` checks headers, scene transforms, Roof/Walls, exact footprint AABBs and manifest surface/material counts using Python+NumPy. External conversion validation also compared every source triangle/vertex: unchanged counts and maximum corresponding vertex error below.000001m. Generated QA/backups stay outside this runtime directory.

## Single-owner tactical beacon

`c01_market_beacon.glb` is not instantiated by battlefield.tscn. Bridge instantiates it exactly once per real capturable site and owns identity/tint. Tint only `Banner`; `Stand` retains matte stone/wood/brass. Root ground0, center offset(.32,0,.32) keeps its whole extent inside that cell and leaves normal unit foot contact clear. Contracts/measurements are in beacon-contract.json and beacon-verification.json.

## Approved N3-only third geometry pass

N3 now references dedicated `c01_n3_upper_market.glb` and `c01_n3_lower_gallery.glb`, not generic short-row modules. Upper eave1.65m, three distinct shop bays,1.12m doorway; lower eave1.30m with1.04m doorway. A recessed upper facade contains a matte cloth awning entirely inside the real4×2 wall footprint. Native positions/elevations/site identity are unchanged. No new street prop or click owner exists.

Roof interior repeating bevelled tile solids are replaced by original PBR tile albedo+normal surfaces; ridge caps and eave silhouette geometry remain. Both new modules are opaque, use linear/mipmap texture sampling, and retain Roof/Walls with2 surfaces/1 material. Old/new same-light offline comparison plus a440px-wide small-scale check were reviewed: shopfront/tier articulation is stronger, no obvious ringing observed. This is not a substitute for native full-scene performance/occlusion acceptance.

N3 upper triangles28,950→10,658; lower25,710→10,158. All eight placed building instances now total270,764 triangles (11.1% total reduction, not a promised FPS gain). Bounds/opacity are recorded in n3-third-verification.json and manifest.json. All eaves/cloth remain inside canonical occupied cells, exact ground0.

E1 still uses the original optimized `c01_row_4x2.glb`; other existing modules are unchanged. Obsolete generic `c01_row_4x1.glb` and its import sidecar are archived outside runtime; the base generator now emits only the six still-used base modules. Historical source SHA remains recorded in generation-provenance.json. N3 source is under authoring/n3 with `.gdignore`; no generated QA/backups are in the runtime source folder.
