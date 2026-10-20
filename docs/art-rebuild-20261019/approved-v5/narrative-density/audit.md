# Fullscreen narrative scene density audit

## Finding

The 512×288 ceiling is an artificial tactical-density constraint. It discards authored detail before desktop rendering. It is not explained by an intentional blur on the visible foreground.

All five initially inspected formal scenes (CH01-S05, CH01-S01, ARCH-THREE-BRIDGES, ARCH-REDSTONE, ARCH-SILVERWOOD) have RGB 1672×941 masters whose SHA-256 matches source-overrides.json. Their explicit crop `[4,2,1668,938]` retains 1664×936. Every inspected 512×288 export matches a LANCZOS downsample of that region exactly. Parent is concurrently adding CH01-S02; a transient missing S02 runtime file was not treated as a finished-work defect.

At 1440px viewport width, 512px art is enlarged at least 2.8125× in CSS pixels; at 1920px, 3.75×. Device-pixel ratio further increases physical demand. The source-native 1664px region reduces those factors to 0.865× and 1.154× respectively. It supplies 10.5625 times as many pixels as the current HD file without inventing detail. Native source does not promise pixel-for-pixel 1920px/Retina coverage.

## Exact consumers and CSS

- `packages/story-candidate-01/src/presentation/candidate-01-story.ts:13–32`: menu, story and chapter selectors all use the same `candidate01AssetUrl` resolver
- `apps/game/src/main.ts:224`: title image. Actual stylesheet is `packages/game-ui/src/styles/app.css:767–782`, not `apps/game/src/app.css`; `.title-art` fills its title container, `object-fit:cover`, center 46%, image-rendering auto, saturation/contrast only
- `apps/experience-lab/src/main.ts:42`: menu image. `experience.css:10–24,50–54`: entry at least 100vh; `.experience-art` covers full width/height, centered, mobile 60% center; saturation/contrast only. Blur at line38 affects small foreground panels
- `packages/game-ui/src/ui/campaign-game.ts:303–314`: shell emits the wash first and the opaque foreground second. `packages/game-ui/src/styles/campaign.css:180–211`: fixed viewport backdrop inset 66px at top; both images cover full box; `.campaign-backdrop-wash` alone has blur(18px), opacity .72 and scale(1.08); `.campaign-backdrop-art` only saturation/contrast. The foreground covers the wash
- `campaign-game.ts:317–343`: story uses explicit story scene; `346–357,399–406`: choices/results use chapter art; `360–396`: staging background uses chapter art and its hero independently uses the briefing scene. `campaign.css:467–494,741–750`: staging container max1120px, hero columns .92fr/1fr with min280px, min-height245px, unblurred cover; mobile becomes one column

Dark overlays and cards conceal some pixels but do not make the unblurred scenic area resolution-independent. No CSS modification is needed to correct this export loss.

## Responsible constraints

- `candidate-01-assets.ts:73–77,153–169`: root manifest density and every entry must agree, currently2
- `tools/art/export_overrides.py:193–203`: scene HD explicitly hardcoded512×288/2 and root density2
- Same exporter line268: directory selection uses `density == 2`, so allowing6.5 above without repairing routing still rejects runtime-hd paths
- `candidate-01-narrative-scenes.test.ts:23–24` and `test_narrative_scene_overrides.py:237–253` encode the old constraint
- `tools/art/README.md:537–554` already states512×288 is the existing convention, not fullscreen resolution

## Smallest coherent contract

1. Preserve canonical256×144, IDs, kind, paths, gameplay metadata, composition, crop and immutable master bytes
2. Keep existing HD registry filename, URL resolver and Vite glob. Change schemaVersion to `candidate-01-runtime-hd-v2`; remove root `pixelDensity`. Existing required per-entry `{topicId,png,width,height,pixelDensity}` becomes the only physical-density authority
3. Validate finite density≥2, positive integer physical dimensions and exact physical=logical×density. Scene records must be existing unframed256×144/16:9 scenes. Tactical/non-scene records remain strictly2×. Keep manifest count/duplicate/path/existence checks
4. Register only reviewed scenes at1664×936 with density6.5, preserving runtime-hd/narrative-static paths. Never set a global6.5, round down to an arbitrary1536, upscale masters, or enlarge canonical256/HD512 files
5. Export canonical independently from source. Native physical output copies the reviewed crop's pixels; smaller explicit physical sizes, where requested, resample that same region. Any physical size exceeding the region fails before writes. Route by canonical/HD role rather than `density == 2`. Preserve all alpha/hash/crop/atomic-plan gates
6. Update the historical HD builder's emitted schema without running it: its legacy regeneration is not an approved way to preserve current sources/registrations

No new scene manifest/resolver, duplicate source-of-truth field, CSS sharpening/blur change, or art regeneration is necessary. Fractional6.5 is a physical/logical sample ratio, not CSS zoom or an integer-only sprite scale.

## Verification plan and limits

- Export regression:1672×941 + explicit crop -> unchanged source/canonical and exact1664×936 RGBA crop pixels, density6.5; --check passes without writing; repeat identical.2× scene sampling still independent of canonical
- Reject nonfinite/boolean/zero/negative/below2 density, fractional/nonpositive/mismatched physical dimensions, non16:9 scene, upscaling, root global-density resurrection, incorrect schema/count/duplicates/path, altered raw hash or transparency; maintain all-or-nothing planning
- Runtime fixture proves same menu/story/chapter URL chooses registered6.5 file, preserves canonical256×144, and throws rather than fallback when missing. Reject non-scene density changes. Guard unchanged tactical records/files at2×
- Run focused Python scene/card/static export tests, focused TS asset/scene/runtime/catalog tests, typecheck; parent reruns against final integrated six registrations and builds actual apps
- After binding, check title/lab/story/choice/staging/result at desktop/mobile cover positions and DPR1/2, request URL/natural dimensions, framing, load time/memory. A1664×936 decoded RGBA image is about5.94MiB versus0.56MiB for512×288
- Preserve four independent stages: raw, export, runtimeBinding, liveGame. This audit establishes source/export/consumer facts; no browser was launched and no live-game acceptance is claimed

Audit initially read-only in empire-art-rebuild-20261019; output written only here. A subsequent parent instruction authorizes bounded code work in the separate clean sparse empire-layout-engineering-20261019 tree. Main/source/catalog/progress writes remain owned by the parent.
