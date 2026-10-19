# 512-unit water sampling diagnostic

Same complete 704×448 camera, actual 28-cell water/bridge occupancy and all 9
structures / 20 initial actors as the formal 128 baseline. This was rendered as
an explicitly unselected comparison before the sampling promotion was approved.
The current runtime was restored to128 after this diagnostic; a later separate
commit selects512.

The approved 1254² source is resized to512/1024 physical pixels over a512logical
period: native1x/2x detail is available. The material is not recolored, blurred,
painted or cropped. Only approved outermost-pixel matching is applied. Alpha masks
are unchanged. The actual river fits within this world period and no longer has
internal ribbon/plane repeat boundaries at y128/256/384. Larger maps can still
reach the next512boundary; this is not a promise of seamless artistic repetition.

Full images preserve large building overlap and legacy roads, shoreline and
crossings. Water-only crops are pixel-exact at1x/2x. Native2x alpha and opaque RGB
exactly match reusable calibration. These are offline BoardPieces renders, not
browser/GPU gameplay captures.

`diagnostic-manifest.json`, `runtime/` and source/export provenance retain the
sampling and raster evidence independently of later runtime changes. The SVGs
reference these retained water textures. Other layers reference the formal atlas
files from the time of review; PNGs preserve the complete reviewed frame.
