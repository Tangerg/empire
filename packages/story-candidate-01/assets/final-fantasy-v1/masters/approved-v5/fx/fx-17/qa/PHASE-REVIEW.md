# FX17 healing phase review

The original runtime strip and atlas05 row1 were inspected directly. The four authored phases are healing-onset → leaf-spiral → healing-peak → orbit-dissipation. The parent accepted the peak baseline first, then the complete native and terminal comparison on 2026-10-20 at 06:11:02 UTC.

1. Onset has a compact thin sage orbit, the olive medical plus, two crossed beige bandage pairs and sparse leaves.
2. Leaf spiral opens and develops the inner ribbons, with more leaves and outward bandage positions. Its outer envelope is already close to peak width, but its ribbons are more open and total alpha mass is lower.
3. Peak has the fullest nested sage orbit, still the same central medical plus and beige bandages, with more leaves around the arc.
4. Dissipation removes the plus and both bandage pairs. Five olive leaves and separate thin broken arcs remain. The central source region formerly occupied by the plus has zero pixels at alpha >=128. There is no leaf bed, fire, ground patch, filled backing disc or renewed pulse.

The first three plus centers are (627.5,736.5), (627,720.5), and (627.5,707.5) source pixels. The maximum displacement is 29 source pixels, or 0.6475 logical pixels, mostly upward. The parent accepted this asset-specific native static variation. No pixels were shifted or normalized to conceal it. Leaf identities are not tracked as exact continuous trajectories; the evidence shows changing authored orbit shapes and poses, not proof that every leaf follows an exact path.

Total source alpha mass rises from 147,078.5 to 308,704.6 to 380,909.5 pixel equivalents, then falls to 69,986.9. This supports visibly different geometry; distinct hashes or alpha totals alone are not a motion acceptance criterion. All four full source black/white composites and native outputs on six backgrounds were viewed. The alpha-1 source perimeter samples are conditionally accepted with actual clean composites and edge deviation at most 1. Native logical 2px padding remains alpha 0.

## Actual heal consumer

The heal event resolves the healed target and calls BoardView.animateHeal. The presentation binding is C01-FX-17, and the adapter selects the HD source while retaining 32-square logical frames and anchor [16,16]. The strip starts automatically and plays four frames at 12 fps without looping. Frame 4 starts at 250 ms and remains until the normal-motion 500 ms tween completes and removes the drawing. Its total dwell is 250 ms, including 166⅔ ms after the nominal 333⅓ ms clip completion. The independently animated +amount text is not baked into the source; the consumer does not fade or scale the strip itself. The separate 420 ms hit lifecycle does not apply.

The ordinary clip APNGs contain four equal 1/12-second authored frames and play once. Separate consumer diagnostics use visible durations 1/12,1/12,1/12,1/4 seconds and then a blank observation sample beginning at 500 ms. The blank depicts removal and is not a fifth authored phase. It lasts 1/12 second only for inspection. All decoded pixels match the selected exports or the explicitly empty removal sample.

The parent reported that actual BoardView heal hold/removal and separate hit timing regression tests passed before new-source binding. This worker does not claim those source masters are already integrated. Browser/live playback was not observed. Reduced-motion and cancellation paths are documented in consumer-audit.json and are distinct from the normal-motion 500 ms contract.
