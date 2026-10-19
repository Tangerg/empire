# Named hero appearance contract, unactivated

This checkpoint introduces a presentation-only appearance catalog and a single resolution path. The production catalog is empty and no `appearanceId` is bound. It does not correct the seven currently mismatched/generic hero portraits or claim any new artwork is complete. The identity audit remains [the seven-person contract](../narrative-family-audit/hero-identity-contract.md).

## Owners and consumers

- `candidate-01-bindings.ts` remains the sole rule-unit → art binding owner. Every old string value becomes `{ topicId }`; future reviewed named units may add `appearanceId`
- `manifest-final-fantasy-v1.json` still owns canonical identity/category/footprint. Its 404 topics and all existing asset/HD/team records are unchanged. An appearance is not a canonical topic and adds no unit/rule/count
- `manifest-hero-appearances.json` owns named sheet registration, with schema version 1 and separate `appearances` / `teamVariants` arrays
- `candidate-01-hero-appearances.ts` validates the document and resolves explicit bindings. `candidate01UnitPicture` uses this result; BoardView's SVG/GPU picture projection, unit icon, and unnamed-unit portrait fallback all consume that same picture
- Named character cards still use `CANDIDATE_01_CHARACTER_ART` and are unchanged. Character masters must be genuine independent RGBA sources; any card background is a separate source/layer. An opaque UI card is not proof of a transparent character master

## Resolution order

1. An explicit `appearanceId` must exist and reference exactly the binding's `topicId`; failure is an error, never a generic-face fallback
2. For that hero, use an explicitly registered `(appearanceId, teamColor)` sheet when present; otherwise use the hero's base appearance
3. Only a binding with no `appearanceId` uses the existing generic team-variant/base resolver, unchanged

Named sheets retain authored palette. The existing team ring/ownership presentation remains. A generic red swordsman sheet cannot override a named hero. Team colours compare case-insensitively, and duplicate colour keys are rejected. Broken explicit bindings are validated on startup, even if the unit is absent from the current mission.

## Source registration contract

Each appearance has `id`, `topicId`, and the following complete sheet registration:

```json
{
  "id": "laiya-stage01",
  "topicId": "C01-UNIT-BANNER-GUARD",
  "png": "runtime-approved-v5/hero-appearance/laiya-stage01.png",
  "hdPng": "runtime-approved-v5/hero-appearance/laiya-stage01@2x.png",
  "pixelDensity": 2,
  "frameWidth": 104,
  "frameHeight": 64,
  "frames": 4,
  "frameOrder": ["idle", "walk-a", "attack", "walk-b"],
  "anchor": [52, 54],
  "hdWidth": 832,
  "hdHeight": 128
}
```

These numbers demonstrate the schema only. The example pivot is NOT an accepted Laiya contact registration. Current BANNER-GUARD is 104 × 64 logical, but the reader does not fix hero canvas width/height to that value or to 96 × 64. The four actual native results decide a shared body scale, transparent extent and registered pivot. A mounted Roderick may need a wider canvas without changing rule occupancy or the canonical footprint. Ivra's source must retain the juvenile story stage; no automatic age progression is added.

`teamVariants` contain the same sheet fields, replacing `id` / `topicId` with `appearanceId` / `teamColor`. Each has independently explicit geometry and dedicated paired PNG paths; it inherits identity/footprint through its appearance, not through an unrelated generic troop. Both density URLs must exist in the Vite graph. HD dimensions must equal logical full-strip width/height × 2. Four semantic roles are exact and complete; pivots must lie inside a frame. Duplicate IDs/paths, unknown topics, noncombat topics, category/footprint overrides, wrong order, incomplete poses, bad geometry and absent URLs are refused.

### Activation gate

The reader validates declarations and runtime availability; it cannot infer the depicted person's identity or pixel alpha from a URL. Before adding any real entry:

1. Preserve the full native RGBA portrait and four separate action sources, exact hashes, genuine alpha, and the named face/age/equipment from the identity audit. No opaque master/background extraction
2. Export through the existing registered-sequence process, preserving complete canvases and recording one shared scale/pivot with per-frame alpha/contact QA. Source hashes and `sequence.json` must accompany registration
3. Verify actual logical/HD dimensions, RGBA and nonempty transparent margins for every pose, complete weapon/flag/horse contours, and consistent feet. The runtime-file test checks PNG dimensions and RGBA container; container type alone does not prove real transparency
4. Review all four frames and the full actual level at gameplay scale; verify dialogue/roster/HUD identity against the independent transparent portrait/card layers
5. Add only the reviewed complete record and its explicit named binding. Update the intentional empty-catalog guard as part of that activation review; never suppress invalid-reference tests

No image generation, card exporter, alpha removal, source processing, rule changes, or incomplete placeholder source activation occurs in this patch.

## Verification and reproducibility

The checked baseline is `d7b161a6ef06c0d63a615f66d03dd78d578e2bf8` (the integrated stone-material checkpoint). The retained comparisons cover:

- All 39 unit bindings × all 3 actual team colours, including generic authored red: complete `BoardPicture`, icon markup hash, source URL/hash and frame geometry
- All 16 actual BoardView/SvgBoardSurface initial SVGs, with actor/structure drawing counts, viewport and unchanged state hashes
- All 16 pure scene-layer and editor-ground contracts
- The complete 60-run replay event/outcome stream
- Canonical asset, tactical-HD and generic-team manifest hashes

[Checkpoint source verification](source-checkpoint-verification.json) checks all 32 distinct bound-unit PNG hashes against the immutable Git baseline, rather than assuming an evolving shared artwork tree is unchanged. [Comparison](comparison.json) records exact equality results. [Validation](validation.json) distinguishes tested contracts from live rendering. SVG/GPU projection contract tests run, but this is not live browser/GPU pixel evidence.

Reproduce from repository root:

```sh
HERO_CONTRACT_OUTPUT=/tmp/hero-pictures.json npx vite-node tools/art/review_hero_appearance_contract.ts
HERO_SCENES_OUTPUT=/tmp/hero-boards.json npx vite-node tools/art/review_hero_scene_contract.ts
CONTRACT_OUTPUT=/tmp/hero-layers.json npx vite-node tools/art/review_candidate_scene_contract.ts
npm run --silent sweep > /tmp/hero-replay.txt
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-hero-appearances.test.ts packages/story-candidate-01/src/presentation/candidate-01-assets.test.ts packages/story-candidate-01/src/presentation/candidate-01-runtime.test.ts --maxWorkers=1 --minWorkers=1
npm run typecheck
```

The two hero review tools only render/read and write their requested evidence outputs. `HERO_SCENE_SVG_DIR` optionally retains the complete actual SVGs as gzip files. The picture tool's baseline used the old string binding shape and its after version reads the explicit object; the serialized topic identity stays identical.
