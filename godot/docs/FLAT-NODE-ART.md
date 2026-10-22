# Flat oath-control node art

The native control-node binding is a one-cell **surface**, not a standing console.
Its four full original RGBA PNGs live in `assets/oath-control-pad/`; the captured
state explicitly reuses normal. Disabled is healthy intact unlit stone; Destroyed
is broken unlit stone. Neither carries an active faction mark or health gauge.

## Registration

Source canvases are 1729×910 (active) and 1728×910 (other states). Their alpha > 8
bounds are respectively (375,211)-(1363,703), (375,211)-(1363,703),
(374,211)-(1359,703), and (299,172)-(1429,740), with exclusive right/bottom edges.
Very faint alpha ≤ 8 stray pixels outside those bounds remain untouched.

Each full source is uniformly scaled to 26 visible logical units wide. The
resulting visible height is about 13, with no anisotropic distortion. The broken
original was generated larger; its own authored scale corrects that raw-source
framing difference. This is comparable registration, not identical silhouettes
or exact pixel invariance. Main bounds vary by less than 0.15 logical units.

Anchors are the central walkable star at (866,427), (866,427), (864,427), and
(864,421). They are intentionally **not bottom-center prop anchors**: the actor's
canonical shoe contact must meet the walkable center, not the front rim. Ground
front equals anchor, but surface art never contributes a standing front offset.
The full source canvas retains transparent margins; it is not a new gameplay
footprint or collision surface. Visible art fits inside a 32×32 logical envelope.

The existing `BoardView` Surface band and canonical projection/picking remain the
only surface path. Bounded dynamic surface sprites reuse `PaintedSurface` and its
canonical depth partitions, preserving their texture-to-board registration while
clipping to ground tops. The bounded path explicitly rejects sprite regions,
frame grids, flip flags, and cropped/margined/nested atlas sources; current pad
frames and flat decorations use complete single-frame textures. A single
center-depth sprite was insufficient: a moving
unit approaching the rear half could be covered, and a forward terrain strip
could cover the front rim. Per-partition composition fixes that source of error
without changing gameplay, adding a projection, or stretching the artwork. The ownership mark and health decoration stay flat and below
units. Other authored structure bindings explicitly remain standing and keep
standing hit masks/occlusion. Unbound rule-derived drawings remain separate.

## Verification

`OathPadChecks.tscn` exercises actual Godot resources, rejected missing/unknown
binding depth, all five logical states/four physical images, measured alpha
registration, same-cell shoe depth and picking, standing-structure preservation,
and two-cell traversal through the real native presentation timeline. The usual
walking bob is preserved; there is no pad-specific transform or height jump.

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj
"$GODOT_NET" --headless --editor --path godot --import
"$GODOT_NET" --headless --path godot res://Scenes/OathPadChecks.tscn
"$GODOT_NET" --headless --path godot res://Scenes/DynamicStateChecks.tscn
```

On a real native display, add `-- --capture-dir=<absolute directory>` to the
OathPadChecks command. It saves native viewport states with/without the occupant
and traversal samples. These are labeled presentation fixtures on the existing
c01-03 board, not a completed third-scene map or a gameplay outcome claim.

## 2026-10-22 display acceptance

The real Linux desktop Godot 4.6.3 .NET run passed 131 assertions with the normal
TerrainView enabled and exited 0, without error or resource-leak diagnostics.
Twenty-eight unedited native viewport captures cover all five roles occupied and
unoccupied at 1× logical display scale and a focused 5× view, plus eight 5×
traversal samples. The 1× frames retain the surrounding existing board rather
than enlarging or resampling a screenshot. Visual inspection confirms central
shoe contact, intact-off versus broken-off distinction, registration stability,
rear-half walking overlap, and an unclipped front rim over terrain. The software
OpenGL driver reported only its existing unsupported V-Sync warning.

The test also requires an actual overlapping sample where the former single
center-depth sprite would cover the actor; it cannot pass on a path that misses
the pad. This is renderer acceptance using detached presentation fixtures, not a
new campaign-layout or gameplay/balance claim. The existing c01-03 map and all
canonical gameplay/content data are unchanged.

Additional checks passed: DynamicState (460 assertions), all 16 authored-board
Presentation contracts, PaintedSurface elevated registration/depth/picking, and
Animation (179 assertions). The supplemental LightingChecks integration is not
claimed green: its unchanged historical c01-01 fixture is 27×17, while the
current authored scene is 36×30, so the existing admission guard rejects it
before dynamic rendering. This slice does not rewrite that historical oracle.

LightingLifetime separately passed 94 assertions through three real-session
load/rebind/restart/queued-exit cycles, with clean process exit and no leaks.
