# Remaining C01 narrative audit

Read-only audit, 2026-10-20 00:17–00:27 UTC. Repository `docs/art-rebuild-20261019/approved-v5/{progress,scope-ledger}.json` is the authority: **80 C01 narrative topics, seven formal, 73 remaining** at the inspected 324-total checkpoint. Old shared progress/classified snapshots were not used. No source modification, Git, generation, engine/build or tests were performed by this audit.

## Deliverables and verified scope

- `manifest.json`: all 80 exact topic/board/slot/crop/hash mappings; source/runtime alpha; current canonical dimensions; every actual story, staging, roster, HUD and codex consumer; selectors, gaps and four recommended next topics
- `consumers.json`: 35 story screens, 11 choices, 16 levels, roster bindings, 401 initial actors and 14 scenario spawns, extracted read-only from actual declarations
- `pixel-verification.json`: **all 73 remaining outputs reproduce identical decoded pixels from original sources**; exactly seven approved replacement cards differ as expected
- `build-audit.py`, `collect-consumers.cjs`, `finalize-audit.py`: reproducible shared-output-only audit tools; `runtime-literal-references.txt` records the complete direct runtime topic references

All **seven protected cards also reproduce pixel-for-pixel from their current registered independent transparent master + opaque backdrop**. Keep LEIA01, RODERICK01, MIREL01, TORREN01, CAIN01, BRAN01, TASHA01 and their separately registered battle appearances unchanged.

| Subgroup | Total | Remaining |
|---|---:|---:|
| Character cards/stages | 22 | 15 |
| Architecture setting plates | 14 | 14 |
| Chapter scene plates | 16 | 16 |
| Everyday scene plates | 5 | 5 |
| Style/key-art plates | 6 | 6 |
| Key-props, including two chapter-ID exceptions | 17 | 17 |

The 15 remaining cards are ten human stages (LEIA02/03, RODERICK02, CAIN02, BRAN02, MIREL02, TORREN02, ADA01/02, SEVERIN), IVRA01/02/03, and NOBANNER01/02. **Only 12 remaining topics have actual consumers: 11 scene plates plus IVRA01.** The other 61 are catalog-only. None of the 17 key-props currently has a shipped consumer.

## Exact original source map

All 21 original boards exist and are RGB1254×1254: six character, four prop, eleven scene boards. `07-topic-catalog.md` preserves the authored 80-topic order; `build_final_fantasy_v1.py:NARRATIVE_SOURCES` supplies explicit numeric topic assignments. The alphabetical final manifest is **not** the source order. The pixel comparisons independently confirm every remaining mapping.

Slots use row-major 2×2 order. Legacy five-pixel-inset boxes are TL[5,5,622,622], TR[632,5,1249,622], BL[5,632,622,1249], BR[632,632,1249,1249]. These boxes describe the old export, not permission to crop a newly authored reusable master. Prop-board03’s special order is **NAME-TAGS, ADA-MEDICINE, IVRA-BROKEN-BRIDLE, EXPIRING-MANDATE**. Four deliberately unused cells have no canonical identity: character-board06 bottom pair; scene-board06 and11 bottom-right. Do not assign them to Elin or invent topics.

Both original `manifest-complete.json` paths are absent; the recovered map is verified, but the legacy bulk builder cannot safely be rerun. Historical namedHD files and `manifest-hd.json` mentioned in deep-customized09 are also absent. Nine planned-only IDs in that document have no current canonical map; the manifest lists them explicitly.

## Export and transparency contracts

- **Character sources:** genuine independent native RGBA with transparent exterior/open gaps, immutable full authored canvas. The seven current masters are1024×1536. Do not substitute an opaque card, chroma matte, cropped body or battle sprite for this reusable source
- **Character UI cards:**192×224 fully opaque RGBA composition is intentional. Current registered exporter places the entire character144×216 at[24,4] over an independently authored opaque background. Foreground is never bbox-fitted or cropped. All80 narrative topics currently have **no HD entry, frames, pivots or state atlas**
- **Scenes/architecture/style:**256×144 opaque RGBA. These are story/location compositions, not board structures or transparent map pieces. Legacy square→16:9 centered cover removes43.75% of source height; original THREE-BRIDGES visibly clips the top/bottom bridge extents. New scene framing needs an explicit16:9 composition/export review
- **Key-props:**128×128. Sixteen current outputs have alpha, derived historically from magenta-keyed boards; reusable remakes must instead retain native real-alpha sources and whole-canvas placement. **CH03-S11 is the exception: an opaque cemetery scene in a key-prop slot**
- `tools/art/export_overrides.py` currently accepts narrative **character-card-composite-v1 only**. It rejects scene/key-prop topics. Add a bounded registered opaque-scene/transparent-prop exporter before claiming those formal. Do not use the legacy builder to overwrite approved work. Optional512×288 sceneHD would require explicit manifest/export registration; it does not exist today

