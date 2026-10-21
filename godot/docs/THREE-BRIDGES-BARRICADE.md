# Three Bridges barricade artwork and content

The native content pack `data/samples/three-bridges-content.json` owns the
`three-bridges.barricade` definition: 60 maximum HP, zero defense, movement blocking,
no vision blocking, half cover, obstruction height 1, repairable and targetable.
Existing HP-zero rules release movement blocking and cover. The same state owner,
not artwork or alpha, determines those transitions. The localized display name is
`sample.three_bridges.barricade` in `Localization/zh_CN.po`.

## Source ownership

`data/selected-assets.json` owns the three native records. Their topic IDs are
`THREE-BRIDGES-STRUCT-BARRICADE-NORMAL`, `THREE-BRIDGES-STRUCT-BARRICADE-DAMAGED`,
and `THREE-BRIDGES-STRUCT-BARRICADE-DESTROYED`. Each is a one-frame 1470 × 1070 RGBA
asset at pixel density 1, copied without modifying the generated image bytes to
`assets/native/structure/three-bridges-barricade-{state}.png`. The matching
`data/native-scene-art.json` provenance records identify this native owner and
record the source hashes. These are not generated TypeScript asset projections.

`tools/godot/export-grayflag.ts` writes its historical selected-asset manifest and
structure bindings only under `tests/oracle/source-v1/`. Its historical image copy
loop uses original asset-manifest paths, not these native paths. It neither owns
nor regenerates these three files, their lifecycle bindings or their contact
metadata. No additional export path or runtime fallback was introduced.

`data/art-bindings.json` maps the new structure's four lifecycle states explicitly:
normal, damaged and destroyed each use their own topic and frame of the same name;
captured reuses the normal topic/frame. Allegiance remains the canonical owner
banner. There is no duplicated captured PNG and no baked allegiance in the images.

## Registration and scale

All states use presentation scale `28.6 / 1171` (approximately 0.02442357), with no
per-state width normalization. The reviewed normal visible body is 1171 source
pixels wide: 44 pixels in the mother illustration at scene world scale 0.65, or
28.6 world units. Its 766-source-pixel visible height is approximately 18.7 world
units, below the roughly 32-world-unit infantry. The destroyed bounds include
oblique ground spread and must not be treated as the height of a standing obstacle.

Coordinates below are explicit logical frame pixels, not alpha-derived runtime
values. Anchor places the object; groundFront supplies painter depth only.

- Normal: anchor `(735, 820)`; foreground base contact `(1040, 943)`.
- Damaged: anchor `(735, 820)`; foreground base contact `(1040, 944)`.
- Destroyed: anchor `(763, 790)`; foreground base contact `(1185, 913)`.

The destroyed anchor compensates its source registration by moving artwork 28
source pixels left and 30 down relative to the normal anchor. Broken wood retains
the same material scale; the wider debris spread is not squeezed into the normal
bounds. These are initial visually authored contacts for runtime evaluation, not a
claim of pixel-perfect registration. No runtime crop, alpha threshold, bounding-box
centering or fallback supplies scale, contact or gameplay semantics.

## Generation and review provenance

Generated 2026-10-21. The normal reset used `frontier-house.png` as a style reference;
damaged used the normal reset and destroyed used the damaged reset. Accepted source
filenames were `normal-reset.png`, `damaged-reset.png` and `destroyed.png`. All three
were inspected visually and retained byte-for-byte, including genuine alpha.

Generation IDs:

- Normal: `exec-9174836a-79fb-4b3b-80ce-17e7e2b0831d`.
- Damaged: `exec-0c2fc321-9232-4f47-878c-0abf89cba662`.
- Destroyed: `exec-c145d36f-8012-440c-9d24-e4002841aba7`.

The earlier `normal.png`, misleadingly named `normal-clean.png`, and
`damaged-rejected-source.png` were rejected for outer haze and are not registered.
Tiny nonzero-alpha pixels outside the visible wood are left untouched. Inspection
bounds were measurement only and do not define a runtime transparency cutoff.

The integrated Linux sample has now shown all three states under normal enemy AI
attacks. Import, source hashes and native lifecycle checks pass; the focused domain
run also enters the cleared cell after a full save/load. Manual F5/F9 retains the
destroyed state. See `THREE-BRIDGES-SAMPLE.md` for the distinction between actual
play, controlled bridge inspection and domain checks. This is an accepted sample
iteration, not a macOS execution claim or final whole-game visual acceptance.
