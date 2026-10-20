# C03 levee and waterworks terrain sources

## Result and scope

Four independently generated source candidates were independently reviewed and selected by the parent for native catalog integration. Registration remains parent-owned. Each selected source is an opaque RGB 1254×1254 PNG, with byte-identical returned output, `raw.png`, and `master.png`. All four use their entire square as one material window and derive only the existing sixteen native32 NESW masks. They are not authored sets of sixteen distinct materials.

This worker authored only the four reserved `masters/approved-v5/canonical-terrain/<hash>/` source folders and this review directory. No shared catalog, exporter implementation, runtime, manifest, consumer, progress ledger, or Git state was edited by this worker. The parent expanded the explicit `CHINA_TOPICS` table before the real export planner was used. Every actual exporter write below was restricted to `export-fixture/`.

## Original identity evidence

The complete original `masters/terrain/terrain-board-01.png` was inspected, not just the requested cells. The canonical topic catalog (`docs/story-candidates/candidate-03/art-assets/07-topic-catalog.md`) places these four identities at zero-based terrain positions 4–7. The historical builder (`packages/story-candidate-03/assets/final-ancient-china-v1/tools/build_final_ancient_china_v1.py`, `build_terrain`) reads the four-column, two-row board in row-major order and includes all four indices in its connected set. These are therefore second-row columns 1–4, all NESW16 native32:

- `c03-terrain-70b193fa`, 夯土堤顶: rammed-earth levee crest. The old board depicts a dry earthen causeway between water, with timber-reinforced shoulders.
- `c03-terrain-44bffd59`, 石脚渗沟: stone-foot seepage drain. The old board depicts a narrow shallow stream between large bank stones.
- `c03-terrain-769e91a5`, 分洪渠: flood diversion channel. The old board depicts a deliberately cut waterway with earthen banks and timber retaining details.
- `c03-terrain-b7e36bf1`, 水冲决口: water-scoured levee breach. The old board depicts turbulent water rushing through broken earth and damaged timber reinforcement.

The original board and exact original runtime strip bytes are retained in `original-reference/`. All sixteen original cells per topic were inspected on black, white and olive backgrounds. No absent upstream manifest content was claimed as inspected, and no anchor, footprint, pixel-density, HD output or gameplay consumer was invented.

## Genuine generation and bounded correction

Four separate built-in `image_gen.imagegen` generation calls were made, one per identity. The original atlas was subject evidence only. The fully inspected approved Uker memory-overlay and Soler heated-walkway broad-matte-v2 masters were simplification references only, never subject or palette templates. Requests call for Legends of Kingdom Rush-inspired chunky cartoon terrain, exaggerated native32-scale forms, broad matte planes and no granular or mottled microtexture.

The initial levee generation genuinely failed the material-routing requirement: it made only a north–south earth strip, and the native32 east/west arm endpoints consequently contained blue water. The exact failed source is preserved under `70b193fa/candidates/initial/`, with failure provenance and native diagnostics. One bounded built-in edit changed the ground to carry dry earth through all four cardinal edge midpoints and moved water to the corners. The corrected source passed the same derivation and material probes. There was no second edit, regeneration, CLI fallback, manual retouching or procedural source replacement.

Thus the batch has four initial generations plus one bounded genuine edit, not four invocation-only sources. Every selected and retained rejected source preserves the tool's exact PNG bytes. Full prompts, request arguments, returned file paths, reference SHA-256 values, source SHA-256 values and invocation history are recorded alongside the sources and in the inventory.

No source was cropped, filtered, recolored, painted, blurred, contrast-adjusted or resampled after generation. Native diagnostics and runtime-fixture derivation use the existing exporter: full-square Lanczos sampling to 32px, its established outermost-one-pixel opposite-edge average, exact historical alpha masks, and transparent RGB cleanup. These derived images do not modify the source master.

## Visual review

All five complete generated images were inspected, including the rejected levee. The selected native32 strips, 2× nearest-neighbor strips, all sixteen mask shapes, and all four actual-adjacency 8×8 connected-grid diagnostics were inspected on black, white and olive backgrounds. Enlarged diagnostics are not HD deliverables.

