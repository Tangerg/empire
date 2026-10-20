# C01 remaining FX source and export audit

## Result

24 pending FX topics are fully mapped to six surviving original boards. The exact 96 source inset crops and 24 full row crops are saved. All 48 current base/HD PNGs reproduce pixel-for-pixel from the cached alpha and legacy builder transformation. All 96 raw crops also match their recorded original-board rectangles. This is source/contract evidence, not approval of the old artwork or a live-game animation test.

9 topics have concrete renderer bindings: FX-01–08 through 23 weapon bindings and FX-17 through heal. FX-09–16 and FX-18–24 are catalog-only in the current consumer search. Each bound weapon’s unit definitions and level-source references are recorded. No new consumers were invented.

## First pilot

Use C01-FX-01 slash only. Source is board 01, row 1, columns 1–4: small crescent → larger sweep → peak crescent → fragmented decay. The opening Twin Hills level includes Laiya and swordsmen using c01.border-blade. This gives a small genuine gameplay test without expanding scope.

## Hard runtime contract

- Four 32×32 logical frames, left-to-right; 128×32 base and 256×64 HD, density 2
- 12 fps; loop only FX-09–12; normal source-over
- Manifest anchor [16,24] is ignored by candidate01FxPicture; actual strip origin [-16,-16] means pivot [16,16] at target cell centre
- Recommended single owner: correct all 24 FX metadata anchors to actual [16,16] and make the renderer derive x=-anchor[0], y=-anchor[1]. This preserves every current [-16,-16] position. Apply metadata+consumer together; honoring old [16,24] would incorrectly shift art up 8 logical pixels
- Regress the full BoardPicture for all 24 topics and a synthetic non-centered anchor. Reject missing/invalid anchors; do not keep a second hard-coded centre fallback
- Clip phase starts are 0, 83⅓, 166⅔, 250 ms. Final frame holds after 333⅓ ms until hit deletion at 420 ms or heal deletion at 500 ms
- Hit rendering independently adds a white burst circle and damage number. Healing adds the amount. Do not bake these separate overlays into art
- Actual image URL resolves the 2× runtime PNG; replacing only base PNG would not update the game

## Export blockers and risks

1. Existing approved frame/registered-sequence exporters reject FX and assume actor/structure phase names. Their alpha gates also require near-opaque pixels. An FX-specific narrow contract is needed; the unit/prop checks should remain strict.
2. The legacy exporter alpha-bbox-fits every frame independently. Slash phase scales are 0.207, 0.145, 0.120, 0.163; this erases authored growth and changes registration. All new phases need one common authored canvas, scale, origin and fixed effect pivot.
3. Real fades can be entirely translucent. FX-02 final phase has maximum alpha 104 at 1× and 118 at 2×. There are 31 existing phase/density combinations below alpha 240. A sprite near-opaque gate must not be transplanted to effects.
4. Source board 06 (FX-21–24) contains cloudy gray/white backing despite RGBA mode. Clean reference semantics survive: damage debris, bolt/nut repair, two-beacon disconnection, neutral-to-blue capture. New masters need genuine alpha without that backing.
5. Board 03 has white source panels, and legacy keyed board families 04–05 show residual purple/chroma degradation in current exports. Raw source crops are references, not finished masters.
6. FX-16’s peak gray ray reaches the old crop boundary. Review overflow rather than repeating the clipping. Loop seams for FX-09–12 require deliberate review.
7. The legacy builder’s assets/manifest-complete.json is absent. The surviving builder, numeric catalog order, visible semantics and exact cached-alpha reconstruction prove the present row/column map. Original-to-alpha chroma conversion was not rerun because it mutates repository data and invokes an external helper.

## Single pivot ownership recommendation

The per-topic FX manifest anchor should own the pivot. Current renderer centering is the behavioral baseline; [16,24] is unused stale metadata. Correcting metadata to [16,16] while making the renderer consume it removes the competing definition without moving old art. Engineering should prove all 24 existing outputs retain geometry, href, clip order, fps, loop, normal blend and target placement, plus a non-centered-anchor test that fails if the renderer still hard-codes centering. This audit made no implementation or manifest changes.

