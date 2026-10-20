# C02 Milight terrain: four native-reviewed candidates

Generated 2026-10-20 with four independent built-in image_gen requests. No CLI fallback, existing-source regeneration, shared catalog edit, runtime export, manifest change or Git operation was performed by this worker.

## Selection and original identity

The next uncompleted canonical manifest-order family after Farlight, Hesha and Kairon is Milight. Exact reserved IDs are c02-terrain-milight-corona-service (variants), c02-terrain-milight-inverted-city, c02-terrain-milight-rotating-blackout and c02-terrain-milight-vein-ring (NESW-16). Reservation and exact requests are in reservation.json.

The complete original terrain-board-04.png was inspected, with original builder and topic-catalog order confirming its bottom row as corona service, vein ring, inverted city, rotating blackout. Each request identifies the relevant original column. The unchanged full atlas and original runtime strips are preserved in original-reference; original-strips-native32.png was inspected. Original strip hashes are in source-inventory.json, with full reference hashes in each master's provenance.json. Farlight cargo master is used only as the approved chunky matte style reference.

## Sources and deterministic windows

All four generated images are 1254×1254 opaque RGB PNGs, preserved byte-identically as raw.png and master.png. Exact tool requests, complete prompts, generated-output path and provenance are beside each source in masters/approved-v5/canonical-terrain/milight-*. No pixel cleanup, color repair, source cropping, aspect fitting, stretching, or hidden master rescaling was performed.

Corona service has exactly four declared 627-square windows, a/b/c/d ordered top-left, top-right, bottom-left, bottom-right. The other topics each use the entire 1254-square master. proposed-registration.json contains the exact hashed entries. No C02 HD output is created.

## Native32 verification and visual review

build_review.py uses the existing read_material and render_material helpers directly, without extending shared registration. Native32 Lanczos sampling, historical alpha masks and the established one-pixel opposite-edge matcher are unchanged. All complete masters, all-native32, all-zoom2 and all four connected-grid-native32 previews were visually inspected. Grid previews use genuine adjacency-derived NESW states on black, white and olive; zoom files are nearest-neighbor diagnostics, not HD assets.

- Corona service retains bronze/amber service channels, circular fittings and charcoal vents on warm gray deck. Four layouts remain visibly distinct. Repeated service motifs are deliberate material variants, not a continuous bespoke scene. The original sun/void scene is translated into amber accents, not reproduced as a celestial object.
- Inverted city retains pale radial plaza, cyan utility caps and dark structural seams. The master includes small purple botanical-looking inset shapes despite a prompt asking for flat purple accents; these are minor baked-in reference-derived decoration and do not define gameplay obstacles. Historical narrow masks exclude most of that decoration. Towers and inverted architecture are intentionally translated into walkable ground rather than perspective structures.
- Rotating blackout preserves charcoal curved gravity plates and sparse amber emergency lights. The original was almost black; the remake makes plate relief somewhat more readable, while remaining clearly the dark family member. At native32 on black it has low contrast and an understated central round plate, so this is a consciously dim material, not a bright walkability overlay. Any gameplay selection or accessibility overlay remains a consumer responsibility.
- Vein ring retains vivid cyan channels, central ring and dark engineered borders. Native masks favor the cyan central channel over the pale exterior deck. Its flow is a strong cyan network, not a literal complete orbital ring scene; the water-like energy rendering derives from the original reference. Repetition of round junctions follows the historical one-material contract.

Distinct cells are 4/16/16/16. All four alpha arrays equal the original runtime strips byte-for-byte; every allowed opposite-edge RGBA pair passes exactly (16 east-west and 16 south-north for variants; 64 each for each NESW source). All 16 NESW states remain distinct and use the original N/E/S/W bit order. native-review.json records cell hashes, sample windows, original hashes and edge evidence. Exact pixel boundary equality proves topology and boundary matching, not perceptual seamlessness of every large layout.

## Integration handoff and limits

Recommend the four proposed-registration.json entries for integration-owner review and native32 export. Preserve IDs, dimensions, tile modes and orders. No shared exporter was run by this worker. No C02 continuous-world consumer, new collision/path semantics, live browser gameplay or HD manifest is claimed by this material delivery.
