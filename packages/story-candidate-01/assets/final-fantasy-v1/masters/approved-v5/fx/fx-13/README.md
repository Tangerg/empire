# FX13 scorched embers: nonloop source candidate

Four independent 1254 RGBA phase masters are ready with preserved raw outputs, exact prompts, reference snapshots and hashes. Parent approved the exact phase names and first pair; final quartet static review was accepted on 2026-10-20.

- `integration-catalog.json`: proposed repository source destinations and exact hashes
- `final-quartet.json`: verified existing staging paths
- `final-review.json`: acceptance state and all QA gates
- `qa/full-and-terminal-labeled.png`: full sequence and terminal peak→residue transition
- `qa/native-32-64-six-backgrounds.png`: native output at both densities
- `qa/all-source-black-white.png`: whole-source transparency inspection
- `qa/PHASE-REVIEW.md`: source semantics, registered earth and visible phase changes
- `qa/motion-and-registration-proof.json`: measured contact and phase diagnostics
- `qa/apng-timing-verification.json`: exact rational timing and decoded-frame equality
- `qa/12fps-once-transparent@1x.apng` and `@2x.apng`: single-play, four equal frames

Keep the established manifest contract: 12fps, loop=false, normal blend, anchor [16,16]. The common 28/1254 whole-canvas transform is used for every phase. No alpha edits, per-frame fit or synthesized motion were applied. Source edges at alpha 1 are conditionally proved clean; final 2px logical padding is alpha 0.

No 420ms hold/removal rule was added. Future consumers own lifetime. No browser-observed playback or live scene integration is claimed.