## Style and transparency

Chunky matte shapes, clean silhouettes, restrained palette and short readable effects. Flame, oath light, memory wisps, healing and smoke may have legitimate luminous/translucent pixels. No unrelated opaque backing or wide light pollution. Source-specific physical motifs (two stones, coals, cracked earth, tower, plinth) must be intentional and compact; do not delete identity-bearing motifs or turn them into opaque tiles.

## Files

- fx-source-audit.json: full machine-readable topic/slot/consumer/alpha/provenance audit
- phase-contracts.json: concise 24-topic phase, pivot, fps, loop and blend requirements
- proposed-export-contract.json: precise recommended export and review gates, not implementation
- source-evidence.md: relevant line-numbered source excerpts plus file hashes
- source-runtime-contact.png: six original boards beside all 24 current HD strips
- boards/: original board reference snapshots
- source-crops/C01-FX-NN/: exact original row, original four inset slots and cached-alpha processing evidence
- current-runtime/: exact current 1×/2× snapshots

## Verification and limits

PASS: 24/24 topic and manifest contracts; 48/48 physical dimensions; 48/48 legacy decoded-pixel reconstruction; 96/96 raw crop pixel identity; four distinct decoded frames per topic/density. No repository modifications, worktree operations, imagegen, exporter implementation or new animation generation. Full app tests and live playback were not run in this read-only audit.

## Phase map

