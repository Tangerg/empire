# Twin Hills: continuous curved road turns

Baseline: `5d59a23ac5bd44571e3cdae37a44f624330865e1`. This changes only the normal-play `c01-01` road projection.

## Reviewed result

[Complete before](before/full-scene-labeled.png) and [complete after](after/full-scene-labeled.png) show the same full 1012×680 camera, 19 actors, four rule buildings, two farmers and all scenery. Both native and 2× frames, original SVGs and village/southern-bend details are retained. Headers are outside the frame.

The old 9px per-cell rounding is replaced by continuous quadratic centerline turns. The approved width tradeoff is explicit: single lanes increase **22→32px**, and adjoining two-cell aprons **54→64px**. Painted area at alpha≥128 increases from 64,046 to 80,455.75 square logical pixels, **25.6%**, almost entirely inside the same road cells. This is a bounded turn-shape improvement; straight cardinal runs remain visible.

These are actual `BoardView` / `SvgBoardSurface` captures, rasterized offline with Inkscape. Production CSS facing and nested SVG clipping are made explicit for that renderer. This is not live browser or GPU-pixel verification. Object layers, contacts and standing order are unchanged; no scenery was hidden to clear a path.

## One geometry authority

The existing material binding supplies 80 road plus four settlement cells. The provided content catalog's route tags supply their cardinal connections. The current graph has **84 nodes, 99 edges and 46 chains**. Every edge is traversed once; there is no separately authored route list.

Chains run between junctions/endpoints. Only collinear intermediate controls are removed. Each degree-two elbow is rounded by a quadratic whose tangent cut is the smaller of 22px and half either adjacent segment. Junctions remain at their actual cell centers. Closed cycles and isolated cells are explicit. Filled 2×2 aprons retain solid centers. Real border ports extend to the board edge; bridge connections reach the threshold without painting the bridge.

The scene owns the reviewed `32/22/2` profile and protected village-notch cells `(11,8)/(12,8)`. The old active area-mask runtime path is removed. Other scenes and editor previews retain their existing cell-local rendering. The previous technical mask atlas remains an historical exported artifact and before-evidence input, not an active road alpha source.

## Exact bounds and safety

`geometry-runtime-coverage.json` and `runtime-masks/` preserve the actually emitted runtime alpha, not a separately reimplemented corridor.

- All 84 radius-8 center disks retain their opaque interior; every real cardinal interface retains its 22px opaque port core
- Both enclosed grass regions remain open, and the road remains one cardinally connected region
- Village-notch cells and all forest/hill/mountain/water/bridge exclusions have maximum alpha zero
- The hard clip is the intersection of a **Euclidean 2px road-union buffer** and actual road/plain cells, with the notch excluded. It cannot paint a generic buffer into other terrain
- Current overhang at alpha≥128 is exactly **2.75 square logical pixels in plain `(6,12)` only**. Its maximum raster extent, including AA, is 1 logical pixel
- The emitted 2× alpha is **pixel-identical to the approved proposal** preserved in `approved-proposal-alpha@2x.png`
- All 2× disk sample points are fully opaque. At native 1×, a few pixels centered near the disk edge also cover exterior antialiasing (minimum alpha219); every pixel area wholly inside the radius-8 disk remains255
- All256 continuous geometries are exactly rotation-equivalent. Inkscape itself has directional curve AA, even for a circle; raster rotation differences are confined to partially covered pixels, while center/port/exclusion/topology checks pass at both densities

The full-frame comparison changes 12,712 native pixels, all within the road boundary band. No authoritative terrain, movement cost, navigation, actor deployment, mission objective, trigger or difficulty parameter changes.

## Sources and deterministic validation

`source-comparison.json` pins all **26 common raster sources**, identical before/after and against main `da5b13490acd09d36f7844616b4e62d2690c43c9` at comparison time. The only removed reference is the old technical `road-masks@2x.png`; no new artwork is introduced. Earth source SHA256 remains `9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53`. Its period remains1024 logical pixels, with one source/raster pixel per logical unit, below native 2× world detail.

`render-invariants.json` compares all16 levels: only normal-play c01-01 ground changes. All level/state hashes, actor bindings, standing layers/order and editor ground hashes remain identical. `replay-comparison.json` records independent before/after sweeps of all60 replay/profile combinations: the complete 3,534,788-byte event/output stream is identical, SHA256 `3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9`.

`verification.json` and compressed logs record actual checks. At this handoff the final frozen single-worker full suite is still running; it is not reported as passed. The sparse engineering checkout temporarily links omitted supporting art/docs to main for the full suite; those links are excluded from the commit and cleaned after the run. Runtime capture inputs are ordinary local files with the hashes above. The integrator should still run the complete suite/build and capture at its fixed main checkpoint.

The first full run caught a real integration error: graph connectivity initially ignored overridden catalog route tags. The implementation now uses the supplied catalog for connections while preserving paint ownership, and the existing catalog-isolation test verifies84 disconnected caps when those route tags are removed. The shared `inBounds` owner is also used. No assertion or timeout was disabled. One intermediate run overlapped a function-signature edit and is not the final frozen verification.

## Reproduce

```sh
npm run typecheck
npm test -- --maxWorkers=1 --minWorkers=1
python -m unittest discover -s tools/art -p 'test_road_corridor.py' -v
PYTHONPATH=tools/art python -m unittest test_field_material test_water_material -v
python tools/art/export_road_material.py --check
npm run --silent sweep > /tmp/road-turn-replay.txt
ROAD_CORRIDOR_OUTPUT=/tmp/road-corridor npx vite-node tools/art/review_road_corridor.ts
STANDING_REVIEW_OUTPUT=/tmp/road-turn-scene npx vite-node tools/art/review_gray_flag_scene.ts
CONTRACT_OUTPUT=/tmp/road-turn-contract.json npx vite-node tools/art/review_candidate_scene_contract.ts
```

The Python corridor suite invokes the production projection, rasterizes its SVG, checks all256 patterns and rotations, the actual map, cycles/aprons/bridge/chunk boundaries, and deliberately corrupts a center, port, protected notch and overhang pixel to prove the guards reject them.
