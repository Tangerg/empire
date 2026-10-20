# FX21 structure-damage phase review

The actual original atlas06 row1, full board and current HD runtime strip were viewed. The source shows gray stone and brown wood fragments, not a whole structure icon. Parent approved debris-lift → rubble-scatter → dust-impact → settling-dust and accepted the full native and terminal comparison on 2026-10-20 at 06:39:54 UTC.

1. Debris lift shows a compact group of gray angular stones, jagged ochre wood and chips with no dust.
2. Rubble scatter adds and redistributes fragments, including a larger lower foreground rock. Transparent gaps remain between pieces.
3. Dust impact introduces a localized warm beige-peach burst with mauve-brown contours behind the largest scattered fragments.
4. Settling dust removes most large debris, rounds and separates the dust into calmer puffs, and leaves small stones and a short wood fragment.

The original atlas is 1536×1024 with rectangular slots. All new sources are independently authored 1254-square canvases with one 28/1254 transform; no alpha fitting, shifting or whole-frame scaling was applied. Overall peak/terminal lower bounds are Y 1100 and 1102, a 0.045 logical-pixel difference. These bounds include fragments, so this does not establish pixel-identical dust contact. A warm-color diagnostic gives lower bounds 1100 and 1092, but can include warm fragment edges and is not perfect material segmentation. Native review found the lower impact region stable while the dust itself reshapes.

Source alpha masses are approximately 204,004, 264,446, 539,562 and 291,860 pixel equivalents. The first two phases have no drawn dust; the peak gains a localized dust body; the last loses most large fragments while rounded dust remains. Distinct hashes and alpha totals are diagnostics, not substitutes for native phase review. Not every stone and splinter is claimed to follow an exact ballistic trajectory across the original changing fragment distributions.

The original RGBA board contains an unwanted gray/white vignette, with outer-edge alpha up to 252. That original is retained as evidence only. New sources were generated on genuine transparency, never repaired with alpha cleanup. All new perimeter maxima are at most 1, actual black/white edge deviations are at most 1, and final logical 2px padding is alpha 0. Full-source black/white and native six-background composites were inspected; no old vignette or unrelated backing panel remains.

No literal current FX21 consumer was found. Structure attack events pass their weapon to playStrike/animateHit, and the candidate weapon map resolves FX01–08. Their generic 420 ms hit lifecycle is not an FX21 binding. FX21 stays catalog-only with no assumed hit/heal hold or removal rule. Four equal 1/12-second authored frames play once, nominal 1/3 second. All 28 APNGs and 112 decoded frames match their expected exports. Browser/live playback was not observed.
