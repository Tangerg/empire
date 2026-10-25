# Reed Village: the burning order

`c01-05`, **05 · 焚村令**, closes the first chapter at 苇草村. Canonical
gameplay belongs to its member of `data/grayflag-campaign-levels.json`; the
scene binding is `data/scenes/c01-05.json`, and source-image registration is
`data/scenes/c01-05-painted-ground.json`. The briefing illustration remains
independent narrative art, not a second map registration.

## Preserved mission

The existing primary objective requires Laiya alive, at least two of three
civilian-tagged villagers alive, and seven completed rounds. `surviveTurns: 7`
succeeds at `turn > 7`, normally after enemy turn seven, at human turn eight.
The hard limit remains eleven; the recommendation of ten is separate metadata.
Ordinary elimination victory remains possible earlier.

The one-shot turn-start trigger with `turn >= 4` introduces the original western
inquisitor, shield soldier and archer at (13,17), (12,18), (13,18), and emits
`gray_banner_raised`. Occupied entries retain the existing skip-without-retry
semantics. That signal records the wave, not the victory.

The baseline opening contains thirteen friends and eight enemies. An actual
campaign projects its available heroes and carried health/rank/resources; it
does not necessarily supply that full roster. Civilian orders remain movement
and wait. There is no new escort zone, rescue action, active fire, production,
interception or alarm mechanic. Scorched fields retain existing rough terrain.

The unchanged progression is C04 aftermath → C05 briefing → C05 battle → C05
aftermath → chapter-two briefing. The map does not revive fallen actors or
manufacture prior victories.

## Natural layout and physical ownership

The former 21×15 board becomes a 50×32 irregular river-bank village. Seven
cottages represent the seven original village sites. Four open watch shelters
and a supply cart represent the five original outposts, retaining three allied,
one enemy and one neutral owner. Two broad clearings remain usable for formations
and civilian retreat, rather than being filled with decorative blockers.

The authored domain has 1,044 connected non-naval walkable cells, 256 water cells,
178 blocked cells and 122 outside cells. Seven closed foundations account for 103
blocked cells; major rocks and solid furniture account for the rest. Complete
ground-footprint intersections, not roof centers, define those masks. Visible
foundation edges are approximate; concealed rear corners carry an explicit
eight-source-pixel conservative allowance. Open canopy mouths and between-post
ground are not solid shells. Low farm fencing remains crossable scenery.

Seven bridge-cell centers lie on the actual upper timber planks. A continuous
ordinary route connects southwest bank (7,21) via (7,20), (8,20), (9,20), (9,19),
(10,19), (11,19), (11,18) to northeast bank (12,18). Dirt banks remain ground;
the river is not foot-walkable. The western reinforcement wave arrives on the
village-side approach, not across an enlarged empty travel buffer.

Source registration uses origin (20,32), cell vectors (32,0)/(0,28), and uniform
world scale 1.25, giving 40×35 world-unit steps. The six old elevation-one cells
become zero because this painting does not depict a raised platform. Expansion,
new foundations/water/paths, flattened heights and relocation are intentional
authoring/balance changes. Other chapters may still have genuine high terrain.

Canonical terrain alone owns traversal, cover and height. The painted document
owns visible material/depth parts and independent pointer contours. Cottage
facades target explicitly authored entrances; roofs do not redirect road clicks.
Forty-five logical objects contain eighty-six material parts. Neither physics
nor entrances are inferred from a visual polygon or arbitrary center. See
[the shared contract](PAINTED-INTERACTION-CONTRACT.md) and
[the physical trace audit](art-provenance/reed-village-geometry.json).

## Opening pressure

The final knight deployment is (43,24), on the painted eastern approach. The
eastern civilian at (36,23) has the ordinary three-step retreat through (35,23)
and (34,23) to (33,23), outside initial enemy movement/attack envelopes. This
does not promise subsequent safety: the knight can approach and threaten a
stationary civilian in a later round. The earlier provisional (35,25) knight
position is not retained. Unit statistics, AI and survival timing are unchanged.

## Art and recovery provenance

The 1651×953 mother painting is `assets/native/scenes/reed-village.png`, SHA-256
`a0c0a00c8037605a8abfa24f81d083079e1e466a103d481f4141fa46c4c79bb1`.
It was generated with the built-in image tool from inspected accepted chapter
paintings and C05 narrative art. Cool water, silver reeds, wood brown, slate and
brick-red roofs provide a distinct palette without changing semantic team,
danger or healing colors. It contains no baked people, UI or selection markers.

After the development environment was replaced, this exact image was recovered
and its bytes verified. Author data and checks were reconstructed from published
base `4fa306c93316fa38f6d8d52300aaee946d7de007` and reviewed geometry. The new
canonical C05 digest is
`69f0b09cbe9d5daae16e4f36a063b3168af38146a5a7a0b6607c19b2be3656ab`.
Fresh connectivity checks found eight isolated walkable edge cells in the first
reconstruction. Pixel review identified (14,31) as river/reeds, now water, and
seven isolated western willow-edge cells as non-tactical scenery, now outside.
No invisible crossing was invented; deployment, sites and the bridge are unchanged.
The audit explicitly records these corrections. This does not restore the
earlier Git commits or claim byte identity with the earlier level.

## Save and verification boundaries

The retired 21×15 C05 battle snapshot is incompatible with the new battlefield.
The existing strict loader must reject it atomically; no compatibility adapter
or state repair is introduced. A valid campaign-only save before C05 remains
the supported entry. Current-map saves must round-trip without state/event drift.

The earlier unpublished build had one recorded normal-AI T8 victory, all three
civilians and Laiya alive, with three friendly losses. That historical result
is not a fresh run of this reconstructed checkpoint. Its result screenshot uses
the existing CampaignShell bridge-town illustration, not the C05 battlefield.
Fresh validation results are recorded below; no historical assertion count or
successful route is silently reused as current acceptance.

### Fresh reconstruction checkpoint, 2026-10-25

- The static C05 validator and all six semantic mutation probes passed. The
  retired fixture deep-equals the published 21×15 source. All other fifteen
  campaign level members remain unchanged.
- Official Godot Mono 4.6.3 import passed. The .NET 8 build passed with zero
  warnings and errors. Whitespace formatting and 288 unique script UID checks
  passed after correcting whitespace in the new verifier.
- `ReedVillageChecks` passed 177 assertions with the explicit evidence export:
  all twelve sites, doors versus roofs, complete foundations, water/bridge
  movement, the opening escape, T4/T8 no-AI timing and strict save admission.
  The check is registered in `verify-native.sh`; the full aggregate and all
  chapter replays were not rerun for this data-only checkpoint.
- A fresh real GUI used an explicitly independent C05 entry with no prior
  victories. Default and zoomed-out views were inspected. The eastern civilian
  moved normally from (36,23) to (33,23), then the normal enemy AI completed its
  first turn. At T2 all three civilians had 58 HP and Laiya had 116 HP; thirteen
  friends and eight enemies remained. This verifies opening fairness, not a
  complete seven-round survival route.
- GUI F5 → observed successful F9 → F5 preserved the full campaign, battle and
  29-event sequence apart from save timestamps. The runtime used software
  rendering; the host reported unavailable V-Sync and audio fallback to its
  dummy driver. No missing C05 painting or geometry fault was observed.

Fresh full T8 normal-AI balance, natural depleted-roster C04 carry-over, and
export acceptance remain unverified. The old successful route is historical
context only. No statistics, AI rules or compatibility loader were changed to
obtain this checkpoint.
