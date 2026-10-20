# FX15 silver-forest spirit fire

Four independent 1254 RGBA phase masters are ready with raw outputs, exact requests, reference snapshots and hashes. Exact phase order and the first pair were accepted by the parent; final quartet static review was accepted on 2026-10-20.

- `integration-catalog.json`: proposed repository destinations and exact hashes
- `final-quartet.json`: verified current staging paths
- `final-review.json`: acceptance state, gates and caveats
- `qa/full-and-terminal-labeled.png`: full sequence and peak→residue terminal progression
- `qa/native-32-64-six-backgrounds.png`: native output at both densities
- `qa/all-source-black-white.png`: whole-source transparency inspection
- `qa/PHASE-REVIEW.md`: leafy spirit semantics and fixed-bed review
- `qa/motion-and-registration-proof.json`: actual contact and phase diagnostics
- `qa/apng-timing-verification.json`: rational timing and decoded-frame equality
- `qa/12fps-once-transparent@1x.apng` and `@2x.apng`: single-play, four equal frames

Keep the manifest contract: 12fps, loop=false, normal blend, anchor [16,16]. Every phase uses the same full-canvas 28/1254 transform. No alpha edits, per-frame fitting or synthesized motion were applied. Source alpha-1 perimeter samples have clean composite evidence; final 2px logical padding is alpha 0. The leaf-bed contact variation is disclosed as an asset-specific static-review caveat.

No 420ms hold/removal was added. Future consumers own lifetime. Browser-observed playback and live scene integration remain unobserved.
