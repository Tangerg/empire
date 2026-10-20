# FX21 structure-damage source proposal

The actual atlas06 row1 and current HD four-frame sheet were inspected. The source is flying gray stone and brown wood splinters, followed by a warm dust impact and settling dust. It is not a building icon or a persistent damaged-structure state.

## Proposed phases
1. **debris-lift:** a compact group of stones, wood splinters and chips lifts, without dust.
2. **rubble-scatter:** more widely scattered wood/stone pieces, including a larger foreground rock.
3. **dust-impact:** peak flying debris and a localized beige-peach dust burst from the lower central impact area.
4. **settling-dust:** fewer rubble pieces and lingering warm dust at the same lower origin.

Generate the peak baseline first, then the other independent phases using the same 1254-square canvas, scale 28/1254, origin [2,2] and source pivot [627,627]. Preserve 12 fps, loop=false, normal blend and anchor [16,16]. No per-frame fitting, pixel shifts or alpha edits.

## Important source risk
Atlas06 is 1536×1024 with 384×256 slots. Its RGBA mode is misleading: the gray/white vignette is actual unwanted backing, with outer-edge alpha up to 252. Recreate only fragments and localized dust on genuine transparency. Do not reuse its keyed alpha or paint a replacement backing panel.

## Actual consumer audit
No literal current FX21 consumer was found in package/app TypeScript, TSX or nonmanifest JSON. Structure attack events do exist, but they pass the weapon to playStrike and then animateHit. The candidate weapon map selects FX01–08, never FX21. Thus the generic structure-hit path uses 420 ms, but that is not an FX21 binding or lifetime. Keep FX21 catalog-only unless an explicit integration is separately authorized. Do not infer a 500 ms heal lifetime either.

Full source/composite, native multi-background, terminal and exact single-play timing proofs will be delivered. Browser/live playback will remain unobserved. Generation awaits phase-order approval.
