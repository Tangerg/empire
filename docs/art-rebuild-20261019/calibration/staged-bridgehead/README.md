# STAGED bridgehead encounter composition study

This is a deliberately staged3v3 art presentation, not initial deployment, an observed turn, a simulated combat result, or an implemented gameplay change. The actual c01-02 terrain subset and three existing crossings remain unchanged. Camera is[160,64,448,280], output448×280 logical and896×560 high-density. P1 visual scales are used.

Blue team cells:(8,5),(9,6),(8,7). Red team:(13,5),(12,6),(13,7). Each cell was checked against `new Battlefield(actualInitializedState, actualCatalog).cellAt(x,y).canReceive('foot')`; all pass, are plain/elevation0, have no occupant or structure. The initialized state serializes identically before and after validation. Fixture records the state hash. The presentation does not relocate actual level units or write any production state.

All six stand-ins use the existing calibration swordsman, with the same source pose. The right-side team is mirrored horizontally around its existing cell-center footing using the renderer-equivalent translate(32,0) scale(-1,1); source pixels and positions are unchanged. Blue/red colors use actual level color values. Contact ellipses and team rings use existing runtime geometry: contact center16,29.2 radii12.5,4.2 fill#0b100d opacity.48; ring center16,29.5 radii11,2.25 stroke1.8. These are restored presentation cues, not new artwork.

Selected scenery omissions from earlier studies remain. Only existing selected pines/bridges/building registrations are eligible, so no new trees or landmarks were invented or moved to improve the layout. Adjacent crossings remain visible at their real96-pixel spacing. This study answers how existing new sprites read together near a crossing, not whether the full level or target art style is complete.

The labeled files are flattened RGB presentation boards. Original raw assets, unlabelled native/high-density renders, source hashes, fixture and registration report remain preserved. No image generation occurred.
