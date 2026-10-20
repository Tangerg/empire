# FX19 concealment source proposal

The original atlas05 row3, full board and current HD runtime strip were inspected. The original begins with a dense gray-green leafy cloud, then curls, breaks into separated ribbons, and ends as sparse wisps and leaves. It is not a healing effect, and the first frame is not an empty onset.

## Proposed phase order
1. **dense-veil:** broad low gray-green smoke with a rising central curl and olive leaves.
2. **curling-veil:** rolling curls deform and open side gaps while leaves advance.
3. **veil-breakup:** separated smoky ribbons and puffs with large transparent gaps.
4. **leaf-wisps:** a few small wisps and floating olive leaves; no actor appears behind them.

Use four independent 1254-square RGBA canvases and one shared 28/1254 transform at origin [2,2], source pivot [627,627]. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. The nominal clip is 333⅓ ms. No 420 ms hit or 500 ms heal lifetime is assumed because no literal current consumer was found.

## Consumer and visibility audit
The search covered package/app TypeScript, TSX and nonmanifest JSON, with no C01-FX-19, C01-FFV1-FX-19 or fx-19.png match. Existing visibility is owned by vision rules, projected into the hiddenUnits overlay, and honored by BoardView and its backends. No FX19 call was found in that flow. The art remains catalog-only. This task neither adds a hiding mechanic nor draws a marker at an unseen enemy position. No figure, hidden target silhouette or eye icon will be baked into the source.

## Required proof
Review the dense baseline before remaining phases. Density dissipates through authored broken shapes and real transparent gaps, not a globally faded copy. Smoke may be translucent without a near-opaque core. Verify actual source composites, native multi-background readability, full/terminal progression, exact single-play timing and decoded-frame equality. Source alpha remains untouched. Browser/live playback stays unobserved.

Detailed source and current code hashes are in source-evidence.json and consumer-audit.json. Generation is paused for parent review of this proposal.
