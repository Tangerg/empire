# Remaining C01 combat audit

Read-only snapshot: 2026-10-20 00:09 UTC. Repository formal progress is **321 total / 38 of 40 combat topics**. Exactly two combat topics remain: **SHAMAN and WYVERN-RIDER**. Both are now assigned to exclusive workers; do not duplicate either. **IVRA-GROWTH is already formal.**

## Deliverables

- `audit.json`: current manifest contracts, original source hashes and exact slots, remaining-topic identity hazards, equipment refusal provenance, protected seven human appearances, and precise Ivra age-selection trace
- `consumers.json`: every combat topic's actual domain binding and materialized initial/reinforcement consumers across four builtin and sixteen C01 levels
- `ivra-age-continuity-side-by-side.png`: original Ivra portrait01/02/03, original combat idle and accepted native idle; inspected after rendering
- `references/`: unresampled source crops, for reference only
- `collect-consumers.cjs` / `build-audit.py`: reproducible audit scripts; output only to this shared folder

## Assignment and source contracts

| Topic | Original high-resolution source | Current formal geometry | Actual consumers | Assignment |
|---|---|---|---|---|
| SHAMAN | `masters/combat-atlases/combat-atlas-axebreaker-golem-shaman-javelin.png`, RGB 1254×1254, third row, combat index26 | Four 32×48 frames; 128×48 strip; HD256×96; anchor16,47; art footprint1×1 | No domain unit, art binding, initial placement or reinforcement | `animate_grayflag_shaman` now owns separate combat work |
| WYVERN-RIDER | `masters/combat-atlases/combat-atlas-colossus-ivra-wyvern-dragon.png`, RGB1254×1254, third row, combat index38 | Four64×64 frames; 256×64 strip; HD512×128; anchor32,63; art footprint2×2 | No domain unit, art binding, initial placement or reinforcement | Existing active worker reserved; proposed quartet is128×80 at54/880.5 shared scale |

Both original boards use columns idle / walk-a / attack / walk-b. Exact old five-pixel-inset grid cells are [5,632,309,935], [319,632,622,935], [632,632,935,935], [945,632,1249,935]. These are legacy builder boxes, **not reliably complete anatomical boxes**. Shaman next-row javelin tips intrude near y925; its clean whole subject row is [0,627,1254,910]. Wyvern spear starts above grid row3 and next-row ancient-dragon horns intrude below; active worker correctly uses row[0,590,1254,885], idle[0,590,309,885]. Full original boards remain the authority.

Shaman is an olive-skinned pointy-eared humanoid with tied dark locks, fur-trimmed brown/rust layers, a laced drum and tall crescent/fork staff. The old attack is nearly idle-like. No implemented rule proves a particular spell or weather action.

Wyvern's original mount is **wingless and four-legged**, under a brown-ponytailed human rider with scarf, spear/pennant, reins and saddle. The concept document calls the topic flying, but no corresponding domain rule exists. Preserve the original grounded anatomy; don't invent wings or a two-legged mount from its name.

### Equipment refusal stays separate and held

`C01-EQUIP-SHAMAN` has a recorded imagegen HTTP400 output rejection, category other/abuse, request97a8e076-6f8b-4f88-b718-a2925d12242d. Its failed prompt and status are preserved in `masters/remake-20261019/equipment/c01-equip-shaman/`. It is a drum plus short forked featherstick icon, separate from the complete combat humanoid and source board. This audit found no saved combat generation/refusal history at the inspected snapshot. It made no generation call, retry or safety-clearance claim; the equipment item remains held.

## Actual deployment, not catalog coverage

Data inspection materialized **401 initial actors and14 scenario-spawn actors** across20 levels. All401 initial actors have art bindings. Twenty-nine combat topics have actual bound placements; eleven are catalog-only: AXE-BREAKER, BERSERKER, CANNON-WAGON, EAGLE-SCOUT, JAVELIN-HUNTER, LANCE-CAVALRY, SHAMAN, SPEARMAN, SPIRIT-FIRE, WHITE-STAG-RIDER and WYVERN-RIDER. Export/formal progress alone does not mean a unit is deployed. Hero appearances can consume a base topic without displaying its generic troop art.

## Ivra: continuity hazard, not missing canonical work

Accepted IVRA-GROWTH retains one same-age four-pose red dragon at56/950.5 scale, logical128×80, native1586×992-class sources and HD1024×160. The original combat board is row2. Source contract preserves four legs plus two wings, gold eyes, gray natural belly plates/horns, white left-neck scar and no rider/chains/collar. Existing README's loose “collar scar and armor” wording conflicts with that more precise source contract.

Actual initial placements: c01-09 neutral `caged-ivra` at7,6; c01-10 allied `campaign-ivra` at11,8; c01-11/12 at5,11; c01-13/14/15 at5,12; c01-16 at5,13. No builtin or reinforcement placement.

All original portrait ages are on `masters/narrative/characters/character-board-03.png` (1254×1254):

- IVRA01 juvenile: top-right, crop[632,5,1249,622]
- IVRA02 young dragon: bottom-left, crop[5,632,622,1249]
- IVRA03 adult commander: bottom-right, crop[632,632,1249,1249]

Current story dialogue and HUD/roster/codex **always select IVRA01**. Battle maps c01.ivra directly to IVRA-GROWTH without appearanceId. No rank/chapter/age selector exists for Ivra, no Ivra entry exists in the hero-appearance manifest, and no shipped source selects portrait02/03. Concept chapter4–6 growth/riding plans are not implemented by the shipped chapters1–3 campaign. Four animation cells are action poses, never growth stages.

The inspected side-by-side shows card01's round-eyed hatchling/tiny horns and wings against the accepted battle dragon's long horn crown, angular head and broad wings. The battle contract calls it juvenile; current progress already records a maturity-mismatch risk. Resolve continuity deliberately in the narrative phase. Do not infer adult identity, overwrite the accepted quartet, add canonical counts or silently activate later-age cards.

## Named humans and remaining evidence gap

Protect all seven formal appearance IDs: laiya-stage01, roderick-stage01, mirelle-stage01, torren-stage01, cain-stage01, bran-stage01, tasha-stage01. Torren is actor-key-specific; the other six have explicit type bindings. Keep their identity and generic troop assets separate.

`campaign-elin` (艾琳) remains a named c01.archer retinue member in all16 C01 initial rosters. It has neither actor override nor canonical named portrait. No reliable face/age/gender source was found. This is an evidence gap, not proof of a gender mismatch or permission to invent an appearance.

## Verification and caveats

The current repository progress/manifest/binding/levels/hero files and inspected images are SHA256-recorded. Consumer extraction evaluates unchanged C01 level declarations with only the actual funds/command_points constants supplied, reads builtin schema2 JSON and traverses every scenario for spawnUnits. No engine, browser, image generation, Git, tests, build or repository write was performed. References are read-only crops; the contact sheet is an audit layout, not a world-scale comparison.

Shared `approved-v5/progress.json` is obsolete formal0; the repository docs progress is the formal321 source used here. The old canonical-topic snapshot also still says IVRA pending-redraw64×64. Legacy builder points at a missing `assets/manifest-complete.json`; don't rerun it over approved sources. Recompute remaining status before integration because active workers may advance it.
