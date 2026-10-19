# Selected broad water sampling

512logical /1024physical is selected after full-map review. Source density is
truly2pixels/world unit; the1254² approved source supports this resize. The
128baseline and its visible ribbon repeats remain in `../water-runtime`.
The reviewed512images remain in `../water-runtime-512-diagnostic`.

The actual runtime was rendered again at the same full704×448 camera after
promotion. `promotion-comparison.json` records eight exact PNG comparisons
(full field, complete integration and both camera crops, at1x/2x) against the
retained512diagnostic. Duplicate images are not checked in.

Topology and all256 reusable masks remain unchanged. c01-02 now uses one shared
512-unit water picture containing its28 water/bridge masks. Mask and image
references are self-contained for isolated GPU baking; no fixed map backdrop
is installed. Longer maps may still expose a512-unit material-repeat boundary.
Legacy road/shore/crossing art and large building overlap remain distinct issues.

Verification:131 TypeScript assertions (including104 architecture),103 Python art
tests, typecheck, production experience build and both world export checks passed.
The previous60-case replay equality remains applicable: only material sampling
metadata/rasters, diagnostic tools and expected presentation tests changed.
