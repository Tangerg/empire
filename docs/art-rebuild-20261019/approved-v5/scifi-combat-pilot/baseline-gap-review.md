# Antler-knight baseline gap: shared-renderer static diagnostic

This is a direct same-scale diagnostic of the existing shared runtimeUnitPicture and boardStripMarkup helpers, NOT a C02 gameplay consumer, C02 scene, live playback, or scene-ready acceptance. It does not import C01 campaign styling or move the canonical anchor.

## Actual shared contract

packages/game-ui/src/art/runtime-raster.ts places the strip at CELL_FOOT minus sheet anchor: [16,31]−[32,63]=[-16,-32]. The shared contact shadow is ellipse cx16 cy29.2 rx12.5 ry4.2, so its vertical extent is25.0..33.4, not a line at anchorY31. Team ring is cx16 cy29.5 rx11 ry2.25. No figure scale or source displacement is introduced by the helper.

The registered projected support is therefore boardX15.568..15.708 and boardY27.770..28.258. Every measured planted-support centroid lies inside the shared shadow ellipse. Thus the common3px canonical-anchor clearance is not automatically a3px empty air gap above the renderer's shadow: the central sole/support region overlaps the upper part of the existing contact ellipse. Exact per-phase normalized ellipse distances are in renderer-support-overlap.json.

## What the proof shows and does not show

renderer-contact-proof.png uses actual helper output at1 logical pixel per raster pixel, with default team tint and ring, all four phases. renderer-contact-proof-3x.png is nearest-neighbor enlargement only. Second row overlays the canonical anchorY31 line and shadow outline. Sources and authored scale unchanged.

At this scale, central fore/hind supports touch or overlap the shadow; no clean empty horizontal band separates the entire unit from its grounding. The wide mounted stance extends beyond the generic25px-wide ellipse, so outer hooves lack local shadow coverage. This static neutral-ground diagnostic supports compatibility with the default helper, but cannot establish acceptance on a real C02 scene or future campaign style. It should not be labeled scene-ready or used to silently resolve the anchor clearance. No actual C02 consumer was discovered or invented.

Inkscape initially failed to honor nested SVG overflow and displayed adjacent frames. The saved proof adds an explicit outer clip exactly matching the helper's declared64x64 viewport; no subject pixels within that viewport are removed. This is recorded in renderer-contact-contract.json. It is a static rasterizer compatibility guard, not a source edit or renderer code change.

## Precise source-correction candidate if required

Do not regenerate the already-good idle. Preserve every current raw/master and provenance file. New versioned imagegen edits for walk-a, attack, walk-b could retain the same1254² canvas, equipment, scale and distinct poses but move/redraw their planted soles onto idle's source baselineY1226, ideally±8px, without clipping the complete silhouette. Required downward corrections are walk-a14px, attack74px, walk-b15px. Maintain genuine pose anatomy rather than stretching just the hooves; preserve native true alpha and all current originals. Do not shrink or independently fit any phase.

After such genuinely authored source corrections, shared renderWidth52 and origin[3,12], zeroY offsets would place all sole baselines near62.84, only0.16px above canonicalY63, with the full canvas ending at64. Current bounded X shifts [0,-2,+2,+1] can remain if remeasured supports justify them. Re-measure actual planted soles, re-run alpha and exporter plan, and inspect phase continuity before selecting replacements. This is a proposal only: NO generation, overwrite, source change, shared renderer change, or guard relaxation was performed.
