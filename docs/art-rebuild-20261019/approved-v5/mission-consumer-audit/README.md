# Gray Flag mission consumer audit

Only **BORDER-FARMER** is still-used mission art among the21 remaining topics. It has two idle-only ambient placements in c01-01. There is no second remaining active mission identity.

## Bounded next bundles

1. **C01-MISSION-BORDER-FARMER**: first board, row1, original `[0,0,1254,314]`; idle `[0,0,314,314]`. Older woman, warm tan skin, concerned dark eyes; taupe/olive headscarf tied under chin; patched beige tunic/dress, rope belt, brown trousers and boots. Screen-left hand holds a tied round seed pouch; screen-right holds a short wooden-handled hoe with bent gray metal blade.
   - Existing placements: `(11.5,8.65)`, scale0.70, opacity0.90; `(16.4,9.15)`, scale0.68, opacity0.88, flipped. Both under-units. Keep these authored values.
   - Complete idle/walk-a/carry-or-work/walk-b at shared scale. Only idle is currently consumed.
2. **C01-MISSION-FORGE-ARTISAN**: optional catalog-only collection work; second board, row3, original `[0,768,1024,1152]`; idle `[0,768,256,1152]`. Warm bronze-skinned woman with dark gray-streaked hair in bun and silver round earring; rolled charcoal sleeves, rust-red leather apron, brown gloves and boots. Screen-right hand holds black tongs with a small orange-hot round workpiece; screen-left holds a small blue-gray handled vessel.
   - Actual consumers: zero. Do not rebind c01-14 forge-laborer, which uses the formal bridge laborer.

Exact unresampled RGB row/idle crops are in `original-crops/border-farmer/` and `original-crops/forge-artisan/`. Original magenta is reference-only.

## Current counts for every remaining topic

| Exact topic ID | Actors | Scene placements | Classification | Original board row |
| --- | ---: | ---: | --- | --- |
| C01-MISSION-BORDER-FARMER | 0 | 2 | scene-active-idle-only | farmer-refugees-driver / 1 |
| C01-MISSION-EVACUATION-DRIVER | 0 | 0 | catalog-only | farmer-refugees-driver / 4 |
| C01-MISSION-BAKER | 0 | 0 | catalog-only | baker-miner-artisan-bridge / 1 |
| C01-MISSION-MINER | 0 | 0 | catalog-only | baker-miner-artisan-bridge / 2 |
| C01-MISSION-FORGE-ARTISAN | 0 | 0 | catalog-only | baker-miner-artisan-bridge / 3 |
| C01-MISSION-RELIEF-COOK | 0 | 0 | catalog-only | cook-doctor-wounded-wall / 1 |
| C01-MISSION-CITY-DOCTOR | 0 | 0 | catalog-only | cook-doctor-wounded-wall / 2 |
| C01-MISSION-WOUNDED | 0 | 0 | catalog-only | cook-doctor-wounded-wall / 3 |
| C01-MISSION-WALL-LABORER | 0 | 0 | catalog-only | cook-doctor-wounded-wall / 4 |
| C01-MISSION-SCRIBE | 0 | 0 | catalog-only | scribe-registrar-delegate-messenger / 1 |
| C01-MISSION-NAME-REGISTRAR | 0 | 0 | catalog-only | scribe-registrar-delegate-messenger / 2 |
| C01-MISSION-COUNCIL-DELEGATE | 0 | 0 | catalog-only | scribe-registrar-delegate-messenger / 3 |
| C01-MISSION-FIELD-MESSENGER | 0 | 0 | catalog-only | scribe-registrar-delegate-messenger / 4 |
| C01-MISSION-CARAVAN-ESCORT | 0 | 0 | catalog-only | escort-caretaker-militia-quartermaster / 1 |
| C01-MISSION-CEMETERY-CARETAKER | 0 | 0 | catalog-only | escort-caretaker-militia-quartermaster / 2 |
| C01-MISSION-TOWN-MILITIA | 0 | 0 | catalog-only | escort-caretaker-militia-quartermaster / 3 |
| C01-MISSION-MERCENARY-QUARTERMASTER | 0 | 0 | catalog-only | escort-caretaker-militia-quartermaster / 4 |
| C01-MISSION-CONTROLLED-SOLDIER | 0 | 0 | catalog-only | controlled-veteran-nursery-witness / 1 |
| C01-MISSION-RELEASED-VETERAN | 0 | 0 | catalog-only | controlled-veteran-nursery-witness / 2 |
| C01-MISSION-NURSERY-CARETAKER | 0 | 0 | catalog-only | controlled-veteran-nursery-witness / 3 |
| C01-MISSION-DETAINED-WITNESS | 0 | 0 | catalog-only | controlled-veteran-nursery-witness / 4 |

## Evidence and traps

- All16 levels resolve340 initial actors and14 scripted reinforcement actors. Mission domain art covers9 refugee actors (c01-02/05/06) and2 laborer actors (c01-06/14), all excluded as already formal. No mission reinforcement actors.
- BORDER-FARMER has exactly2 actual scene placements. The third topic reference is populationBindings metadata; nothing renders or spawns it. topicPlacementPiece always draws atlas frame0, so current farmers are idle-only.
- Canonical baseline and old progress say pending for all24, including the two formal remakes. approved-v5/progress.json and current runtime manifests are the reliable current-stage evidence; runtimeReady means loadable catalog, not authored use or accepted v5 art.
- approved-v5 adult progress prose says three c01.refugee actors; the all16 authored count is9. Bridge progress mentions c01-02/14 offline review but real c01.laborer actors are in c01-06/14.
- The original builder TOPIC_SOURCE manifest-complete.json is absent. Source mapping is recovered from the surviving07-topic-catalog.md mission order plus MISSION_BOARDS/build_units order, all six board pixel inspections, and legacy-row pixel verification. Never zip alphabetical canonical manifest rows to board rows.
- Original row identities are coherent with the current miniature preview, but many boards repeat idle and walk poses or drop/swap props in the third/fourth cells. Preserve identity and declared four semantic roles rather than reproducing a false four-frame animation.
- c01-14 forge-laborer is c01.laborer→BRIDGE-LABORER, not FORGE-ARTISAN. c01-05/07 militia-named actors are swordsman/archer/banner-guard, not TOWN-MILITIA. c01-15 controlled actors use druid/longbow/walker combat art; silverwood-witness is c01.druid, not DETAINED-WITNESS. c01-04 saved_witnesses is a signal and escort objective names Laiya/Mirelle, not a witness-unit spawn.

## Audit files

- `mission-consumer-audit.json`: exact IDs, all24 source mappings/crops/hashes, observed identities, metadata, current counts, exclusions and recommendations
- `all16-level-consumers.json`:340 resolved initial actors counted; actual mission actor coordinates and all14 scripted reinforcements
- `original-row-pixel-verification.json`: independently verifies board-row→runtime mapping with pure legacy functions in memory

Read AGENTS.md and PROJECT_RULES.md. Inspected all six original boards. All24 reconstructed legacy rows match both original logical and HD strips pixel-for-pixel; all four saved reference crops preserve exact original RGB pixels. No generation, repo edits, Git operations or live-game acceptance.
