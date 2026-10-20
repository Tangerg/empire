# Opening wristguard scene classification

C01-CH01-S00 is the intended Roderick/Leia hand-interaction scene. The authored chapter opening explicitly assigns S00 to the image and PROP-LEFT-GLOVE to the separate reusable wristguard; the static scene list and novel continuity agree. Only the canonical `kind` changes, from `key-prop` to `scene`. The inventory mirrors that kind and the future `opaque-scene` requirement. Exact 128×128 geometry, ID, label, path, absent frame/anchor metadata, legacy quality tier and old PNG bytes stay unchanged. There are now 43 scenes (41 wide, two square), 15 props and 22 cards in the same 80-topic narrative inventory.

The original source mapping is correct but depicts the wrong subject: catalog slot 44 → prop-board-04 bottom-right → lone blue-gray, metal-rimmed bracer and tied cord. The full board and exact cell were inspected. Reconstruction from the original cached alpha and historical inset/bbox-fit pipeline exactly matches the current runtime RGBA pixels. `evidence.json` records source hashes, full/inset boxes, both canonical records and the bounded inventory delta. The original board, cached alpha, builder and prior narrative audits remain unchanged historical evidence.

Unlike the cemetery correction, the retained S00 PNG is still transparent (alpha 0–255), SHA-256 `3dd8fad06b9c1c9dfdb26cdb759acdc5fbf9137c87392aa45fdcb9726b3c024f`. That is an explicitly pending subject/alpha mismatch, not an opaque-scene acceptance exception. No new art, native master, source override, HD record, consumer, quality promotion or completed topic is added. The 35-story consumer audit finds zero S00 selectors; the prologue still selects CH01-S01. Generic asset URL resolution does not count as a shipped scene consumer.

Future S00 artwork must be a newly authored, complete opaque square scene showing Roderick fastening Leia's left wristguard. Use the already-supported `opaque-scene-v1` route; preserve exact square crop/density rules and review named-character/hand-action identity. Do not promote the loose bracer, remap to PROP-LEFT-GLOVE, stretch it to 16:9, extract alpha from a scene, or add a selector to manufacture integration. The topic-specific transparent-prop hold is removed because canonical kind now selects and validates the same scene path as every other scene.

Verification at branch `fix/opening-square-scene-20261020`, base `46cc02f`:
- 93 Python tests passed: scene, prop, card, static export and approved ledger
- 368 TypeScript tests passed across narrative scenes, key-props, HD density and approved catalog bindings
- Aggregate typecheck passed
- Read-only registered exporter check passed for 398 outputs across 346 topics
- Both new opening regression guards fail when only temporary fixtures revert S00 to key-prop; production manifests were never reverted
- Source reconstruction, unchanged bytes/selectors/registrations, manifest/inventory bounds and CRLF-aware diff checks passed
- Full application build, full repository test suite and browser visual acceptance are not claimed; no artwork, gameplay or presentation selector changed

The sparse worktree initially omitted unrelated source/runtime files. They were materialized unchanged from Git for the integrated checks; missing-file failures were resolved without code changes. A brief executor reconnect cleared initial temporary logs, so the retained logs are complete reruns. `collect-consumers.cjs` and `audit.py` are read-only inspections except for writing their local JSON reports. Run from the repository root; the audit deliberately pins the reviewed checkpoint and verifies only this isolated correction. Later art registrations require a new review rather than altering this historical evidence.
