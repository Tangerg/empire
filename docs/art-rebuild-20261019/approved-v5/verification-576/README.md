# 576-topic integration checkpoint

Eleven additional canonical catalog bindings since 565: the complete C02 antler-knight four-pose bundle, four Farlight terrain materials and the final six C02 HUD symbols. All 16 C02 HUD topics are now bound. This checkpoint also incorporates the river review harness/route regression corrections and the isolated synthetic forest scale fixture.

## Verification

- Full TypeScript suite: 137 files, 1,938 tests passed (typescript-corrected.txt.gz).
- Full Python art suite: 351 tests passed (python.txt.gz).
- Final typecheck: exit 0 (typecheck-final.txt.gz).
- All four application builds passed: game, editor, engine-demo, experience-lab (build-apps.txt.gz).
- All 911 selected raw master paths in the 576-topic ledger exist in the staged Git index.
- Existing C01 deterministic terrain check and 14 exporter tests passed; six new C02 native-density exporter tests passed. No C01 runtime terrain output changed.

The initial TypeScript run had four failures (shared board/grid ownership, river route expectation and a C01-only terrain count encountering C02 registrations). The corrected run retains all assertions with the correct owning helper, actual mounted state, scene road contract and campaign-specific scope. Initial logs are preserved. A subsequent typecheck exposed the helper's nonexistent BattleState type; this was corrected to the engine's existing GameState, followed by clean typecheck and all builds. No runtime behavior changes in that type-only correction.

## Quality boundaries

Catalog binding is not full-scene acceptance. Antler-knight retains an unresolved 2.742–3.230 logical-pixel visual baseline gap relative to its unchanged anchor. The same-scale shared renderer diagnostic places measured supports inside its existing contact shadow, but outer hooves exceed that generic shadow and there is no C02 playable consumer. Original sources and a future source-correction proposal remain preserved; no offset guard, footprint or anchor was weakened. Native 64 is accepted, the secondary 32 downsample is not runtime density.

Farlight material windows retain original source bytes and exact historical tile alpha masks. The projection-garden source uses four declared square windows rather than stretching its non-square canvas; its central strip is explicitly omitted. C02 terrain has only its existing native density, no invented HD output or continuous-world consumer. Small HUD details lose definition at native 32; silhouettes remain readable on the three reviewed backgrounds.

Live game acceptance remains false. Fresh browser navigation could not reach the reported development server, and no live interaction or full task completion is claimed. Future Hesha, boarding-team and C03 prop candidates are excluded from this checkpoint's count.
