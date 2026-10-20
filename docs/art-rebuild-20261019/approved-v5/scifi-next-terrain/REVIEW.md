# C02 Kairon terrain: four native-reviewed candidates

Generated 2026-10-20 with four independent built-in image_gen requests. No fallback CLI, regenerated completed topic, shared catalog edit, runtime export, manifest change, or Git operation was performed by this worker.

## Selection and identity

Kairon is the next uncompleted four-topic family in canonical manifest order after Farlight and Hesha. Exact IDs were reserved with the integration owner before generation and recorded in reservation.json. The original complete terrain-board-03.png was visually inspected, and the original builder and topic catalog confirm its top-row ordering: train platform, vertical city, vacuum breach, separation ring. Each request explicitly identifies the relevant subject column and uses the approved Farlight cargo material only as a style reference. No reference was cropped or altered.

## Sources and windows

Four 1254×1254 opaque RGB PNGs are preserved byte-for-byte as raw.png and master.png in the C02 masters/approved-v5/canonical-terrain/kairon-* directories. Each directory includes the exact request.json, full prompt.txt, generation-output.json, and provenance.json with reference and master SHA-256 hashes. This was independent material authoring, not extraction or post-generation source cleanup.

Train platform has four declared 627×627 quadrant windows, a/b/c/d = top-left/top-right/bottom-left/bottom-right. Each other source uses its entire 1254×1254 square. Exact proposed entries are in proposed-registration.json. No C02 HD output or manifest is invented.

## Native32 and connected-grid review

The existing exporter whitelist did not include Kairon at generation time. The local build_review.py therefore calls the existing read_material and render_material helpers without changing exporter globals or shared registration. It uses the established Lanczos native32 sampler, one-pixel opposite-edge matcher and historical native32 alpha masks. Master pixels remain untouched.

All four complete sources, all-native32.png, all-zoom2.png, and each black/white/olive connected-grid-native32 image were visually inspected. The strips are genuine 32px cells; zoom files are nearest-neighbor diagnostics, not higher-density assets. Original runtime strips are copied into original-reference, with hashes recorded in native-review.json.

- Separation ring: broad warm-gray arc pavement and dark panel seams remain visible. The material emphasizes the walkable deck, so the original exterior purple void is not reproduced within the opaque source. Historical alpha determines the exterior. Amber accents are subdued at native32; this is not a literal whole orbital ring scene.
- Train platform: four clearly distinct platform arrangements retain cyan circular mechanisms, straight recessed channels and warm deck plates. These are four material variants, not one continuous large scene; connected-grid review shows repeated motifs rather than claiming scene continuity.
- Vacuum breach: navy painted gaps, fractured plates and charcoal conduits distinguish damaged deck from intact city material. The vacuum is opaque navy inside the historical alpha, not newly invented transparent holes or collision geometry. It is deliberately darker but remains distinguishable on black, white and olive backgrounds.
- Vertical city: cyan utility channels and warm-gray deck plates remain identifiable as engineered urban ground. The source converts architectural identity into a ground material, not tall buildings. Visible utility motifs repeat across connected tiles under the historical single-material contract.

Checks passed: raw/master byte equality; source geometry and opacity; exact alpha equality to each original runtime strip; 16/4/16/16 distinct cell hashes; all 416 allowed neighbor edge comparisons have exact RGBA equality (128 per NESW source, 32 for variants). Per-cell pixel/alpha hashes, windows, sample hashes and edge reports are in native-review.json. Equal boundary pixels establish edge compatibility, not perceptual seamlessness of arbitrary large maps.

## Integration boundary

Recommend the four proposed entries for integration-owner review and narrow topic whitelist registration. No runtime files were changed. Preserve topic IDs, 32px cell dimensions, variant order and N/E/S/W mask bit order. This delivery proves material generation and historical topology only, not live continuous-world consumer coverage or new gameplay behavior.
