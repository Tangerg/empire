# Surface / transition completeness audit

Snapshot: 2026-10-19T21:24:00.194070+00:00; main 9dd042c.

## Result

- Original inventory: 36 atlases. This audit checks 8 surface families (32 material slots) and 4 transition families (188 derived Blob47 cells).
- 20 exact original-index native material sources are saved. Existing approved grass is a functional replacement for meadow tone0. Eleven surface roles remain individually unremade.
- All four transition atlases remain byte-identical to the frozen originals at both densities. They need source/material integration, not 188 independent paintings.
- Current usage below is derived from the exact current maps, selectors, shore predicate and authored-surface suppression. It is not a new full-scene pixel/render acceptance.

## Surface status

| Atlas | Native source status | Current consumer / binding |
|---|---|---|
| surface-meadow | tone0 functional replacement; tones1/2/3 pending | Approved grass world base on all16 maps; legacy tone2: 68 hill cells /9 levels; world binding: meadow-base |
| surface-forest-floor | 0/4 original slots individually remade | Overlay source0 baked into 625 legacy forest transition cells /11 levels |
| surface-earth | 0/4 original slots individually remade | Overlay source0 baked into 303 legacy earth/shore transition cells /5 levels; approved road-earth affects dirt-road only |
| surface-old-stone | 4/4 exact natives saved | tone0: 1137 cells; world binding: stone-base, stone-paving |
| surface-snow | 4/4 exact natives saved | No direct surface cells in current selectors/maps |
| surface-wasteland | 4/4 exact natives saved | tone1: 56 cells, tone2: 30 cells |
| surface-graveyard | 4/4 exact natives saved | tone0: 554 cells; world binding: graveyard-base |
| surface-forge-stone | 4/4 exact natives saved | tone0: 252 cells |

## Remaining exact source IDs

- surface-meadow / meadow-2 (index 1)
- surface-meadow / meadow-3 (index 2)
- surface-meadow / meadow-4 (index 3)
- surface-forest-floor / forest-floor-1 (index 0)
- surface-forest-floor / forest-floor-2 (index 1)
- surface-forest-floor / forest-floor-3 (index 2)
- surface-forest-floor / forest-floor-4 (index 3)
- surface-earth / earth-1 (index 0)
- surface-earth / earth-2 (index 1)
- surface-earth / earth-3 (index 2)
- surface-earth / earth-4 (index 3)

## Derived transitions

| Atlas | Current cells / levels | Needed |
|---|---|---|
| transition-meadow-earth | 303 / 5 | Reuse original mask topology with approved source RGB/world UV; active |
| transition-meadow-forest | 625 / 11 | Reuse original mask topology with approved source RGB/world UV; active |
| transition-meadow-snow | 0 / 0 | Reuse original mask topology with approved source RGB/world UV; catalog-only |
| transition-earth-stone | 0 / 0 | Reuse original mask topology with approved source RGB/world UV; catalog-only |

The legacy builder uses families[overlay][0] for every derived transition. The baseSurface label is metadata; its RGB is not painted into the transparent transition by that routine. The current renderer draws both material.blend and CANDIDATE_SHORE using atlasCellMarkup, so approved grass/road earth/stone surface replacements do not automatically update those overlays.

## Priority

1. Forest-floor four sources: 625 active transition cells; c01-15 alone364 and c01-01 126. Preserve green/brown broadleaf, yellow-olive rooted litter, needle/twig floor and brown oak-leaf litter identities. Initial256 preview proposed.
2. Earth four sources: 303 active transition/shore cells. Preserve dry tan, dark damp cross-ruts, ochre longitudinal tracks, and muddy puddled ground. Initial256 preview proposed; separately evaluate any reuse of existing road-earth.
3. Meadow remaining three: meadow-3 is the active broken-grass/stone hill tone; meadow-2/4 are catalog-only. Compare with the existing1024 world grass base before choosing a period.

## Scope inventory: all36 original atlases

- surface-meadow: 4 components; detailed in this audit
- surface-forest-floor: 4 components; detailed in this audit
- surface-earth: 4 components; detailed in this audit
- surface-old-stone: 4 components; detailed in this audit
- surface-snow: 4 components; detailed in this audit
- surface-wasteland: 4 components; detailed in this audit
- surface-graveyard: 4 components; detailed in this audit
- surface-forge-stone: 4 components; detailed in this audit
- transition-meadow-earth: 47 components; detailed in this audit
- transition-meadow-forest: 47 components; detailed in this audit
- transition-meadow-snow: 47 components; detailed in this audit
- transition-earth-stone: 47 components; detailed in this audit
- route-dirt-road: 64 components; listed for scope accounting; status not re-audited here
- route-edge-dirt-road: 64 components; listed for scope accounting; status not re-audited here
- route-mud-road: 64 components; listed for scope accounting; status not re-audited here
- route-edge-mud-road: 64 components; listed for scope accounting; status not re-audited here
- route-stone-road: 64 components; listed for scope accounting; status not re-audited here
- route-edge-stone-road: 64 components; listed for scope accounting; status not re-audited here
- route-forest-trail: 64 components; listed for scope accounting; status not re-audited here
- route-edge-forest-trail: 64 components; listed for scope accounting; status not re-audited here
- water-slow-river: 64 components; listed for scope accounting; status not re-audited here
- water-shallow-stream: 64 components; listed for scope accounting; status not re-audited here
- water-marsh-channel: 64 components; listed for scope accounting; status not re-audited here
- water-icy-water: 64 components; listed for scope accounting; status not re-audited here
- elevation-auto-temperate: 16 components; listed for scope accounting; status not re-audited here
- cliff-modules-temperate: 16 components; listed for scope accounting; status not re-audited here
- elevation-auto-highland: 16 components; listed for scope accounting; status not re-audited here
- cliff-modules-highland: 16 components; listed for scope accounting; status not re-audited here
- forest-existing: 16 components; listed for scope accounting; status not re-audited here
- forest-temperate: 16 components; listed for scope accounting; status not re-audited here
- scenery-regional: 16 components; listed for scope accounting; status not re-audited here
- crossings-fortifications: 16 components; listed for scope accounting; status not re-audited here
- camps-foundations: 16 components; listed for scope accounting; status not re-audited here
- landmarks-large: 16 components; listed for scope accounting; status not re-audited here
- decals-small: 24 components; listed for scope accounting; status not re-audited here
- rural-life: 16 components; listed for scope accounting; status not re-audited here

## Evidence

- surface-transition-completion-audit.json: per-source original crops, hashes, native selection, current world manifests, per-level derived counts, and both runtime density comparisons
- pending-original-surface-contact.png plus original-pending-references/: complete source-board-derived references for the three incomplete families
- Full original boards remain at the recorded frozen baseline paths; raw sources and all revisions remain unchanged
- No repository edits or generation performed in this audit
