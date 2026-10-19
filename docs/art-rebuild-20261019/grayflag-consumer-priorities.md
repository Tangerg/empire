# Gray Flag consumer coverage — static source map

All 404 canonical C01 topics are included in the CSV and JSON. 113 have explicit production source or authored-scene references; 291 have no static reference found in the scanned scope. These are NOT 113 observed renderings or 291 proven unused assets. Tests, comments, generated asset manifests and tools were excluded from reference counts. The scan covered apps/packages TS/TSX/HTML/JSON, with asset JSON limited to authored scenes. Each hit retains path and line.

## Priorities within the already authorized full remake

1. World scenery/ground: active terrain is composed from the separate environment-builder kit, not the 32 canonical legacy terrain sheets. Surface-meadow, forest canopy/floor cells, connected dirt roads and river, transitions and cliff modules have broad battlefield impact. Keep connected mask indices, variants, cell size and anchors. The environment JSON covers 36 atlases; 27 have explicit atlas/cell references. Atlas references can select multiple cells dynamically; no reference is not proof of dead cells.
2. Common board units: SWORDSMAN, ARCHER, BANNER-GUARD, HEAVY-KNIGHT, LEGION-SHIELD and others in UNIT_ART. 29 combat-unit sheets and 3 mission-unit topics have explicit bindings/placements. Maintain idle/walk-a/walk-b/attack roles; static illustrations cannot replace multi-frame sheets.
3. Buildings: 10 interactive-structure topics have explicit bindings/placements (list below). The common castle/village/barracks path makes LORNE-KEEP, JOINT-GRANARY and MILITARY-REGISTRY particularly relevant. Preserve normal/damaged/captured states.
4. Battle props: prioritize COVER-1/COVER-2, AFTERMATH-2/AFTERMATH-3/EVIDENCE-1 plus opening authored scene's LOGISTICS-1/LOGISTICS-3/FACTION-2/FACTION-3. No placement changes are needed or authorized by this map.
5. Narrative: menu CH01-S05, seven character portraits and chapter backgrounds have explicit consumers, but improving them does not fix battlefield ground.

## Dynamic path limitations

- candidate01StoryArt(topicId) and candidate01TryIconMarkup(topicId) accept runtime string IDs. External/composed content may supply topics not visible as literals.
- Binding-table references prove support for a semantic unit/structure/weapon, not that every shipped battle creates that entity.
- All assets are eagerly registered; registration/import is availability, not visible use.
- State-dependent effects, markers, structure states, connected masks and variants are selected during play. Their actual frequency is not measured here.
- Actual Chromium capture remains blocked as documented in README.md. No Mac, gameplay changes or additional visual acceptance inferred.

## Buildings with explicit consumer paths
- C01-STRUCT-JOINT-GRANARY: 三方共管粮仓
- C01-STRUCT-LORNE-KEEP: 洛恩堡垒据点
- C01-STRUCT-MERCENARY-DEPOT: 佣兵补给仓
- C01-STRUCT-MILITARY-REGISTRY: 军籍登记点
- C01-STRUCT-OATH-TOWER-CONSOLE: 誓约塔控制台
- C01-STRUCT-REFUGEE-SHELTER: 难民避难棚
- C01-STRUCT-REPAIRABLE-GATE: 可修城门
- C01-STRUCT-SUNKEN-BELL-SLUICE: 沉钟水闸
- C01-STRUCT-WASTELAND-CHEVAUX: 荒原拒马
- C01-STRUCT-WOUNDED-SHELTER: 伤兵棚

## Environment kit references
- surface-meadow: 2 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-forest-floor: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-earth: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-old-stone: 5 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-snow: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-wasteland: 5 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-graveyard: 3 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- surface-forge-stone: 3 explicit atlas references, 0 explicitly referenced named cells; declared components 4
- transition-meadow-earth: 5 explicit atlas references, 0 explicitly referenced named cells; declared components 47
- transition-meadow-forest: 4 explicit atlas references, 0 explicitly referenced named cells; declared components 47
- transition-meadow-snow: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 47
- transition-earth-stone: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 47
- route-dirt-road: 2 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-edge-dirt-road: 2 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-mud-road: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-edge-mud-road: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-stone-road: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-edge-stone-road: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-forest-trail: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- route-edge-forest-trail: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- water-slow-river: 2 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- water-shallow-stream: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- water-marsh-channel: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- water-icy-water: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 64
- elevation-auto-temperate: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 16
- cliff-modules-temperate: 1 explicit atlas references, 9 explicitly referenced named cells; declared components 16
- elevation-auto-highland: 0 explicit atlas references, 0 explicitly referenced named cells; declared components 16
- cliff-modules-highland: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 16
- forest-existing: 1 explicit atlas references, 8 explicitly referenced named cells; declared components 16
- forest-temperate: 1 explicit atlas references, 15 explicitly referenced named cells; declared components 16
- scenery-regional: 0 explicit atlas references, 9 explicitly referenced named cells; declared components 16
- crossings-fortifications: 1 explicit atlas references, 7 explicitly referenced named cells; declared components 16
- camps-foundations: 1 explicit atlas references, 9 explicitly referenced named cells; declared components 16
- landmarks-large: 1 explicit atlas references, 0 explicitly referenced named cells; declared components 16
- decals-small: 1 explicit atlas references, 17 explicitly referenced named cells; declared components 24
- rural-life: 1 explicit atlas references, 13 explicitly referenced named cells; declared components 16
