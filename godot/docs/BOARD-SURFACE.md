# Shared board surface

The logical grid and battle state stay unchanged. `BoardProjection` derives one
presentation surface from the composed grid outline, canonical elevation and cliff
edges. One elevation unit raises the center by 12 logical pixels. Corners average
incident cells connected without a cliff; a declared cliff separates the two sides.
No first-level cliff is invented to justify rock art: its hills are continuous grass
slopes. The same surface maps ground UVs, actor/building/decor placement, movement
animation, range/path outlines, camera targets and inverse pointer picking.

Terrain geometry and ranges share depth partitions cut directly from the original
triangles at standing contacts and required surface boundaries. They do not retain
a full-map pixel tessellation. Contact ordering has one-logical-pixel resolution.
An unchanged board retains its geometry and logical material viewport. Movement
prepares the crossed contact depths once before the tween, not on every frame.
Conservative pixel partitions remain around non-injective or cliff risk spans.
Sorting a whole fan triangle by its centroid would let its rear portion incorrectly
cover a standing object. Bands bound that approximation to the declared one-pixel
sorting resolution. Actor placement anchors and authored foreground ground contacts
are distinct: a door can remain aligned to its road while the building's nearer
foundation corner determines depth. Neither value changes gameplay collision.

A steep valid surface can face away from the fixed camera. Such a fan still defines
logical contact projection, but its underside is excluded from rendering and picking.
Picking considers visible tops and actual cliff-face occlusion rather than applying
a flat inverse transform. Nonfinite or degenerate logical geometry is rejected.
The absolute Godot Z range is checked against the map extent and scenery margins;
there is no compressed-depth fallback.

`ProjectedGround` owns a one-update transparent SubViewport containing the logical
material compositor. Surface polygons borrow its texture. Meshes release that
reference before the viewport is freed; authored ground is transferred, not cloned,
when the compositor is rebuilt. Runtime height/terrain changes invalidate the cached
geometry. Surface normals only shade the same mesh; they do not introduce a second
height or collision representation.

Current limits: sprites are upright billboards with authored ground-front depth,
not deformable foundations. Buildings must be placed on suitable terrain. A correct
continuous slope and shared input contract are not a claim of final art direction;
stronger height cues and coherent ground palettes remain visual iteration work.

## Verified checkpoint, 2026-10-21

The production build and native resource scene pass for all 16 campaign resource
projections, including ground-front placement, partition area/depth preservation,
visible-surface picking, real cliff-face occlusion and capture playback. The cloud
Linux window was operated at 1180×812 on Mesa llvmpipe: slope selection/movement,
F5 save, F9 reload after restart, uphill movement followed by `u` undo, pan/zoom,
and a complete enemy turn with village capture all ran. The capture path now changes
only the owner banner, matching the normal-frame map-building contract. HUD numeric
projection uses the existing JSON element boundary for both parsed and CLR-created
number nodes; it no longer assumes every canonical number is stored as a CLR double.

A same-window village observation compared published `2a8681c0` with this surface:
Godot `TimeFps` was 29 versus 39; `RenderTotalDrawCallsInFrame` was 572 versus 339;
`ObjectNodeCount` was 1069 versus 995. The corresponding `TimeProcess` samples were
44.24 ms and 39.13 ms. These are distinct Godot monitor snapshots taken before image
readback, not an averaged benchmark or reciprocal FPS/frame-time pair. Current ground
geometry was 2816 source triangles, 6975 clipped patches and 78 partitions. Earlier
full pixel slicing was rejected after an observed 913 draw calls and 2377 nodes.
Movement may temporarily add crossed depth cuts, then restores the standing-only
partition set; it does not reconstruct terrain every animation frame.

This establishes spatial correctness and removes the measured renderer regression.
It does not make the twin hills visually finished: the first level's traversable
slopes are still subtle, legacy composite castle/camp art remains visibly different
in scale, and stronger authored terrain silhouette is a subsequent level-design task.
No new macOS package or macOS runtime acceptance is claimed by this checkpoint.