- C01-FX-01 斩击 (board 1, row 1): small ivory crescent → broader swept crescent → largest crescent with pointed trailing cuts → separated slender silver fragments; Preserve crescent orientation and open inner arc; expansion and fragmentation must be authored, not whole-image scaling.
- C01-FX-02 穿刺 (board 1, row 2): narrow ivory puncture with two dark chips → longer needle plus outward chips → peak spear-like puncture and chips → faint needle and small residual chips; Keep puncture axis and lower impact point fixed; do not translate the whole frame to fake motion.
- C01-FX-03 箭矢命中 (board 1, row 3): short wooden arrow approaching down-right → arrow tip makes cream starburst → arrow gone; radial cream/brown chips → few tiny dissipating chips; Impact point tracks the arrow tip, not the individual bbox center; no bow or unrelated target body.
- C01-FX-04 钝击 (board 1, row 4): small ochre elliptical ring and side dust → expanded double impact ring → peak dust ring with flying stone flecks → small residual ring/puffs; Use a stable ground-impact center, growing ring then decay; no solid opaque disk.
- C01-FX-05 普通火焰 (board 2, row 1): small orange flame and ember → medium flame with sparks → tall peak orange/yellow flame → small dying flame and loose embers; Hold the flame root fixed while height changes; a flame tongue can be luminous but no rectangular glow.
- C01-FX-06 琥珀誓火 (board 2, row 2): low broken amber rune ring → rising larger segmented ring → complete bright segmented amber ring → ring broken into floating amber fragments; Preserve angular oath segments and open center; this is not generic orange fire or a sunburst.
- C01-FX-07 攻城碎屑 (board 2, row 3): dense airborne wood and stone explosion → fewer falling large pieces → small falling remnants → settled wood/stone heap; Ballistic down/out trajectory then settlement; do not arbitrarily reverse to small-to-large generic four phases.
- C01-FX-08 亡者记忆冲击 (board 2, row 4): cluster of gray human echoes, gray wisps and amber papers → taller overlapping human echoes → faint dissolving echoes and loose papers → wisps and paper fragments only; Preserve ordinary-person memory silhouettes/name-paper semantics; avoid replacing them with a generic blue skull.
- C01-FX-09 秋雨 (board 3, row 1): three descending blue-gray droplets → droplets near ground with first specks → one drop and crown splash → flat residual water specks; Loop must hand off residual specks to new falling droplets; transparent negative space, no white source panel.
- C01-FX-10 河流浅水 (board 3, row 2): two stones with narrow blue ripple → expanded elliptical ripples → largest outer ripple → broken fading ripples around stones; Keep two stone positions fixed; authored outward wave and repeat seam, no opaque water tile.
- C01-FX-11 荒原强风 (board 3, row 3): long ochre wind ribbon with grass flecks → bending gust and scattered flecks → fragmented smaller ribbons → few dust dots; Maintain drift direction and rhythmic seam; muted translucent gusts, not a solid background.
- C01-FX-12 山炉热浪 (board 3, row 4): three thin amber heat wisps above a coal line → phase-shifted wavering wisps → broken shorter wisps → small residual wisps; Coals/heat origin stable; wisp deformation and continuous loop, no full-tile opaque heat haze.
- C01-FX-13 焦土余火 (board 4, row 1): dark cracked earth patch → small cracks glowing and rising smoke → peak sparks/flames from cracks → low remaining embers; Cracked earth is a source-semantic compact motif, not permission for unrelated opaque backing; keep patch stable.
- C01-FX-14 墓园污染 (board 4, row 2): low green-gray fumes above soil/name papers → rising twisting fumes and lifted papers → largest curled pollution plume → low fading wisps; Retain name papers and restrained graveyard fumes; source soil patch is semantic, not a full rectangle.
- C01-FX-15 银林灵火 (board 4, row 3): small pale-green wisp above leaf litter → growing green-white flame → peak leafy spirit flame with curling branch → small residual wisp and sparks; Hold leaf-bed root; preserve sage/leaf identity with controlled translucent light.
- C01-FX-16 七塔灰光 (board 4, row 4): small tower with short gray rays → wider longer tower rays → largest tall gray ray fan and stones → short residual gray beams; Tower is original semantic subject; gray rays come from its amber top. Source peak touches the top crop boundary, requiring overflow review.
- C01-FX-17 治疗 (board 5, row 1): small sage leaf orbit, bandages and plus → larger leaf spiral around plus → peak orbit/plus and bandages → leaves and fading arc only; Keep medical plus and restrained sage motifs; true translucent glow is allowed, no yellow/white opaque panel.
- C01-FX-18 受控 (board 5, row 2): broken amber bands and diamonds → bands align into ring → double closed restraint rings → settled smaller double restraint rings; This is closure/restraint, not burst-and-vanish; loop remains false under current contract.
- C01-FX-19 隐蔽 (board 5, row 3): dense gray-green leafy smoke → smoke curls and separates → broken smoky ribbons with leaves → few floating leaves and wisps; Density should genuinely dissipate; translucent fog can have no near-opaque core.
- C01-FX-20 援护盾 (board 5, row 4): curved blue-gray shield stroke → small amber impact spark on arc → peak spark spray on shield arc → few fading sparks with arc; Preserve narrow blocking crescent and stable contact; do not insert a shield bearer or opaque backing.
- C01-FX-21 结构受损 (board 6, row 1): wood/stone fragments lifting → larger dispersed rubble → peak debris/dust impact → settling dust and a few pieces; Original board has unrelated gray/white vignette backing; recreate fragments/dust only with genuine alpha.
- C01-FX-22 工程修复 (board 6, row 2): tilted bolt and nut with tiny sparks → hammering flash at bolt/nut → upright fitted bolt with blue repair swirl → finished upright bolt and small glints; Preserve assembly progression and hardware; remove unrelated cloudy panel backing, not legitimate spark alpha.
- C01-FX-23 塔网失联 (board 6, row 3): two beacons with intact cyan link → link bends and crackles → broken separated link/square fragments → no link, sparse fading squares; Preserve connected-to-disconnected causal progression and beacon spacing; no opaque source panel.
- C01-FX-24 据点占领 (board 6, row 4): neutral pale banner on plinth → pale banner with blue capture swirl → blue banner and peak capture ring → blue banner with settling ring/sparks; Preserve neutral-to-blue ownership change and fixed flagpole/plinth; no reverse-order or unrelated backing.
