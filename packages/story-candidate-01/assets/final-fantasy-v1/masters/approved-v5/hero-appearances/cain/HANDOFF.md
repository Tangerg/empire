# Named Cain stage01 source handoff

All four battle poses and the original-baton transparent portrait were accepted by the coordinator. Source delivery is complete.

## Integration contract

- Battle: c01.cain selects cain-stage01 as a presentation appearance under existing C01-UNIT-LEGION-SHIELD. Generic c01.legion-shield stays unchanged
- Portrait: existing C01-CHAR-CAIN-01, original board01 cell2, preserves the short command baton
- Battle uses the rules-supported spear. Cain remains foot/heavy/infantry; do not infer a horse from knight-charge
- No stage02, new canonical topics, rule changes, repository edits or Git operations

## Battle geometry

- Native common canvas: 1586 × 992; attack and walk-b each need one transparent column on the right during export only
- Shared uniform scale: 44 / 912.5
- Frame order: idle, walk-a, attack, walk-b
- Source feet: [681, 966], [646, 960], [531, 928], [709, 951]
- Suggested frame: 104 × 64, origin [8, 4], registration limit 8 accepted; maximum measured offset is 7.233 logical pixels
- Full canvases fit. No silhouette fitting, alpha trim or per-pose scale
- Attack spear butt is naturally occluded by torso/shield; full thrust and complete tip were accepted

## Portrait geometry

- Native source: 1024 × 1536 RGBA
- Whole-canvas uniform composition only; 192 × 224 and 92 × 112 diagnostics are in narrative/char-cain-01/native-card-review.png
- Background is a separate coordinator-owned layer

## Native source hashes

- idle: df533ea0e65c555d7c0ec22bb4578b9d1c9ef4a85185afafb765511094481c0d
- walk-a: 4fc84d833104f3a79ea114aa6f57d1a0a361542dac16f114ff75087a0bde39e6
- attack: 5272a25476e86e984b46686bdc01910b45fd6a9d0afe05dfb45d84afa524db25
- walk-b: f3d0d949498c1adf08cc9cab49a99423e1ca865b0cec39984ebeea085d4f0189
- char-cain-01: 94c05055a9a4a150b359c2290dec6d13ff386272da9628b27cc1b36001edea17

All five raw/master pairs are byte-identical to their native built-in image outputs. Exact prompts, requests, source references and hashes are verified in final-delivery-verification.json. Native perimeter alpha is at most 1, with no pixels above 1; no alpha cleanup, recoloring or source cropping occurred. QA previews are derivatives only.
