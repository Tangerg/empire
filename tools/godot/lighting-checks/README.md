# Source lighting and composition contracts

Run `bash tools/godot/lighting-checks/run.sh`. The runner calls the existing TypeScript scenery adapter for all 16 canonical fields, builds the existing native project, and runs `LightingChecks.tscn` headlessly. It does not use the desktop, install software, rewrite artwork, or execute a gameplay simulation.

## Source owners and native composition

- `candidate-01-map-scene.ts` owns settlement/bridge-head light pools and field sunlight. `CandidateBoardLighting` retains the source gradients, positions, stops, and alpha. Terrain places hearths before sunlight, both below foot-sorted standing figures. Hearth spill is deliberately not clipped; sunlight uses the existing playable-shape mask.
- `runtime-raster.ts` and `candidate-01-board-style.ts` own the unit contact ellipse and team ring. `CandidateBoardLighting.UnitBody` composes their exact source geometry and CSS overrides. The contact opacity is 0.62, replacing rather than multiplying the SVG presentation attribute 0.48.
- `svg-board-surface.ts` places the body and current clipped strip inside one figure, excluding badges. `CandidateSpriteShadows.CreateFigure` follows that ownership. It composes contact, ring and current frame first, then applies the figure filter once. Frame changes explicitly synchronize this composition. The original sprite retains its texture, foot-relative offset, facing scale, metadata and depth, but its own draw is suppressed so art and shadows are not doubled.
- `CandidateFilterComposition` is the single source-over/cutout/filter composition owner. A filtered drawing includes original RGB and the chained shadow result in one texture. Placement opacity therefore applies after composition, rather than independently to overlapping source/shadow draws. Original source images and files are never modified. The outer unit is a native `CanvasGroup`; death/spawn fades use its `SelfModulate`, preserving source whole-unit opacity while badges remain outside the figure filter.
- `candidate-01-crossing.ts` owns the registered junction union. `CrossingRenderer` supplies the two explicit source registrations and cutouts to `CandidateCrossingLighting`. Each axis is clipped before their source-over union, and the standing filter is applied once afterward. Original texture/shader handles remain hidden and inspectable. The result retains the registered pivot and has no duplicate per-axis shadows.
- Structures receive the source contact ellipse, not an invented silhouette filter. Their owner banner retains its independently declared source filter. Existing rule-derived structure drawings already contain their contact and are not shadowed twice.
- Authored environment placements use their explicit `standingShadow` contract. Authored frame placements emit the source `is-scene-frame` class and therefore use the foreground preset. Region names and art filenames never select the stronger generated perimeter-edge preset.

## Lifetime and tests

One explicit `CandidateShadowCache` belongs to each board. It shares original-frame compositions, team-dependent figure bodies, registered junctions, and exact procedural banner sources. Repeated projections do not create new banner cache identities. Changing a frame, team, registration or cutout changes the relevant cache identity. Board replacement/disposal releases the cache; disposal is idempotent and permanently rejects further allocations. Structure contacts are cached by their declared geometry, rather than recreated per projection. Unshared contact and field-light nodes explicitly own and dispose their texture wrappers; there are no process-global caches.

The suite checks all pool geometry and sunlight stops against the live TypeScript adapter; all filter declarations against live CSS/SVG; contact/ring source geometry; atlas-frame isolation; chained-filter alpha; ordered source-over RGB/alpha; postcomposition opacity; complementary cutouts; immutable source pixels; figure team/frame cache identity; hidden original draws; native junction registration; board depth; and animated frame restoration. Existing `AnimationChecks` separately protects unchanged death/spawn timing and the new whole-group fade property. Deliberately changed source figure sigma and team-ring radius were rejected by the oracle comparison.

## Remaining source paths and visual gate

This bounded checkpoint does not wire the route/route-edge offset filter (`candidate-01-board-style.ts:29`, `candidate-01-map-scene.ts:362–371`) into the native connected-material compositor. The action/selection/cursor filter (`board-style.ts:61`), marker opacity (`board-style.ts:160`), and illustrated cover props (`candidate-01-runtime.ts:215–220`) are separate remaining native presentation paths. Strong generated perimeter shadows cannot be claimed while generic perimeter scenery is absent. Their declared presets alone are not implementation coverage.

