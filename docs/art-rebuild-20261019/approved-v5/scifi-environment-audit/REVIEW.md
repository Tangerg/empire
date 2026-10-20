> Integration correction, 2026-10-20: [48,91] is the atlas default and each unregistered cell's legacy anchor, not a frozen approved-source contact requirement. Ordinary cells are contact-calibrated by the existing exporter (lines199–215); the atlas default stays unchanged. Do not force a new source to the default or regenerate solely for that purpose. Existing stone-wall-straight is already [48,64.3604465709729].

# Environment source audit: C02 premise and next C01 quartet

Audit date: 2026-10-20. Scope: repository files and original atlas inspection, including the complete environment catalog outside the 1,212 canonical topics. Read AGENTS.md and PROJECT_RULES.md. No image generation, source registration, runtime changes, commits, exporter execution, or test execution. Only this review document was written.

## Decision

There is no existing C02 environment atlas contract in this worktree. Do not manufacture four C02 environment IDs or a new consumer. Following the integration owner's corrected scope, the next recommended four independent C01 sources are **palisade-straight, palisade-corner, palisade-gate, and palisade-breach**, existing indices 6–9 of `crossings-fortifications`.

These are **catalog-only decorative source replacements**, not four newly delivered gameplay features. The existing environment exporter supports all four already. No additional exporter, registry, semantic owner, scene selector, or gameplay placement is needed.

## C02 evidence and boundary

- `packages/story-candidate-02/assets/final-scifi-v1/manifest-final-scifi-v1.json` is a 404-topic canonical manifest, not an environment-builder manifest. Its 12 categories include 32 terrain, 32 battle-prop and 24 interactive-structure topics; there is no environment category or environment atlas collection.
- No C02 environment-builder directory or C02 `EnvironmentCatalog` construction exists. `packages/story-candidate-02/src/index.ts` exports a seven-chapter campaign contract, not a concrete decorative scene implementation.
- All 32 C02 battle-props already have entries in `tools/art/source-overrides.json`. Recasting four as new environment sources would duplicate existing work/identity.
- C02 terrain has its own `tools/art/canonical-terrain-overrides.json` owner (24 C02 entries at audit time). Its test explicitly requires `environmentCellCountIncrement` to be zero. Material variants/connected masks are not new decorative drawings.
- C02 interactive structures are a different contract: `build_final_scifi_v1.py:310–327` reads six 3×4 original boards, three state columns per topic; outputs 3×64×64 frames, anchor `[32,63]`, footprint `[2,2]`, states normal/damaged/captured. They must not be silently converted to single decorative environment cells.
- The actual environment manifest is exclusively `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json`: 36 atlases and 1,188 components. The historical `docs/art-rebuild-20261019/environment-atlas-manifest.json` also contains those same 36 C01 atlas paths. Historical status strings there are not completion authority.

## Coverage check, not acceptance

All ten module atlas families were inspected, not just canonical topics. Current `environment-overrides.json` contains 155 cell overrides: all 16 each in forest-existing, forest-temperate, scenery-regional, rural-life, camps-foundations, landmarks-large, cliff-modules-temperate and cliff-modules-highland; all 24 decals-small; and three stone cells in crossings-fortifications. This count is not the integration owner's broader 186 independent environment-source total: other material/crossing pipelines are separate. Neither count proves scene acceptance.

The only module family with unregistered cells is crossings-fortifications. Its registered cells are stone-wall-straight (10), stone-wall-corner (11), and stone-gatehouse (12). Some remaining bridge slots have separate registered crossing representations, so absence from this particular registry is not sufficient to call those bridge subjects missing. The four palisade slots avoid that ambiguity and form a coherent original source family. No independent palisade master directories or alternate registration entries were found; the separate `landmarks-large/border-palisade-fort` is an already completed, different large-landmark identity.

## Original complete atlas identity

All paths below are relative to the repository root.

Original full board, visually inspected in its entirety:

`packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/crossings-fortifications-4x4-key.png`

- RGB, 1254×1254, 4 columns × 4 rows, magenta-key background.
- SHA-256: `62de4cfea9af8197be6a62a7e567fa1ba8475654d3660e16ec19e473bff73d19`.
- Original alpha extraction: `environment-builder-v1/intermediate/alpha/crossings-fortifications-4x4.png`, RGBA 1254×1254, SHA-256 `967de5c5a2b13df39e9cd260d642e5d0b34abb4c8c720878cfdecd5ff3691733` (same C01 pack prefix).
- Original builder: `environment-builder-v1/tools/build_environment_builder_v1.py:445–475`, reads that alpha sheet as 4×4, fits into 192×192 HD cells with padding 10, and derives semantic order from SEMANTIC-CATALOG.json.

The immutable baseline atlas was also visually inspected, not substituted for the full original board:

- `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/baselines/crossings-fortifications.png`, 384×384, SHA-256 `43e02953833b91f400684b8ce30db4a855283148ceff2235311591799de1e3ab`.
- Same directory `crossings-fortifications@2x.png`, 768×768, SHA-256 `3d7bdd4b141721c668256b9d378b6ff833cd979e2f317c3078b472ee8cf27fd1`.
- These are existing checksummed baselines in `environment-overrides.json`. Preserve them; do not replace them with the currently overlaid runtime atlas.

## Exact next source targets

Coordinates are zero-based index and half-open XYXY boxes. Row/column descriptions are one-based. Original-board boxes follow the builder's rounded quarter-grid boundaries; they are identity evidence, not proposed trimming instructions.

