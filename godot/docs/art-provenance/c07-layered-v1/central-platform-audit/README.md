# Central low platform — static geometry candidate, art not accepted

## Scope and source responsibility

Adds one independent low platform/three visible steps to the single selected assembly, **not official C07**. All earlier37placements/36asset definitions unchanged. Total38instances/6sites/3152depth entries. Rules, logical terrain, actor appearance/animation and paused saves unchanged. Runtime remains unchanged and runtimeReady=false.

Original combined50blocked-cell group includes a core, low platform, planters/benches, independent front corner post and separate background canopy. Source responsibility and exact RGB witnesses are in `central-platform-review`. The group rectangle is not a platform footprint. Source west stair(728,488) and southeast stair(808,488) were absent from selected core-only view; candidate restores these actual material points. Four fixed source pavement points remain uncovered. The prior (712,472) interpretation as west wooden container was withdrawn: it is foliage, not foundation.

Visible west foot is approximated; southeast bottom edge is intentionally4–5source pixels behind the original visible step. Hidden rear(766,448) and central connections are **new design**, not a recovered original footprint. Candidate foot folds(714,491)→(767,518)→(824,491). Front corner post's own foot around(767,521) and its upright remain absent, even though the new platform projects over its former(760,504) stone-body sample. Planters/benches must not be counted as restored.

## Correct surface semantics; no runtime exception

The first common-front export (preserved) incorrectly rendered all1476overlapping core pixels behind the platform. A carrying horizontal face must not inherit the farthest vertical stair foot. The offline exporter now supports explicit `horizontal-plane` row decomposition: each row's projected local plane midpoint supplies its depth; vertical risers still use each step's own physical bottom fold. Unknown offline modes reject. All output remains ordinary formal `SceneSurfaceDocument`: same compiled owner for draw, coverage, bounds and depth. No alpha threshold, Z offset, ground-patch workaround or special landmark runtime branch.

Horizontal66row supports; vertical104/107/110supports; combined387supports/918members. This cost is **not a performance pass**. Geometry export/rejoin reproduces the reviewed JSON byte-exactly. Frozen source scripts are provenance; only formal merged surface plus selected layout advance runtime facts. The supported core comes after the platform in selected declaration order, preserving same-parent equal-Z draw order on33boundary pixels. This order is guarded explicitly, not assumed to be strict Z separation.

Existing core PNG warm stone interiors are reused through UVs; no new image editing/generation. First stretched gold material looked like wood and failed visual review. Current broad gray stone has correct placement but is too flat/sharp/continuous for final hand-painted art. Contact/material integration and full-scene performance remain pending; do not present this as a final scene.

## Verification

-1476real core/platform overlap samples:0incorrect final draw order;33equal-Z samples explicitly checked same parent and core-later sibling order.
-247real front Torren(45,32) opaque overlap samples:0platform-in-front errors. This is draw/depth verification, not a full per-pixel pointer acceptance claim. No invented overlapping rear pose.
-Two actual stair samples covered; four source pavement negatives uncovered.
-Conservative full projected convex hull versus324adjacent legal edges × real Torren walk-a/walk-b sole contours and actual possible facing choices:82,950segments,0intersections. Scope is this fixed appearance/frames/region, not whole-level/all-unit movement certification. No action was dispatched.
-Fresh selected-loader/core regression and production build logs retained. Six paused-save hashes unchanged; original scene hash unchanged.

## Targeted negatives

Each reached its named assertion after successful initialization:
-Old common-front surface:1476wrong core pixels; `Horizontal carrying plane must remain beneath actual core base`.
-Late vertical risers:172wrong front-role pixels with core still correct; `Platform real front Torren must remain visible`.
-Illegal solid paving extension at(824,520):`Platform real stair and paving material contract failed`.
-Reversed declaration order:33equal-Z core pixels incorrectly covered; carrying-plane assertion.

Excluded attempts: the initial probe incorrectly treated equal Z as automatically wrong; actual same-parent sibling order must decide ties. An archived `.cs` copy also caused a duplicate external harness definition and one subsequent run used the old DLL. Its failed build and `central-platform-plane-positive` output are excluded; only `*-plane-final-positive` and later selected results from the corrected fresh build count. Archived sources now use `.cs.txt` where necessary in the external project; repository evidence is excluded by its tracked `.gdignore` and the existing production project boundary.

Native v1/v2 screenshots share camera, viewport and display-cloned front actor. They are static geometry studies with absent neighboring objects explicitly labeled, never gameplay or completion evidence.