- **Rammed-earth levee:** The correction makes the whole visible connector network consistently warm dry earth, with subtle broad horizontal tamping bands. No blue endpoint interrupts the route. The full source has four timber stakes rather than the requested two and more broad shoulder shading than the two-plane target. It contains smooth large-scale color gradients, not granular mottling. The historical central core/narrow arms clip almost all corner water, slopes and stakes, so the native strip reads as a plain compacted-earth network rather than a fully illustrated raised levee. That semantic loss is disclosed; the clean crest material is useful, but no claim is made that native32 preserves the whole levee scene. The single bounded edit is exhausted.
- **Stone-foot seep drain:** The source generated six large stones, not the requested eight, with three-face shading rather than the strict two-plane target. Its earth contains broad secondary patches and its water contains several wide reflections, but no dense grain or tiny rock field. At native32, stone shoulders visibly bracket the blue-gray shallow-water route. It is materially distinct from both the plain earth and the broad open-water channels. The same prominent pair of stones repeats at each junction, producing obvious regularity; occasional stone margins appear like an interrupted lining at cell joins.
- **Diversion canal:** Broad blue-teal water and three large pale current bands dominate the complete source. Smooth ochre corner banks have several large side facets. Its clean water fields are the simplest of the three water sources, with no foam clutter. The inherited mask clips the bank corners nearly entirely, leaving a readable blue current material rather than a banked canal cross section. The repeated current-band pattern is visible in the grid; curves do not imply physically continuous flow through every turn or T-junction.
- **Washout breach:** Jagged cold-water and off-white foam bands are unmistakably more turbulent than the calm diversion channel. The source uses more branching foam shapes and bank facets than the requested minimal two ribbons, but they remain broad painted shapes rather than speckled microtexture. At native32, foaming water survives strongly. Broken timber and most eroded bank mass are clipped, although small brown earth remnants survive at some shoulders. The resulting water network repeats its same central curl and cannot depict one unique narrative breach. It is not a simulation of directional water flow.

All four grids are inevitably regular and ladder-like because they preserve the historical tiny rounded center and narrow orthogonal arms. Exact edge-pixel equality is not equivalent to seamless interior motifs or natural water routing. No historical mask was broadened to conceal these limitations, and no additional source variant was invented.

## Mechanical evidence

`checks.json`, `source-inventory.json`, `native-review.json`, `export-plan.json`, `isolated-export.json`, and `isolated-export-check.json` record the results:

- Four unique opaque1254 selected sources; exact selected returned/raw/master equality for every topic.
- Four full-square `[0, 0, 1254, 1254]` registrations, not source crops.
- Four native32-only plan entries and zero HD outputs.
- Sixteen derived, distinct cells per topic; 512×32 strip per topic.
- Complete alpha strips byte-identical to their original runtime references; all share the unchanged historical NESW32 alpha hash.
- Exact RGBA edge equality for all 64 allowed east/west pairs and all 64 allowed north/south pairs for each topic.
- Coarse endpoint material probes pass: all four levee midpoints are warm earth (`R > B`); all four midpoint routes for each water source are blue (`B > R`). These probes supplement, not replace, visual review.
- The real exporter plan passes against the shared tree and isolated proposal. Four private fixture exports and four private `check=True` exports pass. Shared runtime files were not exported by this worker.

`build_review.py` reproduces the checks and private diagnostics. `prepare.py` documents the initial requests and refuses to overwrite an existing reserved source directory; it is not a routine rerun command. `proposed-registration.json` is the four-entry merge proposal, not a live catalog.

## Handoff and remaining ownership

The parent independently inspected all four complete selected masters and black/white/olive native strips, confirmed the dry levee midpoint routes, retained stone edges, and calm-channel versus foaming-breach distinction, and accepted these as native catalog materials with the masking/repetition limitations above. This is not a declaration of runtime integration or live scene verification. Parent owns catalog merge, formal export and binding, combined pack/scene review, ledger update and Git checkpoint. Preserve the disclosed clipped levee/bank semantics, repeated junction motifs and non-continuous current direction in that decision. If the corrected levee is rejected for native identity, hold it rather than attempt another source edit within this batch.

## Integration-owner acceptance

Independently inspected all four full1254 master canvases and native32 strips on black/white/olive. The selected genuine levee correction restores dry earth at every cardinal midpoint; stones remain visible along the narrow drainage material, and calm channel versus foaming breach is clearly distinct. Four existing canonical identities are selected and formal-export/check verified at native32 with the historical alpha unchanged. Masked corner context and repeated currents/junctions are material limitations, not a claim of completed scene waterworks. Original rejected levee bytes remain preserved. The private export-fixture is reproducible and ignored to avoid duplicate source storage; verification JSON remains tracked.