Headless checks establish source parameters, ownership, resource use, cache behavior and mathematical composition, not visual acceptance. The native Gaussian uses four-sigma support, and geometry uses subpixel coverage; these are not claimed pixel-identical to browser-specific rasterizers. Live native inspection is still required for filter softness, transformed prop edges, junction cutout seams, CanvasGroup fades and overall lighting balance. No new art, removed saturation/contrast grading, dynamic light simulation or audio is introduced. Generic perimeter scenery and non-lighting presentation gaps are separate from this checkpoint; this is not an all-campaign visual-completion claim.

CSS `drop-shadow` uses its third length as Gaussian standard deviation, unlike `box-shadow` blur radius: [W3C Filter Effects 1, section 6.1](https://www.w3.org/TR/filter-effects-1/#funcdef-filter-drop-shadow). The original `board-surface.ts` contains a misleading half-radius comment; its actual SVG constants are preserved unchanged, independently of the CSS presets.

## Performance acceptance

The recorded cold board composition took approximately 4.9 seconds in this cloud
CPU environment; the immediately repeated cached projection took about 4 ms.
These timings are not a desktop frame-rate acceptance. Cold composition and
frame-change shadow-cache behavior must be profiled in the real application;
correct source filter parameters alone do not establish responsive presentation.


## Shutdown regression gate

A combined checkpoint once printed `Animation presentation checks passed` and then emitted four unsafe `ImageTexture` references and crashed during shutdown. Identity tracing matched all four IDs to the final uncached structure-contact textures, not figure filters or CanvasGroup buffers. Those textures now have explicit board ownership; standalone procedural light nodes use `OwnedProceduralSprite` and detach/dispose their own texture wrappers before deletion.

Run `bash tools/godot/lighting-checks/check-lifetimes.sh [absolute-godot-project-directory]`. The optional project path supports an isolated exact-tree copy without changing the source tree. It records actual process exit codes and rejects any resource-leak/error diagnostics even after a success marker. It runs AnimationChecks six times, LightingLifetimeChecks three times, existing real-session screen binding checks, and the lighting oracle contracts. Lifecycle checks cover actual save→JSON→load, repeated projection, rebind during movement, a new-session restart, stale callbacks after unmount, queued scene deletion, cache release ordering, explicit disposal of standalone lights and forced managed finalization. Native visual acceptance remains separate.

## Procedural resource ownership

Generated SVG badges and terrain masks now use `OwnedProceduralSprite`; cache-backed
sprites continue borrowing their board-owned texture. `OwnedShaderMaterial` owns
only its generated material and shader, never texture parameters borrowed from
terrain or imported resources. Removing a ground clip or deleting its sprite
releases those wrappers explicitly. Capture banners retain their existing visual
composition and now dispose their exclusive texture through the same owner.

`LightingLifetimeChecks` additionally checks gauges, banners, ground-clip removal,
borrowed-texture survival, and real terrain/material teardown before managed GC.
Run `bash tools/godot/lighting-checks/lifetime-mutants.sh` to prove the lifetime
assertions reject removed texture disposal, removed material disposal, and an SVG
path that bypasses its owner. The isolated runner requires a clean baseline;
compilation failures and process timeouts are not successful mutations.

Construction is also transactional: borrowed material inputs are validated before
allocation, and the procedural sprite factory frees its node and exclusive
resources if configuration fails. A focused native check retains the failed
instances and verifies invalidation, borrowed-input survival and propagation of
the original exception. `lifetime-mutants.sh construction` runs that baseline and
one deliberate missing-cleanup regression, without repeating the full lifecycle
matrix. It does not rely on global orphan counters.

These checks establish resource ownership, not completion of the broader native
quality audit. Catalog/composition, live-unit invariant ownership, application
admission, localization, metadata consistency and formatter enforcement are
separate work in progress.
