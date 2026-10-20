# Chinese battle props, batch 02

Four candidate masters and native 64×64 review exports are ready for the main integrator. No shared catalogs, manifests, runtime files, progress records, or Git state were changed by this worker.

## Source and identity review

Each topic was generated in one separate built-in image_gen.imagegen call, using the complete original Chinese props board for identity and the approved bprop-engineering-1 master solely for chunky style. Raw and master PNG files are byte-identical, 1254×1254 RGBA. Exact prompts, prompt hashes, input-reference hashes, call IDs, raw paths, and source/export alpha measurements are preserved beside each master.

- c03-battle-prop-27872fa1: round bronze military token, open square center, broad curling relief, brown cord and ochre tassel. Motif matches the full-atlas fourth-row fifth object. The earlier broken runtime crop cannot establish exact historical atlas-slot mapping; this is an explicit semantic reconstruction, not a claim of verified crop provenance.
- c03-battle-prop-2e0af0f2: open wooden field medicine chest, cream bandage bundles, green and brown ceramic jars, red pouch and hanging strap. No modern medical cross or writing.
- c03-battle-prop-2e7df413: ivory-and-blue navigation banner with unmistakable curling wave, full pole and torn cloth edge. No substituted dragon or foreign heraldry.
- c03-battle-prop-2f038eea: black iron camp cauldron with two handles, three coarse food pieces and wooden ladle, supported over a small stone/log hearth with contained flame. Hearth is functional, not a rectangular ground tile.

## Native inspection and caveats

Inspected all four generated full sources and native-review-white-black-olive.png at its actual 64×64 tile size. Silhouettes, intended motifs, and component colors remain identifiable against all three backgrounds. No apparent clipping, opaque rectangle, bright fringe, detached debris or generated text. Actual transparency remains in gaps, including the token center and cauldron handles.

The token and cauldron contain more mottled/faceted surface shading than the ideal very broad two-tone reference; this is a real source-style caveat, not pixel-cleaned away. At 64×64 it largely collapses into readable broad shading, and is not judged an integration blocker. The medicine chest has mildly bright ceramic facets. Wave banner has more fold facets than the requested three, but the wave motif remains clear. Exact requested hearth stone/log counts are not asserted because overlapping/occluded forms make them uncertain. These are candidates for integration review, not a claim of live-scene approval.

## Mechanical verification

All four pass existing validate_alpha. Token and cauldron source outer edges are fully alpha zero. Chest has 59 source edge pixels at alpha 1; banner has 7 at alpha 1. These negligible native-output residues are retained, not cleaned. All four 64×64 review export outer edges are alpha zero.

Exports exclusively use existing tools/art/export_overrides.py fit_canvas(source, (64,64), 2): full authored canvas contained into 60×60, centered at (2,2), LANCZOS. No crop, recolor, geometry changes, alpha stripping, cleanup, or runtime modifications.

Integration input: integration-candidates.json. Detailed inventory: delivery-manifest.json. Byte identity/alpha verification: verification.json. Generation and persistence are reproducible from prompts.json and persist.py without rerunning generation (raw outputs must remain present).
