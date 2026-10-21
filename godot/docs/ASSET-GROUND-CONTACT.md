# Placement and foreground contact

The native selected-asset schema 2 and environment catalog `environment-builder-v1.2`
require an explicit `groundFront` point on every selected asset or named environment
cell. Coordinates are logical frame pixels, before pixel density, presentation scale,
placement scale, or reflection. Missing, nonfinite, or out-of-frame values are rejected.

`anchor` places the image relative to its authored location. `groundFront` identifies
its foreground ground contact for painter depth. Neither is a gameplay occupancy or
collision rule. The existing `footprint` is not used to manufacture this point. A door
threshold can remain the placement anchor while the nearer building corner supplies
the foreground contact. Weapons, flags, shadows and transparent padding must not be
used to infer this fact from alpha bounds.

Both catalog sprite factories attach the point in physical frame coordinates.
`SpriteGroundContact.Offset(sprite)` transforms it using the sprite's current texture
size, reflection, offset, centering and local transform. The result excludes sprite
position and is expressed in its parent's coordinates. Consumers add that displacement
to their logical placement for shared projection depth; they do not move the image or
assign an exceptional Z layer. A separately transformed container must likewise
transform the displacement into the board's logical coordinates.

The current authoring retains established contacts explicitly for the existing asset
set. The reviewed native frontier house uses `(800,1132)`, the straight rural fence
uses `(90,108)`, and the corner fence uses `(56,106)`. These correct foreground bases
that extend below their placement anchors. This does not claim every old asset has
received a fresh visual audit. Ground decals and UI thumbnails may carry the same
metadata without using depth projection.

The native JSON documents are the authoring owners. `export-grayflag.ts` writes old
TypeScript-derived selected assets and environment catalogs only under
`tests/oracle/source-v1`; it cannot overwrite native foreground contact metadata.
Historical manifests are not accepted as native runtime catalogs.

This is a sprite painter-depth contract, not a deformable building foundation.
Buildings spanning a slope still require an appropriately authored level placement;
foreground contacts do not flatten terrain or change movement legality.
