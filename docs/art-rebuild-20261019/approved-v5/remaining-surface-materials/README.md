# Remaining original surface roles

## Decision

Keep128 logical units for the three newly active world materials: meadow-stony (original meadow tone2), wasteland-scorched (tone1), and wasteland-scree (tone2). Physical exports remain512/1024 and unchanged approved sources supply all RGB.128 makes decorative stones/scrub subordinate to actors and existing standing scenery.256 was compared in completec10/c14 scenes and unobstructed material planes; its larger accent shapes do not justify replacing128. No source recoloring, filtering or new generation occurred.

This is a role-scale integration acceptance only. Scorched ground still contains substantial repeated small scrub/pebble detail and residual fine soil texture. The4-cell128-period motif is visible in an unobstructed plane. Broad concept-level simplicity and natural seamlessness are not asserted. Existing hard terrain boundaries, hill occlusion and densec14 forge/construct/prop overlap are unchanged; full composition and live/GPU acceptance remain pending.

## Exact role and counting contract

- Meadow-2/3/4 add3 exact original-index source identities. Only meadow-3 (zero-based tone2) is currently consumed:68 non-authored hill cells across9 maps
- Wasteland-1/2/3/4 add4 identities. Wasteland-2/tone1 is56 c01-14 scorched cells; wasteland-3/tone2 is30 non-authored mountain cells (c03:1,c09:1,c10:28)
- Snow-1/2/3/4 add4 identities, all catalog-only. No snow terrain, blend consumer or snow scene is invented
- Three active roles affect154 cells across10 maps; eight new roles remain catalog-only
- Registered unique environment identity count advances175→186. Canonical topic count remains315
- Meadow-1 uses the existing approved grass-material functional replacement. It is exported in the four-slot meadow catalog but is not counted again as a newly authored original-index identity. All32 surface catalog roles are covered by31 exact-index native sources plus this existing base

progress-update-fragment.json is a merge fragment for the coordinator: replace/add the three environmentCells entries, add materialStages.open-ground-world-materials, and update only materialStages.meadow-base.limits. Preserve other current progress fields; regenerate the existing scope ledger.

## Verification

-23 focused TypeScript tests pass;21 Python surface/world export tests pass; typecheck passed
- Explicit wrong wasteland tone0 mutation failed with the original-tone guard; exact bytes restored
- All16 campaign level/state hashes and both standing-layer hashes are identical; only the expected10 ground projections change
-22 existing terrain/manifest/mask input files retain exact byte hashes; no masks, geometry, terrain tags, standing coordinates, targets or rule code changed
-60 shipped-level event/outcome replay streams are byte-identical: SHA2563ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9
- Nine actual renderer-versus-continuous-plane comparisons pass with maximumRGBA difference0: three materials at formal128 period/display1x and2x, plus three at diagnostic256/display1x
- Complete actual BoardView/SvgBoardSurface snapshots retain every actor, building, named prop, depth order and layer count on c03/c09/c10/c14. c10/c14 also retain complete256 comparisons
- Parent verified113 full-tree architecture/open-ground/field tests. The worker's own architecture run reached103 passes with only the sparse checkout's45 unavailable documentation links failing; no product failure was suppressed

## Evidence

- before-03/09/10/14 and after-03/09/10/14: complete native1x PNGs, SVGs and exact source provenance
- diagnostic-256-10/14: complete alternative-scale PNGs/SVGs/provenance
- c01-10-128-versus-256-crop.png and c01-14-128-versus-256-crop.png: explicitly labeled nearest-neighbor2x crops from complete scene renders; they are scale comparisons, not substitute scenes
- plane and plane-256: unobstructed actual shared-renderer tiles and independent full-plane fixtures
- full-scene-comparison.json, scope-comparison.json, plane-comparison.json, replay-comparison.json: measured invariants
- consumer-roles.json and registration-plan.json: exact original slot and immutable source mapping
- verified-native-sources.json and formal-world-manifests.json: approved source hashes, formal manifests, actual SVG-observed periods/counts
- focused-tests.txt.gz, tone-mutation-rejection.txt.gz and replay-before/after.txt.gz: retained verification

SVGs and text evidence have gzip companions for compact repository archival; no original source or evidence file was deleted. Large sources/runtime PNGs stay in the coordinator's checkout. Offline Inkscape raster evidence is not a browser/GPU screenshot, includes initial poses only, and preserves all existing standing occlusion. Browser socket denial was not retried or bypassed.
