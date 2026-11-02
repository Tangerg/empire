# Granary front-left courtyard masonry — static engineering candidate

The same selected assembly gains an independently supported main wall and terminal pier. No runtime implementation, rules, collision terrain, campaign binding or save changes. Official C07 remains unchanged and runtimeReady=false. C06/C07 gameplay is paused; these are only initial-state display clones and offline geometry checks.

## Root cause and physical ownership

The old `granary-front-left-visible` painted mask is not a masonry footprint: it also covers the left timber fence and foreground road. Its flat front=265 hides real Torren at (19,15). The old (314.5,212.5) witness is timber, not stone. Original source and new native reference images preserve this correction. Left timber needs its own future owner and is not silently declared restored by this masonry batch.

Visible stone feet were manually located with approximately ±2 source-pixel uncertainty. Main wall and terminal pier own separate local support curves. The low right original extension conflicts with canonical legal (24,15)/(24,16) foot space and is intentionally omitted, not copied into a new collision rule. The hidden pier rear is explicitly new design: its base vertex (379,246) was narrowed to (379,248), with front/side visible feet and support curve unchanged. This repairs the frozen west-facing idle boot overlap, without altering actor samples. Current pier contact is (369,223), not the previous (369,221).

Runtime surface files own current polygon/UV/contact. Archived source/export helpers are reproducible provenance, not another runtime implementation. No alpha threshold, triangulation guard or source role contract is relaxed.

## Evidence and exclusions

- Final five real appearance controls: Torren front17/main, front195/main, rear98/pier, front86/pier; actual raider rear81/main. Zero wrong depth samples; representative end-to-end Pick checked with ground ownership recorded. Old false-mask witness now picks Torren.
- Two valid negative controls initialize successfully and reach `Granary courtyard real material front/back depth failed`: flat-main-265 reverses17+195 front overlaps; background-pier reverses98 rear overlaps. Failure exit alone is not the evidence.
- Frozen real idle0 boot ROI supplies93 A>=200 lower-edge pixels. Old pier base catches28 at (24,15) west; current base catches0. Twenty canonical legal cells, both idle facings, are free of main/pier footprint. This is one real boot, not all appearances or animation phases. Continuous walking applicability is documented separately; do not interpret idle-west sweep as executed movement.
- Initial finely partitioned cap failed formal degenerate-triangle validation. Continuous cap topology replaced it; failed candidate is preserved and excluded, not counted as a depth negative.
- Intermediate pre-origin-registration launch was interrupted and excluded. Only final positive and final screenshots use contact (369,223).
- Fresh production build passes with0warnings/errors. No action/replay prerequisite or gameplay test was run.

## Art and whole-scene limits

**Final art NOT passed.** Current gray/flat masonry and unsegmented cap are weaker than original warm hand-painted stone; contact shadow and cross-scene material integration remain debt. Stop local texture resampling. This is a bounded geometric candidate, not a top-tier final scene or complete courtyard. Left timber/connected wall remain next responsibility. Total selected35instances/6sites/2612depth entries is not a performance pass. Official55painted groups and all missing foundations remain separate.
