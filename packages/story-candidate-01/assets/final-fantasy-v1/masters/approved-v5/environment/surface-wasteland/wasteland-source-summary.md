# Surface wasteland: four native material sources

Exact original IDs wasteland-1 through wasteland-4 retain indices 0–3 from the second row of surface-regional-4x4.png (board indices 4–7). Parent confirmed exclusive generation after audit on 2026-10-19 at 21:06 UTC. Actual-used indices 1/2 were generated first, then catalog indices 0/3; the manifest order never changes.

All outputs are native 1254-square RGB, fully opaque. Generator/raw/master/named-master copies are byte-identical with verified SHA-256, as are exact sent prompt hashes. No native image crop, rescale, recolor, blur, offset, alpha cleanup, edge lock or texture filtering was applied. Diagnostic contact sheets alone use resampling.

## Identity and current use

- wasteland-1 / index 0: light golden-tan sandy earth with shallow swept bands, stones and dry tufts; catalog-only
- wasteland-2 / index 1: reddish/rust-brown scorched soil with stones and burnt dry tufts; current c01-14 uses 56 cells
- wasteland-3 / index 2: dusty ochre/gray-brown bare rock soil, stones and subtle erosion bands; current c01-03 uses 1 cell, c01-09 uses 1, c01-10 uses 28
- wasteland-4 / index 3: warm ochre parched clay with broad irregular soil plates and narrow dark cracks; catalog-only

Current direct use is inferred from freshly read rule maps, exact selectors and inclusive authoredTerrainSurface exclusions: 86 total legacy surface cells. The old full actual SVG inventory had 28 wasteland pieces in c01-01. Its current map has 25 mountain cells, all covered by authored surface zones, so those are not counted as current old wasteland floor. The historical draw evidence and new derivation are kept separate; no fresh full-scene acceptance render is claimed.

The original atlas contract is 4 × 1, 32 logical / 64 HD, flat continuous ground with no standing source foot, anchor, footprint or obstruction. No lava, fire, pools, glow, metal symbols or new game geometry were invented from the molten terrain label. The original transition list has no wasteland Blob47 pair.

## Review limits

A 256-unit world period remains the initial preview proposal. Accent groups are generally larger/more numerous than requested: non-cracked materials often have 25–35 groups, many 8–25 world units wide. The clay source has only about 3–5 dominant plates across width rather than 8–12, so plates can span 50–100 units. Scale remains subject to preview/scene review.

Color families are distinct, but all new outputs are warmer or brighter than their original swatches. The red scorched variant is notably more orange/red and saturated. Sand is more golden/yellow; dusty soil is lighter ochre; clay is also more golden. The first two generated soil candidates retain fine low-contrast texture and small patch boundaries. Sand and clay have cleaner broad planes. Dusty soil has branching pale erosion bands that may read like paths; these remain material marks, not authored roads. All differences are documented, with no automatic regeneration or code filtering.

Untreated native edge RGB differences are about 4.7–10.8 out of 255. Contacts include unblended repeats, whose boundaries are not natural continuations. No seamlessness or level-update claim is made. Parent owns seam/world-phase integration and actual scene review.

## Deliverables

wasteland-complete-native-manifest.json; wasteland-active-pair-native-manifest.json; wasteland-catalog-pair-native-manifest.json; wasteland-four-source-contact.png; wasteland-color-and-period-qa.json; source-contract.json. Each source directory has unchanged raw/master/named-master, exact prompt, source evidence, alpha/material QA, semantic notes and manifest entry. Active/catalog pair contacts remain for backup and review. No repository edits.
