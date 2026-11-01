# C07 minimum production integration sequence — 2026-11-01

## Decision and current boundary

This is an implementation plan, not completed integration. The official `c01-07.json` remains painted ground (55 groups / 108 parts). All new candidates are static and unbound. The latest council preview registers six sprites; atomic canopies, fences, boxes and gate geometry are still external audit combinations. The empty clean-ground image must not replace the official scene while omitted buildings/walls/trees would make blocked terrain appear traversable. No C06/C07 commands, AI turns or saves are advanced here.

## One geometry owner

Extend `AssembledGroundDocument.Factory` / `AssembledGroundView.Create`, reached through `BoardSceneRendering.Load`; do not add a second composition path or BattleScreen test switches. The asset may declare resource-local authored surfaces, explicitly exclusive with its existing whole-canvas DepthStrips representation. Existing valid sprite assets remain supported because current chapters use them, not as a compatibility shim for failed geometry.

An authored surface owns polygon/UV, material coverage mode and physical or explicitly virtual support. Instance contact/scale and canonical site belong to the existing placement. Compile immutable triangles, clipped UVs, support slices and bounds once. Drawing, `StandingVisual.Covers`, pointer/cue geometry and `TerrainDepthEntry` consume this compilation. Opaque-material UV guards remain distinct from native-alpha sampling; neither may silently substitute for the other. Reject unsupported inner-hole rings instead of filling them. Restrict tint alpha or explicitly account for it in coverage.

Do not migrate external wrapper nesting, C07 absolute coordinates, hard-coded source-to-tile ratios, probe actors, environment variables or expected test counts. Source authoring data and deterministic compilation have one editable owner. Identity-UV regions and rebuilt-UV regions are explicit; removed base regions cannot still draw. Same physical wall upper/lower regions share support; unrelated roof/tower members do not. Support comes from the member-owned UV domain, never whole-image low(x). Structural continuation over empty columns is labeled virtual, not a measured foot.

`GroundSite` is registered once per semantic instance, never per mesh/strip. Existing BoardView owns draw ordering and picking; material coverage, pointer selection and conservative cue avoidance remain separate contracts.

## Small, recoverable sequence

1. **First complete loader slice:** migrate the closed training-fence geometry/material to the production assembled path in an isolated static scene. No official C07 binding. Prove its three transparent holes, beam, actual C07 front/back appearance and wrong-material/depth controls through the real loader; compare legacy sprite chapter loading unchanged. This establishes the reusable path before importing hundreds of components.
2. **Selected-set assembly:** migrate blue stand, three main buildings and their closed atomic canopy/cargo groups, then the two southern gates. Each group needs the same Draw/Pick/support owner and site/approach tests. Northern jamb parts may be diagnostic-only; rejected lookout mounts and missing leaves cannot masquerade as finished gates.
3. **Scene responsibility closure:** complete remaining rules-significant buildings, street walls/trees and entrances, with original/fixed actor evidence and ground registration. Preserve known T7 wall/tree behavior. Missing major objects on clean ground block production switching; smaller decorative differences remain explicit visual debt, not invisible omissions.
4. **One integrated static acceptance batch:** full-scene same-camera images, actual roster appearances, front/back/side occlusion, continuous opening/approach checks, site uniqueness, targets/cues, scaled windows, load/unload/reload and malformed-asset failure recovery. Compile production and external audit separately; old probe success is not production build success.
5. **Measured performance and formal binding:** satisfy correctness plus a bounded controlled performance comparison, then switch official scene only when scene coverage and registration permit. Real normal-play victory is a separate pending-authority validation, never inferred from static probes.

Each step ends in a source commit and verified durable backup; current GitHub authentication remains unavailable. No main/force push or new chapters.

## Verification that must survive migration

- Preserve actor alpha evidence, native pointer behavior and original failure samples; ground can outrank a covered actor, so negative Pick alone is not proof of object coverage.
- Same compiled triangle/UV used by draw and inverse native-alpha sampling. Interior samples alone do not establish GPU linear/mipmap edge equality; inspect seams at supported zooms and subpixel positions.
- Count overlapping primitives, including duplicates inside one depth entry. Negative controls: undeleted base, duplicate member, draw-only UV change, independently wrong left/right support. Each must fail its intended assertion, not fixture initialization.
- Original continuous openings and real support boundaries retain their contract. Do not move samples or relax alpha to turn the two northern lookout failures green.
- Keep canonical terrain, passability, entrances, roster/state digest and save semantics unchanged. Visual changes are not permission to change gameplay geometry.

## Performance evidence and bounded first optimization

Performance is **not passed**. Existing C05 software-rendering comparison measured 293 calls / 26942 ground patches / 147 partitions for strips versus 193 / 15518 / 67 for an incorrect unsplit control; the latter broke rear-rail occlusion and is not an acceptable optimization. C07 barracks/granary main bodies alone have 276/178 slices, before atomic geometry. Counts are risk signals, not measured GPU FPS.

BoardView assigns Z from `Projection.LayerAtDepth(StandingDepth(entry), layer)`; BoardProjection uses floor(cameraDepth), while every distinct standing cut can also grow ground partitions. Therefore measure entries, primitives, partitions, patches, draw calls, texture switches, build time and steady frame/process/render samples. Use identical static full-scene camera/actors/renderer, warm up 10 seconds, sample 20 seconds without screenshot/import/build, and reverse run order. Cloud llvmpipe does not establish user GPU performance.

First optimization is same-material, same-depth, same-blend slice-local triangle batching with stable tie order. Cache immutable compilation. Do not replace distinct support with a whole-object Z or delete alpha; projected depth/elevation, ground partitioning, semi-transparent overlap and actor interleaving must remain correct. Wider spans require proof across legal roles, shadows and high/low terrain, including C05 regressions. No speculative renderer rewrite precedes measurement.

## Explicit unfinished quality

Northern lookout architecture/voids and leaves; remaining scene responsibility; ground/building sharpness mismatch; dense roof/grain detail; flat gate wood/hinges; weak contact shadows; all-scene performance; and authorized real C07 flow remain unfinished. The selected local candidates are useful engineering evidence, not a claim of final top-tier art or whole-level completion.
