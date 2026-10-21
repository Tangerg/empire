# Shared board surface

The logical grid remains square. `BoardProjection` derives one
presentation surface from the composed grid outline, canonical elevation and cliff
edges. One elevation unit raises the center by 12 logical pixels. Corners average
incident cells connected without a cliff; a declared cliff separates the two sides.
The fixed dimetric ground basis is `(0.8x - 0.8y, 0.4x + 0.4y)`; height displaces
only screen Y. Upright sprites are not skewed. Screen bounds, contact depth and
inverse picking derive from this same basis. The same surface maps ground UVs, actor/building/decor placement, movement
animation, range/path outlines, camera targets and inverse pointer picking.

Terrain geometry and ranges share depth partitions cut directly from the original
triangles at standing contacts and required surface boundaries. They do not retain
a full-map pixel tessellation. Contact ordering has one-camera-depth-pixel resolution.
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

## Previous height-surface checkpoint: 6a70edff

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

## Dimetric ground and north-hill authoring

The native first level deliberately changes its terrain design: four south-facing
cliff edges and two east return edges define the north platform, while two west
ramp columns retain a traversable 0→1→2 sequence. These are canonical level edges,
not scenery collision. Foot traversal is blocked across cliffs; flight can cross.
The actual starting scout can descend and ascend the ramp within its turn budget,
and every starting ground unit remains terrain-connected to the central corridor.
The south hill has not been redesigned. Historical source-level hashes are not an
acceptance condition for this intentional native level change.

`AuthoredScene.openingFocus` optionally names an initial camera cell. The screen
checks that it belongs to the canonical interactive cells. It does not change
playability or camera geometry. Without an authored camera composition, the current
human formation supplies the target.

`BoardView.Pick` checks the actual opaque pixels of visible standing sprites in
render order before returning the visible surface hit. Decorative foreground art
blocks click-through. A building's cell cannot be reinterpreted as a hidden unit
occupying that cell. Ground and cliff occlusion still compare the same camera-depth
value; selection never changes domain legality. Existing keyboard selection remains
available independently of render occlusion.

Range decorations submit indexed triangle arrays grouped by depth and texture;
cliff faces share a polygon submission per depth. UVs and vertex interpolation stay
on the original projected surface. Cursor-only movement does not reconstruct range
commands. Static board geometry is retained; movement adds only crossed contact
cuts before its tween. Non-overlapping surface vertices do not themselves require
a full-width depth cut. Conservative one-pixel cuts remain within actual cliff-face
and folded-surface overlap spans, expanded by the authored height reach.

Facing vectors are projected by `BoardProjection` from the composed grid strategy.
Settled sprites, facing pips, movement and strike lunges therefore agree; the
application no longer supplies separate screen-facing callbacks. HUD direction
buttons retain their localized semantic names rather than showing unprojected
Unicode arrows. Billboard spawn/death effects preserve their authored screen-space
pivot, not an invented second ground coordinate.


## Dimetric checkpoint validation

The production build, all-campaign presentation resource scene, animation scene and
camera integration scene passed. Focused coverage includes opaque body selection,
foreground building rejection, cliff-face occlusion, projected facing/strike vectors,
and the authored north ramp's actual walking budget. Formatting and tracked Godot
resource metadata checks pass. These are targeted affected contracts, not a new
claim that every historical oracle suite has been rerun.

The actual Linux window was exercised at 1180×812: body selection, ramp movement,
undo, F5 save, a complete enemy turn with capture at (23,14), return to human turn 2,
F9 restoration to turn 1, zoom and drag. The runtime log contains no exception or
native error. Interaction remains usable on the cloud software renderer, but is not
a high-frame-rate hardware acceptance. The process was stopped from its owning
terminal after observation; clean teardown evidence comes from the native scenes.

On Mesa llvmpipe, the initial selected-range implementation submitted 13,469 draw
calls. The final same-opening selected observation submitted 786, with 1,381 scene
nodes and Godot TimeFps 23; TimeProcess was 58.28 ms. Unselected opening was 606 draw
calls, 1,319 nodes, TimeFps 21 and TimeProcess 54.37 ms. A later zoom/pan observation
was 556 draw calls, TimeFps 27 and TimeProcess 45.30 ms. These are separate monitor
samples before screenshot readback, not average frame times or reciprocal metrics.
Opening geometry contains 2,821 source triangles, 18,202 clipped patches and 216
partitions. No hover-frame range rebuild is needed. The old axis-aligned projection
is not retained as a runtime alternative.

The new ground direction is a coherent spatial foundation, not final landscape
acceptance: legacy composite castle scale and the north platform's short silhouette
still need art/level refinement. No new macOS execution or package is claimed.
