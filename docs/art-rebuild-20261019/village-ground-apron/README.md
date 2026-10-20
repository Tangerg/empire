# Single village ground-contact pilot

A restrained presentation-only addition at C01-02 village (8,5), using the existing ground-edge registration/export owner. This is not a cure for the remaining hard road junctions and is not repeated across every building.

## Source

Built-in image generation produced an original genuinely transparent ground-only apron, with no buildings, props, shadow disc or square backing. The first candidate was rejected as too warm/painterly and is retained under `source-candidate/`. Revision 2 uses the approved grass and earth originals as references; its exact prompt and metadata are retained beside the unmodified master. The production supplemental master SHA256 is `0d11e2d8ff7c87285541b319053384cc2cac7f35f5cc2c18344143b0a13f06ff` (1692×930 RGBA). It is a needed supplemental source, not another canonical-art slot.

No procedural recoloring, filtering, alpha painting or cropping occurred. The existing exporter preserves the entire authored canvas, adds only a technical transparent margin and performs independent native 1×/2× Lanczos exports. Source outer-edge alpha is at most one 8-bit step; exported edges satisfy the same existing contract. The meaningful width is 80 logical pixels; uniform source scale preserves aspect ratio.

## Placement and ownership

One ground-only piece is anchored at world (272,191), beneath the original full-scale building. The manifest names every tile in its footprint plus neighboring support. A changed village, road or terrain support disables this local decoration. No new rendering owner, gameplay terrain, collision, building, actor, economy or mission change is introduced.

## Evidence

- [Whole scene after, 2×](single-village-trial/runtime-standing-full@2x.png), offline production rendering, not browser QA.
- [Village group before](single-village-trial/village-group-before@2x.png) and [after detail](single-village-trial/village-group-detail@2x.png). These labeled crops supplement, never replace, the complete scene.
- [Pixel/state comparison](single-village-trial/pixel-comparison.json): the same gameplay hash, and all image pixels outside the apron bounding box are byte-identical.
- [Exact full-canvas export proof](single-village-trial/export-report.json).
- 47 focused TypeScript tests, five exporter/alpha tests and typecheck pass. Tests retain local terrain mutation rejection and exact source/resize assertions.

The broader irregular layout remains unchanged. Road ribbons and some river transitions still need cohesive art work; this pilot addresses only one building entry, without adding clutter.

## Low-alpha fringe qualification

Raw source viewing reveals red-biased RGB in faint generated fringe, not fully transparent-only RGB. The source has 3,441 red-biased pixels, all alpha 1–7; native 2× has 17 at alpha 1–3, and native 1× has 12 at alpha 1–11 after the existing Lanczos export. These original pixels are preserved, never cleaned procedurally. `verification/edge-rgb-audit.json` defines the predicate and records exact pixel/projection measurements. Native black/white/meadow composites are preserved beside it.

At production 2× the flagged pixels contribute at most two 8-bit channel levels when composited onto the actual scene backgrounds, before standing occlusion. Actual before/after differences at their projected centers range from zero to seven levels because normal neighboring edge interpolation also contributes; this is not misreported as isolated fringe attribution. The native 1× meadow composite bound is six channel levels. The inspected native composites and village group show no bright red fringe. This qualification is retained for source-quality review rather than calling the master perfectly clean.
