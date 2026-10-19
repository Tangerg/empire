# Surface meadow pilot contract

Read-only design, 2026-10-19. Do not use the forest cutout exporter.

## Consumer and four identities

`packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts:26`
uses surface-meadow as the field base. Lines82–86 explicitly distinguish four
materials: flowering grass, bare dirt, grass over broken stones, blue flowers.
Hill uses tone2 (zero-based, meadow-3). Preserve all four identities and indices.

Source: final-fantasy-v1/environment-builder-v1/masters/surface-common-4x4.png,
first row, four rounded grid cells. Output surface-meadow has four columns, one
row, cell32²/64², total128×32/256×64. Opaque RGB or RGBA alpha255 is correct.
No cutout padding, anchoring offset, chromakey or forest footprint contract.
Manifest category base-surface, tileMode variants, edgeLocked true, unchanged.

## Existing builder operations, not yet authorized for new-master adapter

build_environment_builder_v1.py surface_atlases() calls make_tileable(cell,64),
then lock_family_edges for the four cells. make_tileable convertsRGB, square-fits,
offsets half a tile, blurs a central cross through a soft mask, then averages
opposite edge pixels. Family locking averages top/bottom rows across all four
cells and left/right columns across all four; corner averages are shared.
save_pair downsamples64→32 then repeats family edge locking at32.
These are pixel synthesis, beyond resize/pack. Do not silently reuse them.

## Conservative candidate route

Imagegen authors four full-bleed opaque seamless textures from exact source
references, with matching quiet boundary vocabulary. Preserve raw masters.
Require square same-size canvases, no grid lines, shadows, lighting gradient,
borders or large unique center objects. Deterministically resize full canvas
(no crop/recolor/blur/edge replacement) to64 and32; pack unchanged ordering.
Check exact self and cross-family edge equality and generate repeated2×2 and
mixed4×4 native/HD comparisons. If equality fails, report it: do not claim
edgeLocked or silently average pixels. Request imagegen retry or an explicit
separate edge-derivation decision. Imagegen alone is not guaranteed to meet
bit-exact equality, even when the texture looks seamless.

## Validation and dependencies

Record source hash, sizes/mode, alpha range, each cell edge hash, per-edge mean
and max color difference, decoded output hashes and comparison paths. Preserve
all other atlas hashes, manifest order and renderer adjacency masks. Visual
seam acceptance must inspect repeated material and all four pairings, not only
one isolated tile. Actual-game terrain and transition integration remains
pending until observed.

Existing transitions meadow-earth, meadow-forest and meadow-snow use overlay
material textures, not meadow pixels, so they need not be regenerated merely
for this base change. However boundary palette compatibility is still a visual
gate. Do not count these transition atlases as remade with the meadow pilot.

## Approved implementation update

The user explicitly approved bounded programmatic seam matching on2026-10-19.
New adapter `tools/art/export_surface_overrides.py` does NOT reuse the old blur,
half-tile offset, or broad fitting. It resizes each square source independently
to32 and64, then changes only the outermost row/column by shared family edge
averaging. Interior pixels match the plain resize byte-for-byte; source masters
are immutable. Tests cover interior preservation, all family edge equality,
source nonmutation, opaque-only contract, exact order and reproducibility.

Actual generated output requires visual repeated2×2/mixed4×4 and combined-scene
review. Perfect edge equality cannot remove a repeated center blob or vignette.
Early combined-scene review found legacy meadow too mottled and repetitive;
new generation should keep distributed, quiet detail so units remain focal.
Fern yellow/brightness is a pending cohesion concern, to compare against the
new ground before deciding whether imagegen correction is necessary.
