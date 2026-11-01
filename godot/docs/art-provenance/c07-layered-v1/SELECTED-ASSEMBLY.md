# C07 selected assembly — single advancing candidate

## Current owner

- Scene: `res://data/scenes/c01-07-selected-assembly.json`
- Instance layout/order/site ownership: `c01-07-selected-assembly-ground.json`
- Referenced asset definitions: `c01-07-selected-assembly-objects.json`
- Each surface asset's referenced geometry file owns its explicit local polygon/UV/contact. Existing sprite assets retain their own declared strips.

These are complementary owners, not parallel candidate scenes. **Subsequent selected-object integration edits this assembly**, rather than copying it to another per-batch production-directory preview. There is no new runtime selection switch, wrapper or composition root: use the existing `BoardSceneRendering.Load`/assembled factory/compiler.

Earlier `c01-07-*-preview*.json` resources remain frozen historical audit fixtures. They preserve what earlier pixel/geometry comparisons actually measured, and must not advance alongside this selected assembly. New before/negative cases are derived into the external audit workspace from the selected resource, not maintained as another live scene definition. The bootstrap script is an archived one-time exact merge and refuses to overwrite the selected files; it is not an ongoing generator from stale slice definitions.

The initial merge retained the barracks object's five-instance order, then the non-overlapping granary instances. Shared3asset definitions and placements were compared exactly before deduplication. No geometry, UV, material, placement or rules changed in consolidation.

## What this is and is not

At this checkpoint:16independent instances,5independently owned building/gate sites and1393depth entries. The1393includes the existing276-strip barracks body and178-strip granary body plus188-strip council hall and188-surface southeast stone gate plus22-surface independent wooden leaf and174-surface southwest stone gate with20-surface independent left leaf; it is not a performance pass. The5sites are **not the total interactive sites of C07**. Remaining canonical sites still use legacy default building placeholders. Walls, other foundations, yard/stock, gates and scenery remain incomplete.

Official `c01-07.json` is unchanged (55painted groups/108parts), and selected assembly remains `runtimeReady=false`. Neither clean-ground visibility nor default placeholders prove full foundation/route/art correctness. Do not bind it to normal campaign entry until selected/full-scene responsibility, material/contact integration, input/cue/camera and measured performance acceptance are complete. C06/C07 real gameplay remains separately paused; all present checks are pure static/display-clone tests.

Next closed components must join this assembly while preserving previous actual-role/material/site tests. The northern rejected lookout geometry and absent leaves remain rejected/incomplete, not implicitly selected by consolidation.

Council hall integration: [selected-council-audit](selected-council-audit/README.md), exact reviewed asset/placement transfer with fresh three-role directions and two wrong-depth negatives.

Southeast stone formal integration: [southeast-stone-surface-audit](southeast-stone-surface-audit/README.md); independent leaf joined this same assembly: [southeast-leaf-surface-audit](southeast-leaf-surface-audit/README.md).

Southwest stone formal integration: [southwest-stone-surface-audit](southwest-stone-surface-audit/README.md); its independent left leaf is now included: [southwest-leaf-surface-audit](southwest-leaf-surface-audit/README.md).
