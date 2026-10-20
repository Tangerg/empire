# Ambient FX audit and bounded exporter proposal

## Result

FX09–12 are four catalog-resolvable ambient loops, not deployed campaign weather effects. Their exact canonical identities are autumn rain, river shallows, strong wasteland wind, and mountain-forge heat. The proposed patch enables only these four additional FX contracts, preserves FX01–08 unchanged, and keeps animation lifetime owned by the consumer. No artwork, manifest, override registration, scene binding, gameplay rule or completion count was changed.

Start generation with GENERATION-CONTRACT.md, especially FX09's staggered rain/splash renewal section. generation-contract.json gives machine-readable orders, original row mapping, exact canonical/HD records and shared placement. The complete original source and exact audited code/manifest files are copied unmodified under evidence/; evidence-index.json records their original paths and SHA-256 hashes.

## Source and runtime findings

1. Original source/label mapping is certain: build_final_fantasy_v1.py lines352–365 takes four rows from each FX atlas and zips the24 rows to topic order; the original topic catalog lines435–438 labels FX09–12. Therefore atlas03 rows1–4 are rain, shallows, wind, heat. The original atlas is1254² RGB with magenta gutters and white cells. Its saved alpha intermediate still contains white panels; neither can enter the new exporter as genuine transparent art
2. Current authoritative canonical metadata is4×32px,12fps,loop=true,blendMode=normal,anchor[16,16]. HD metadata is256×64,density2. The old builder still emits[16,24], alpha-bbox crops and independently fits each frame, so it is provenance evidence only and must not be rerun over the new work. The initial canonical-topic inventory also contains the stale[16,24] snapshot; use the current pack manifest
3. Current CANDIDATE_01_WEAPON_FX binds only FX01–08; the heal binding is FX17. No application/campaign authored reference to FX09–12 was found outside catalog/test infrastructure. The existing consumer inventory agrees and explicitly says observedInBrowser=false. Arbitrary future/external callers can still request an effect by topic, so the finding is bounded to this checked source tree
4. CANDIDATE_01_ART.presentation.effect delegates to candidate01FxPicture. It resolves the HD URL, takes the canonical anchor directly, and declares frames[0,1,2,3],12fps,loop=true. It does not create or place a scene instance
5. FrameAnimationController.tick selects floor(elapsedMs*fps/1000)%4 for looping clips. Frame4 begins at250ms and wraps to frame1 at333⅓ms. SvgBoardSurface.effect creates a running instance, and its owner must call remove or dispose the surface. A loop does not automatically expire
6. BoardView.animateHit lines611–640 owns the420ms hit lifetime, white-circle overlay and damage number. animateHeal owns500ms. These are consumer policies; neither is an ambient clip duration. No change to those consumers is proposed
7. No FX-specific ground/water/depth mask is declared. The sprite viewport uses overflow=hidden to isolate the current32px cell; that is frame-window clipping, not a terrain mask. BoardStrip exposes no blend-mode field. These effects render normally/source-over by default, consistent with their normal metadata. The obsolete candidate-map-ambient screen-blend style is explicitly documented as having no emitter; it is not a live FX09–12 consumer
8. The original rows identify the right subjects but do not establish seamless loops. Rain ends in low beads, wind in sparse dust, and heat in short tongues. Existing runtime measurements expose substantial frame-content changes (wind alpha sum31320 in frame1 vs2124 in frame4 at1×). This is a seam-risk indicator, not substitute motion review. The proposed fourth phases explicitly renew the next cohort, and every topic requires direct4→1 review

## Proposed change

ambient-exporter-and-playback.patch changes only:

- tools/art/export_fx_overrides.py: adds four exact cyclic orders; requires actual Boolean loop=true only for those topics, while retaining loop=false for01–08; reports continuous-loop vs once-hold,333⅓ms sequence duration, last→first transition, consumer-owned lifetime and unperformed loop review
- tools/art/test_fx_overrides.py: adds five focused tests (four-topic export/check/provenance/alpha invariants; exact Boolean loop flag; one-shot loop rejection; wrong cyclic order rejection; no assumed hit lifetime)
- packages/game-ui/src/ui/__tests__/candidate-ambient-fx-playback.test.ts: eight tests through the actual candidate presentation and SVG surface, covering all four topics' wrap/persistence and owner removal/restart/disposal

All existing full-canvas geometry, exact source hashes, duplicate checks, transparent edge guards,1×/2× outputs, normal blend, anchor validation, output-path safety and all-before-write planning remain intact. The exporter still cannot approve art, detect all opacity-copy tricks, or prove a seamless loop. Its report explicitly leaves semanticMotionReview and loopClosureReview required separately and liveGamePlayback not performed.

## Verification actually run

-31/31 Python tests passed against isolated proposed tool files; python-test.log
-8/8 new TypeScript surface playback tests passed against the unmodified actual repository implementation; ts-playback-test.log
- Mutation test intentionally restoring the old nonloop-only manifest gate failed all four ambient cases; mutation-test.log. This was an expected regression detection, not a passing suite
- git apply --check accepted the proposal against the main checkout at audit completion
- Main source was not modified by this audit. No full worktree was created
- Full app build, complete TypeScript suite and typecheck were not run here; parent owns integration verification
- No browser texture rendering, generated art playback or live campaign deployment was tested. jsdom verifies declared URL/geometry and frame-window scheduling, not the loaded image pixels

## Reproduce / integrate

The patch uses repository-relative paths. First review it, apply it in the parent-owned checkout, then run:

    python -m unittest discover -s tools/art -p 'test_fx_overrides.py' -v
    npx vitest run packages/game-ui/src/ui/__tests__/candidate-ambient-fx-playback.test.ts packages/game-ui/src/ui/__tests__/candidate-fx-playback.test.ts packages/story-candidate-01/src/presentation/candidate-01-fx.test.ts
    npm run typecheck

The checked standalone harness is also retained under harness/ and points to the actual read-only source checkout. Its cache is outside main. proposal/tools/art/export_overrides.py is an unchanged dependency copy for running isolated tests; it is deliberately not included in the patch.

Before registering generated FX09 or later topics, extend the reviewed phaseOrders in candidate-01-fx.test.ts with the agreed names, then register source paths/hashes, produce/check both exports and record a formal export report. The current patch deliberately adds no catalog entry. Do not mark those entries approved until visual transparency, shared registration and native12fps4→1 continuity reviews pass. No scene consumer is added by this work.
