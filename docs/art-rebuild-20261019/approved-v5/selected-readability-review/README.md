# Selected-actor readability prototype

**Actual complete c01-02 BoardView / SvgBoardSurface, rendered offline at display 1x.** This is not a live-browser screenshot or GPU-playback claim. All scenes retain the production 852 × 584 viewport, all 9 buildings, all scenery and all 20 actor nodes. Art is the approved new/legacy mixture at integration checkpoint `5d72a1a`; exact input hashes are in `selection-provenance.json`.

## Review

`selection-comparison-detail.png` shows four paired details: existing selection on the left, the new cue on the right. Details are actual 1x pixels enlarged 2x with nearest-neighbor sampling, not new high-density art. Every detail has an accompanying complete scene SVG and `@1x.png`.

1. **Tree:** actor 11, `bridge-cleric`, foot `(80,319)`, substantially behind `scenery:29` / `mixed-forest-edge`
2. **Granary:** actor 5, `bridge-guard-1`, foot `(48,351)`, substantially behind `terrain:5` / the granary
3. **Unobstructed:** actor 4, `bridge-knight`, foot `(112,255)`
4. **Hidden + selected:** actor 11 deliberately hidden through `BoardOverlay.hiddenUnits`; the normal selection ring remains existing UI behavior, but no actor silhouette is emitted. There are 19 visible actors and 20 retained actor nodes in this negative case

The muted-gold cue was reduced from the initial 18% prototype to **12% opacity** to limit color wash on unobstructed art. It is a subtle selection location/shape cue, not a promise that fully covered character details become legible.

## Scope

- Only `selected && !hidden` produces a cue. This is **not general occlusion detection** and does not give all visible units an x-ray treatment
- Normal actor artwork stays in canonical footpoint order. Trees/buildings are not faded, omitted or moved
- The cue is at the start of the existing effects plane, above standing art but below normal effects and the cursor. Existing selection glow/brightness and ring remain
- SVG copies the current clipped sprite window and advances it from the original frame callback. Pixi references the original current-frame texture. Neither registers a second timeline or performs another bake. Body-only generic art uses its existing body alpha
- Cue placement, nudge, swell, facing and opacity follow the original drawing. Deselect, hide, remove and dispose clean it up
- No gameplay state, facing, level cells, source pixels or animation identity changes

`selection-provenance.json` contains input hashes, actor IDs/feet/facings, visible counts and unchanged state hashes. `selection-raster-validation.json` compares complete scene pixels and verifies that the hidden-selected negative pair is exactly equal. Positive pairs differ only where the selected cue paints.

## Verification

- TypeScript: pass
- Complete suite: **96 files / 861 tests pass**
- Focused architecture + SVG/Pixi checks: **125 tests pass**
- All four app builds: pass
- 60 shipped-level/profile replay runs: byte-identical to baseline, SHA-256 `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`
- Tests cover state-order safety, actual hiddenUnits overlays, current-frame/texture reuse, movement and visual offsets, body-only art, no extra bake/timeline, cleanup and preserved depth
- Browser/GPU pixel equivalence and moving-scene visual acceptance are still not claimed

## Reproduce

```sh
npx vite-node tools/art/review_selected_readability.ts
python tools/art/render_selected_review.py docs/art-rebuild-20261019/approved-v5/selected-readability-review
npm run typecheck
npm test -- --maxWorkers=2 --minWorkers=1 --testTimeout=20000
npm run build
npm run --silent sweep > /tmp/selected-readability-sweep.txt
```

The 20-second test budget accommodates cold dynamic imports in the architecture guard. No semantic assertion is weakened.
