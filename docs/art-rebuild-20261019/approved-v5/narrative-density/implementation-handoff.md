# Source-native scene density implementation handoff

Implementation uses the existing sparse `empire-layout-engineering-20261019` tree on new branch `dot/narrative-source-density-20261020`, based on main `37c028b`. The old branch is preserved. No new worktree, remote push, main edit, art/source/catalog/runtime/progress change is included.

## Contract and integration prerequisite

The code intentionally rejects the old schema. Parent must migrate its CURRENT HD registry and export the selected scenes together with adopting the code; do not replace the manifest from this older sparse tree.

- Existing file: `packages/story-candidate-01/assets/final-fantasy-v1/manifest-tactical-runtime-hd.json`
- New schemaVersion: `candidate-01-runtime-hd-v2`
- Delete root `pixelDensity`
- Per-entry fields remain `{topicId,png,width,height,pixelDensity}`
- Only CH01-S05, CH01-S01, CH01-S02, ARCH-THREE-BRIDGES, ARCH-REDSTONE, ARCH-SILVERWOOD become 1664×936 at 6.5
- Their paths, selected 1672×941 originals/hashes and explicit `[4,2,1668,938]` crops stay unchanged
- All unrelated entries, tactical densities, canonical geometry, metadata, registrations and bytes remain unchanged

The adjacent `migrate-scene-density.py` is an explicit one-time helper for the parent. By default it only reports; `--write` changes only the current HD manifest after checking every selected source/crop/hash and every unrelated density. It refuses surprises instead of guessing. It does not export images or change progress. It has not been run against main.

After schema migration, invoke `export_overrides.export(root, temporary_catalog)` with a temporary schemaVersion1 catalog containing only those six existing `opaque-scene-v1` entries from the current source-overrides.json. The exporter reads physical dimensions from the current manifest. Run again with `check=True`. This avoids re-exporting unrelated art. Keep before/after hashes of the six canonical PNGs and complete masters; they must remain unchanged.

Physical output now copies the exact 1664×936 region without resampling (`explicit-region-copy`, `resampling:null`); canonical output still independently downsamples using LANCZOS. Existing UI URL selection and CSS stay unchanged. The scene integration test derives its set from registered opaque scene overrides, so the concurrent S02/Silverwood additions are covered without a stale hardcoded topic list.

## Verification performed

- Python: 62 passing tests: scene exporter19, narrative card14, static exporter16, framed exporter11, cohesion preview2
- TypeScript: 103 passing focused tests across density31, runtime11, document4, actor consumers4, hero appearances24, map-structure presentations29
- Typecheck passed after fixes; final rerun recorded below
- Mutation proof: restoring the old density==2 path routing fails the native6.5 source-copy regression; removing tactical2 enforcement fails its regression. Both mutations were restored
- The six real-manifest/source-native PNG integration checks, source-bound asset/catalog suites and app builds are deliberately left to parent after CURRENT data migration. Sparse source masters are absent and physical PNGs have not been replaced here
- Focused TS suites used a temporary v2 header in this tree only, then restored the original bytes. The standalone density suite uses a mixed-density metadata fixture through the actual resolver; exporter and parent integration tests establish the physical PNG bytes
- No live browser was launched. Raw/export/runtimeBinding/liveGame remain separate; liveGame stays pending

Recommended final checks: the four exporter test files, `candidate-01-hd-density.test.ts`, `candidate-01-narrative-scenes.test.ts`, `candidate-01-assets.test.ts`, `candidate-01-runtime.test.ts`, `tools/art/approved_catalog_bindings.test.ts`, typecheck and normal app build. Review desktop/mobile title/lab/story/choice/staging/result cover crops, actual image URLs/natural dimensions and DPR1/2 before claiming browser acceptance.

Final local commit: `9eeee575dddf38adfea0f6151a71d0c855b4fc71`. Final typecheck and diff whitespace check passed; worktree clean. Migration helper was checked in an isolated temporary six-scene fixture: dry-run leaves bytes unchanged, write changes only six entries/header, repeat is byte-identical, and a mismatched source hash fails without replacing the manifest.
