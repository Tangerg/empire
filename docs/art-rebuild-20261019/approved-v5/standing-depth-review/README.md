# Standing depth: complete c01-02 scene

These are **offline captures of the actual `BoardView` / `SvgBoardSurface`**, rasterized with Inkscape at display 1x and 2x. They are not live-browser screenshots or GPU-playback evidence. The production viewport is 852 × 584; the tactical map remains 22 × 14 cells.

## Compare

- `runtime-legacy-order-full@1x.png` / `@2x.png`: diagnostic restoration of the former fixed static-layer order
- `runtime-standing-full@1x.png` / `@2x.png`: shared footpoint order
- Both contain **all 9 buildings and 20 actors**, all 46 original scenery pieces, ground, roads, registered horizontal crossings, scene lighting, and the full production frame. There are no selected omissions. The art is the current approved new/legacy mixture, not an all-new set.
- The same placements, sizes, sources, facings and initial poses appear in both variants. Only static depth opt-in is disabled in the diagnostic legacy variant. The original standalone SVGs include the actual app stylesheet. Offline rasterization converts its facing mirror into equivalent SVG geometry and preserves nested sprite-sheet clips.

The right-middle keep doorway is no longer covered by the haystack whose foot is in the row behind it. Nearer trees correctly cover actors behind them. Some actors are substantially occluded; this remains a tactical-readability issue, not a justification for dropping scenery, lifting every actor, or revealing rule-hidden actors. The perimeter frame also remains exactly as the production scene draws it.

## Contract and evidence

`BoardPiece.foot` opts a static picture into the existing actor plane. The single `BoardDepthOrder` owner merges 9 buildings, 41 standing props, and 20 persistent actors by world contact y, then x, then stable source-layer/index or actor id. Buildings and actors use the registered cell contact `(16,31)`; scenery already placed at its contact uses `(0,0)`. Source image bounds and sprite top-left coordinates do not control depth.

The 3 flat crossing decks and 2 lights remain in their original scenery plane. Unannotated pictures retain their original fixed layer and order. Range/path/markers remain below the standing plane; foreground/effects/cursor remain above. Actor `place` changes depth; `nudge` and `swell` do not. Replacing a static layer does not recreate or reset an actor or its animation.

- `runtime-scene-provenance.json`: actual source/display counts, every actor id/cell/facing/foot, ordered static source ids and contacts, source hashes, identical state hashes
- `building-contact-bounds.json`: registered anchors, presentation scales, all nine source-frame alpha bounds, and resulting world bounds
- `actor-occlusion.json`: initial-pose alpha-mask analysis of each actor sprite and the actual later drawings that cover it; source-layer/index ids identify procedural props, with atlas cell ids where applicable. This is a diagnostic, not a gameplay visibility rule

Source checkpoint: integration `d768b14` plus depth implementation `47c06ee`. New roads/water and registered horizontal crossings are included. No gameplay, level cells, actor facing, source image pixels, or collision geometry changed in this checkpoint.

## Verification

- TypeScript: pass
- Complete suite: **96 files, 855 tests pass** (`npm test -- --maxWorkers=2 --minWorkers=1 --testTimeout=20000`). The extended budget accommodates the architecture guard's cold dynamic imports; no semantic assertion is weakened
- All four application builds: pass (`npm run build`)
- SVG/Pixi contract tests include complete actual c01-02 population, actor movement across multiple objects, equal-foot deterministic ties, visual nudge/swell independence, static replacement/removal, retained unit identity/timeline, fixed ground and overlay ordering, and absent-foot legacy compatibility
- Final integrated sweep: all **60 shipped-level/profile runs byte-identical** to the pre-depth baseline. Event/outcome file SHA-256: `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`
- Both complete 1x scene images visually checked. Browser/GPU pixels and animated tactical visibility are not claimed

## Reproduce

From the repository root:

```sh
npx vite-node tools/art/review_standing_depth.ts
python tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/standing-depth-review --scene-only
python tools/art/review_actor_occlusion.py docs/art-rebuild-20261019/approved-v5/standing-depth-review
npm run typecheck
npm test -- --maxWorkers=2 --minWorkers=1 --testTimeout=20000
npm run build
npm run --silent sweep > /tmp/standing-depth-sweep.txt
```

The capture uses the existing board harness and the shared depth owner; it is not a separately hand-composed map. The diagnostic alpha analysis preserves every drawing in the full scene and isolates them only in temporary files to measure overlap.