## Actual consumers and selectors

- Story dialogue uses a fixed speaker→portrait01 table. Desktop92×112, mobile64×88; cover at50%18%. Narrator/unmapped speaker gets no image. No age/rank/chapter selector
- Roster direct-card IDs are laiya,mirelle,bran,tasha,ivra, rendered44×44 cover at50%18%. Join gates use completed-battle counts3/4/7/9 for mirelle/bran/tasha/ivra; rank/HP/disposition do not choose art
- **Torren gap found and reported:** roster originally omitted actor context, showing generic swordsman despite formal TORREN01. During this audit the parent/main writer patched fallback portraitSvg to use the current battle node’s existing campaignUnit→levelUnitKey binding. Source inspection confirms that narrow change; parent subsequently reports real-roster regression, unbound same-type, Elin and unchanged stored-type tests passed. Elin stays generic archer; generic swordsmen stay generic
- HUD inspector passes actorKey and renders84×98 SVG over logical96×112 plate. Named card image is x−8,y0,112×112 with centered slice and rounded clip. Codex uses64×75 SVG by unit type without actor context, so no named Torren entry. Missing card uses that actor/type’s idle frame; missing provider uses game-ui vector/rule-derived art
- Battlefield figures and HUD unit/recruit icons use battle sheets, **not narrative cards**. No narrative architecture or key-prop is a map occupancy/depth/structure consumer
- Story shell uses the explicit scene twice (blurred wash + full-viewport cover). Choices, staging background and results default to chapter1 THREE-BRIDGES, chapter2 REDSTONE, chapter3 SILVERWOOD; unknown chapter falls back to1. Staging’s inset hero separately uses the specific briefing scene
- CH01-S05 is both the game title and experience-lab background. Title cover position is center46%; lab center/mobile60%center. Full CSS/markup paths and dimensions are in the manifest

## First four high-value topics

All four retain their current256×144 opaque consumer contract. Preserve source identity and any already-approved named people; provide true-alpha masters separately wherever reusable character/prop material is needed.

| Priority | Topic | Original slot | Actual value |
|---|---|---|---|
| 1 | C01-CH01-S05 | scene-board02 TL | Both entry screens, three stories, one staging hero |
| 2 | C01-CH01-S01 | scene-board02 TR | Prologue + first brief/aftermath and staging |
| 3 | C01-ARCH-THREE-BRIDGES | scene-board01 BL | Chapter1 choices/staging/results + level12 valley pair/hero |
| 4 | C01-ARCH-REDSTONE | scene-board01 TL | Chapter2 choices/staging/results + six stories/three heroes |

The manifest includes exact hashes, source indices, consumers and proposed export QA. For a clean early-story alternate, CH01-S02 has unambiguous bridge-truce identity (scene-board04 BL) and level02 brief/aftermath/staging use.

## Holds and semantic exceptions

- **Ivra:** all current narrative consumers choose juvenile01; accepted battle quartet has no age owner. The known maturity mismatch is held. Do not overwrite battle art or silently activate02/03
- **Elin:** named archer exists on all16 levels, but no reliable named face/age/gender/canonical portrait source exists
- **CH01-S00:** label describes Roderick tying a wrist guard; actual original source is a loose bracer (prop-board04 BR). **CH03-S11:** label describes cemetery name-wood; actual original source is a whole opaque cemetery (scene-board07 TL). Distinct LEFT-GLOVE and NAME-TAGS prop topics already exist. Do not silently remap or change kind
- **Story reuse:** CH05-S26 late council plate is reused in eight shipped early/finale stories; REDSTONE covers cemetery and unflagged beats; THREE-BRIDGES covers bell valley. Art remaking alone does not resolve these selector-semantic mismatches
- Later human/dragon stages and architecture’s planned multi-year/governance states have no runtime selection owner. Preserve them as separate catalog identities until explicit selection work exists

Input hashes and the parent’s concurrent roster patch are recorded. Completion recheck: overall formal total advanced to326; narrative remains7/80. Parent reports the roster tests passed; this audit did not rerun them. No live-game visual acceptance is claimed.
