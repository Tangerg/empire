# Isolated style calibration

Bulk generation is paused after the user rejected the assembled direction. This directory is a design and evidence sandbox, not a production asset override.

The proposed actual-level fixture is c01-02, world x0–13/y3–12. It includes roads, the river and bridges, trees, village cells, and deployed party members. The first level has no river; its authored scene denies water atlases, so a river must not be presented as that level.

Before more bulk work, compare one representative assembled slice against the approved concept: broad readable silhouettes, restrained cel-shaded forms, coherent character/building/tree scale, authored terrain masses, and actual presentation sampling. Keep tactical data unchanged. Visual size and source density are not automatically constrained to the previous tiny sprite framing.

The production surface currently samples inset3 and bleed0.5. Prior standalone composites omitted that sampling and runtime effects; their isolated alpha and identity checks remain useful but do not establish style acceptance. No new artwork or runtime replacement is included here.

## Exact surface sampling comparison

The existing renderer magnifies by32/26, crops3 logical source pixels on each edge (6 physical pixels at2×), then draws a33×33 nested viewport at[-0.5,-0.5] over the32-unit tile. This removes the one-pixel edge-matched border from view. A factual baseline must reproduce inset3/bleed0.5; an isolated new-art candidate may propose inset0/bleed0.5 but must label that presentation change explicitly. Direct pasted tiles cannot be claimed to reproduce the existing renderer.

## Reproduction and status

`node_modules/.bin/vite-node tools/art/extract_calibration_scene.ts` extracts real c01-02 BoardPieces and unit/building markup. `tools/art/render_calibration_scene.py` accepts `--source`, `--masters`, and `--output`. Point masters at `packages/story-candidate-01/assets/final-fantasy-v1/masters/calibration-v4`. It uses Inkscape for headless SVG rasterization and spells nested SVG clipping explicitly because the renderer otherwise leaks whole atlas sheets.

The four saved variants share camera and placements: factual current renderer sampling, then keep width96/human44, keep width144/human44, and keep width144/human34. Pine visible height90 is shared. All source canvases are retained and anchored by measured source foot points, with no alpha cleanup. Only three objects change style; the rest remains mixed legacy/earlier remake, including diagnosed broken road/water connectors. These are pending art studies, not gameplay screenshots or accepted final style. Normal keep state intentionally substitutes the original owned/captured state for shape comparison.