| Existing ID | Index / location | Original board box | 1× runtime slot box | Footprint |
| --- | --- | --- | --- | --- |
| palisade-straight | 6 / row 2, column 3 | [627,314,940,627] | [192,96,288,192] | [2,1] |
| palisade-corner | 7 / row 2, column 4 | [940,314,1254,627] | [288,96,384,192] | [2,2] |
| palisade-gate | 8 / row 3, column 1 | [0,627,314,940] | [0,192,96,288] | [2,2] |
| palisade-breach | 9 / row 3, column 2 | [314,627,627,940] | [96,192,192,288] | [2,1] |

All four inherit the exact current contract: 96×96 logical cell, 192×192 HD cell, current per-cell anchor `[48,91]`, `obstructionHeight: 1`, `tileMode: modules`, category `structure-kit`, atlas `ySort: bottom-anchor`. No presentationScale is declared, so the catalog/default semantics remain 1. HD slot boxes are exactly twice the listed 1× coordinates.

Subject preservation from the full original board:

1. **palisade-straight**: one continuous straight fence of eight pointed wooden stakes, two horizontally tied rope bands; muted brown wood, darker lower sections, no masonry or gate. Preserve the complete silhouette and row orientation.
2. **palisade-corner**: one joined V-shaped/return wooden stake fence with two rope bands and a readable central bend, not a curved straight fence or a freestanding barricade. Preserve the original perspective and joined corner.
3. **palisade-gate**: one closed double wooden door between two tall timber posts on chunky gray stone footings; dark metal straps and two ring handles; two blue-gray hanging banners with pale cross-like emblems, one on each outer side. Keep the gate closed and both banners; do not turn it into the separate stone gatehouse or an open pass-through.
4. **palisade-breach**: one broken gap through a wooden stake fence, tall surviving end stakes, snapped inward timber, two fallen diagonal timbers and low gray rubble at the opening. Preserve obvious breakage and gap; do not deliver an intact shorter fence or the separate crossed-spike wood-barricade.

Proposed future master destinations, using the existing naming convention (none created):

- `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/crossings-fortifications/palisade-straight/palisade-straight-master.png`
- Same directory pattern for `palisade-corner/palisade-corner-master.png`.
- Same directory pattern for `palisade-gate/palisade-gate-master.png`.
- Same directory pattern for `palisade-breach/palisade-breach-master.png`.

Do not prescribe sourceFoot coordinates before inspecting the actual new sources. `[48,91]` is the current authored catalog anchor, not permission to infer a new object's foot from its bounding box.

## Consumers and minimal integration

`packages/story-candidate-01/src/presentation/candidate-01-environment.ts:62–88` imports every environment atlas at both densities and constructs `CANDIDATE_01_ENVIRONMENT` from the single manifest. `packages/game-ui/src/art/environment-catalog.ts` resolves the semantic cells and exposes their exact index, footprint and anchor. Thus all four already have a real catalog consumer.

Searches across C01 source and authored scene JSON found no gameplay selector/placement for these four palisade IDs. The existing fortress selector in `candidate-01-terrain-materials.ts:178` selects only the three already registered stone sprites. Do not add the palisades there merely to obtain a scene screenshot; do not change occupancy, passability, map rules, bridge logic, selection weights, or depth ordering. Native atlas/catalog review is the truthful completion level for this quartet until separately authored placements exist.

Use `tools/art/export_environment_overrides.py` unchanged:

- Its supported contracts already include crossings-fortifications `(4 columns, 4 rows, 96 logical, [48,91])`.
- `FORTIFICATION_SLOTS` already fixes exact labels/order, including these four IDs. The exporter verifies manifest slot order and explicit index equivalence.
- Add only four entries under existing `environment-overrides.json` → `atlases` → `crossings-fortifications` → `cells`, each source path, SHA-256 and registration `{scale, origin, sourceFoot}`.
- Reuse the existing immutable 1×/2× baselines. Whole authored RGBA canvas must fit at both densities, with no inferred alpha crop. Origins use nonnegative half-logical-pixel steps.
- Existing `render_cell` computes anchors from the actual rounded HD transform and measured sourceFoot. This is the existing single anchor owner; do not introduce a second registry or manually divergent anchor calculation.
- Existing exporter overlays only these cells, preserves all unmodified cells pixel-exactly and exports to the current 1×/2× atlas destinations. Manifest IDs, footprints, obstruction heights and layout stay unchanged.

## Contract blockers / required verification when implementation begins

1. **Known test expectation must change with legitimate expansion.** `packages/story-candidate-01/src/presentation/candidate-01-crossing.test.ts:39–49` currently asserts exactly the three registered stone IDs. Update it to the legitimate seven-cell set when the four masters are integrated, while retaining explicit bridge exclusion and immutable-baseline hashes. Do not delete/weaken the protection or touch it during this audit.
2. **No existing gameplay placement.** Catalog-only delivery is allowed by the full-inventory remit, but it cannot be reported as live gameplay integration or scene acceptance. Current placement absence is not a reason to invent a consumer.
3. **Actual masters/registrations not yet authored.** Measure and record contact point and full-canvas transform after generation. Validate true transparent background, opaque interiors and clean native silhouettes without deleting identity-bearing wood, banners or breach geometry.
4. **Finite source counts are not acceptance.** Keep the four independent sources separate from density outputs and catalog component counts. The quartet leaves all connected-surface/elevation/bridge issues outside its scope.

Recommended later checks: focused environment exporter tests; exporter `--check`; focused crossing test after its intended expectation update; unchanged-cell pixel hashes covering the other 12 slots at both densities; source SHA and manifest identity checks; original full-board/new native 96/192 visual comparison. Do not claim a live-browser pass without one. This audit ran none of these implementation checks and made no runtime changes.
