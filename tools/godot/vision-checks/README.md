# Fog and committed-cast query parity

Run `bash tools/godot/vision-checks/run.sh` from the repository root.

The oracle compares visible tile insertion order, visible actors and individual
visibility across square4, square8 and hex grids, fog on/off, allied owners,
opaque cover and viewers with no field units. Separate projections compare
public delayed-cast coverage/countdowns, overlapping casts and missing casters.
All source fixtures use canonical integer coordinates and vision radii.

Native vision rejects fractional vision radii explicitly. Original TypeScript
accepts numeric radii without validating integrality and can produce fractional
coordinates; that malformed-content boundary is not parity-compatible. Shipped
GreyFlag content uses integer radii. Core grid query parity does not establish
hexagonal native scene layout parity: current authored GreyFlag scenes are square.
