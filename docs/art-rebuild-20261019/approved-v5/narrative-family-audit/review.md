# Gray Flag narrative family: read-only source / UI contract audit

## Result and first bounded group

The 80 topics are **22 character cards, 41 scenes and 17 key-props**, not 80
transparent illustrations. All 21 original source boards were inspected. No
new images, exports, production code or asset files were generated/changed.

Start with four existing, high-frequency **character-card identities**. Their current UI outputs are opaque; new character masters must be genuine transparent RGBA, with any UI background supplied independently:

| Topic / identity | Static dialogue beats | Original source cell | Current output |
|---|---:|---|---|
| C01-CHAR-LEIA-01 / 莱娅 18岁 | 33 | character-board-01, TL, catalog slot0 | char-leia-01.png |
| C01-CHAR-MIREL-01 / 米蕾尔 | 12 | character-board-01, BR, slot3 | char-mirel-01.png |
| C01-CHAR-BRAN-01 / 布兰 | 10 | character-board-02, TL, slot4 | char-bran-01.png |
| C01-CHAR-RODERICK-01 / 罗德里克 | 9 | character-board-01, TR, slot1 | char-roderick-01.png |

Counts are static `speaker` references in `src/story.ts`, not player telemetry.
Tasha also has9; Roderick is chosen for the opening/tutorial. Cain has8, Ivra3.
All four cards already have dialogue, campaign roster and battle HUD consumers.
No character-stage or topic ID changes are needed to replace this group.

The current runtime files are in
`packages/story-candidate-01/assets/final-fantasy-v1/runtime/narrative-static/`.
The master boards are in the same pack under `masters/narrative/characters/`.
Full file and pixel SHA-256, exact crop boxes and source hashes are in
`inventory.json` for all80 topics. This audit does not certify that every scene's
storytelling detail is semantically complete; confirmed exceptions are below.

## Source order and reproduction evidence

All21 boards are RGB1254×1254, with four617×617 crops after a5px inset:
TL `[5,5,622,622]`, TR `[632,5,1249,622]`, BL `[5,632,622,1249]`,
BR `[632,632,1249,1249]`. Cell order is TL,TR,BL,BR.

`.../tools/build_final_fantasy_v1.py:68–91` names the explicit board→topic-slot
mapping. Slots are **the original topic-catalog order**, recoverable from
`docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md:9–92`.
Do not zip these slots with the final manifest, which is alphabetically sorted.
Examples that defeat an implicit order assumption:
- character-board-02 → `[4,5,28,29]`
- prop-board-03 → `[76,73,74,75]`
- prop-board-04 → `[77,78,79,44]`
- scene-board-04 → `[26,27,45,46]`
- unused: character-board-06 BL/BR; scene-board-06 BR; scene-board-11 BR

The legacy builder reads `assets/manifest-complete.json` (`TOPIC_SOURCE`, line20;
read at95–97). That file is absent even in the complete main checkout. It is not
safe to rerun the monolithic builder or reconstruct its order from sorted IDs.

Read-only recomputation using the current helper functions and cached prop matte:
- 64 opaque outputs reproduce every RGBA byte exactly (22 cards,41 scenes, and
  the incorrectly classified opaque cemetery image)
- The16 transparent prop outputs reproduce every RGB byte, but alpha differs
  slightly: mean absolute alpha difference0.0198–0.1917 on an8-bit channel.
  Exact current outputs and matte cache hashes are recorded. The cause/version
  of that historic alpha processing was not established; no exact byte-rebuild
  claim is made for those16 outputs
- All source board, cached matte and80 runtime file hashes were checked again
  after the audit and stayed unchanged

Legacy details: only prop boards enter `chroma` (line378); that method removes
magenta via a cached matte, then an explicit saturated-magenta cleanup
(lines134–155). `grid_cell` flood-removes white edge gutters for RGBA cells
(lines100–130). `fit_transparent` crops at alpha28 and refits with4px padding
(lines158–176,393). These are historic derivations, not untouched native alpha.
Do not carry that chroma pipeline into a new direct-RGBA source contract.

## Transparency / canvas contracts

| Use | Current reality | Next-source contract |
|---|---|---|
| Character **card** | 22×192×224 RGBA, alpha255 everywhere, complete parchment/background | Compose an independent true-RGBA character source over an independently authored background; retain192×224 output and named identity |
| Character **standing/cutout master** | No independent narrative standing-cutout consumer in current story/HUD wiring | Required genuine RGBA reusable source with transparent exterior, regardless of current UI output; no chroma extraction from legacy card |
| Story illustration / archive / menu plate | 41×256×144 RGBA, alpha255 everywhere | Complete opaque16:9 environment; no transparent border; key evidence outside bottom32 logical pixels |
| Isolated narrative prop | 16×128×128 with transparent exterior; derived from magenta boards | Native RGBA with transparent exterior and intact subject; separate contract from opaque scenes/cards |
| Cemetery scene classified as key-prop | 1×128×128, fully opaque | Resolve its classification explicitly; do not blindly force transparency |

The current UI opacity is an observed legacy behavior, not authority to make new character masters opaque. The latest approved direction requires separate genuine transparent character masters. Complete scene illustrations may remain opaque. An intentionally full-background character illustration must still have a matching reusable transparent character source.

The opaque legacy resizer is `ImageOps.fit`, center-crop (`builder:179–180`).
A square617 source becomes192×224 by removing14.3% of its width; a256×144 scene
removes43.75% of the source height before display. Native6:7 cards and16:9 scenes
avoid this implicit source loss. The game then performs additional view-specific
cover/slice crops; source identity alone does not prove UI clearance.

## Real consumers and container constraints

Paths below are repo-relative:

1. `packages/story-candidate-01/src/presentation/candidate-01-story.ts:3–16`
   maps7 named portraits, exposes menu `C01-CH01-S05`, and resolves story topics.
   Lines25–32 map the three chapter plates (THREE-BRIDGES, REDSTONE, SILVERWOOD).
2. `packages/story-candidate-01/src/story.ts:45–57` declares11 scene IDs. Static
   references: council CH05-S26=8; REDSTONE=6; hills CH01-S01=3; burned CH01-S05=3;
   PUBLIC-RATIONS=3; other6 scenes=2 each. Chapter and menu reuse is additional.
3. `packages/game-ui/src/ui/campaign-game.ts:317–343` selects the dialogue card
   by speaker. `styles/campaign.css:310–323`:92×112, object-fit cover,
   object-position50%18%; at viewport≤760px it is64×88 (lines741–748).
4. Campaign roster: `campaign-game.ts:365–386` and `campaign.css:557–574` use
   the same authored card in44×44, cover,50%18%. Unnamed units instead use their
   actual combat frame in an SVG plate.
5. Battle HUD: `packages/story-candidate-01/src/presentation/index.ts:58–70`
   uses the seven `candidate-01-bindings.ts:89–97` cards. It places an image
   at(-8,0),112×112, `xMidYMid slice`, clipped to96×112. `game-ui/src/ui/hud.ts:679`
   requests84px wide, yielding84×98. This is different from the CSS dialogue crop.
6. `campaign-game.ts:302–314` renders each story/chapter image twice, as a wash
   and the main backdrop; `campaign.css:176–211` fills viewport below66px topbar
   with cover. Wash is opacity.72/blur18px; main artwork gets CSS saturation/
   contrast. These are UI effects, not edits to the source.
7. Battle staging image: `campaign-game.ts:373` and `campaign.css:475–495` use
   a responsive grid hero, minimum245px high, object-fit cover; mobile caps image
   at220px high (line752).
8. Main menu: `apps/game/src/main.ts:224`, `game-ui/src/styles/app.css:774–783`
   uses CH01-S05 full viewport, cover, center46%. Experience-lab uses the same
   plate (`apps/experience-lab/src/main.ts:42`, `experience.css:19–20,53`).

These named shipped adapters directly consume18/80 unique topics:7 cards+11
scenes. The other62 are15 cards,30 scenes,17 props. They remain catalog inventory,
not demonstrated story/HUD integrations. None of the80 has a tactical-HD entry
or a registered source-overrides entry at this audit checkpoint.

## Confirmed gaps and identity/classification mismatches

1. **C01-CH01-S00, “罗德里克系护腕”**: catalog slot44 maps to prop-board-04 BR.
   The source is a lone bracer with a cord; it does not show Roderick tying a
   bracer on Laiya. It is exported as a128×128 transparent key-prop. This is an
   actual subject/scene mismatch, not a wrong offset. The shipped prologue uses
   CH01-S01 instead, so this asset is presently unconsumed rather than visible
   as the opening scene. Future correction requires a real two-person scene and
   an explicit scene contract, not relabeling the prop as completed illustration.
2. **C01-CH03-S11, “墓园姓名木片”**: slot54 maps to scene-board-07 TL, a complete
   cemetery gathering with people and name tokens. Its RGB source becomes an
   opaque128×128 key-prop because slot54 is hardcoded in the prop set
   (`builder:392–395`). This is a type/size mismatch between scene source and prop
   contract. The separate NAME-TAGS prop(slot76, prop-board-03 TL) already owns
   the isolated tokens. Do not merge those two identities.
3. No swapped identity was found among the22 character-card board slots. In
   particular, Tasha's tool-bearing art matches her current engineer-hammer unit;
   it should not be called a proven mismatch from the “mercenary leader” label.
4. `tools/art/export_overrides.py:107–110` supports only battle-prop/equipment/
   skill/status/hud, with transparent-source validation. It explicitly rejects
   narrative-static. The environment and animated-unit exporters are also the
   wrong contracts. New opaque narrative sources currently have no bounded
   approved override exporter.
5. No narrative HD binding exists. The192×224 card already provides about2×
   density for92×112 dialogue, but256×144 plates are enlarged to full-screen
   backdrops. Source detail alone will not survive that small runtime contract.
   A later scene-HD change needs actual URL/manifest/UI validation, not a count
   of larger saved masters.

## Executable four-card proposal (not implemented by this audit)

1. Freeze the four topic IDs above, their old runtime hashes and the other76
   hashes. Author one named-character RGBA source per ID, with native source SHA
   and identity notes. No new four-character board or inferred order.
2. The character source must have a genuinely transparent exterior and intact
   subject. A separately authored background has its own path/hash. Do not
   remove an opaque background with chroma/segmentation or paint replacement
   scenery in the exporter. Native sources remain unchanged and reusable.
3. The bounded card exporter uses explicit full-canvas character placement over
   the independent background. It emits the existing192×224 RGBA card without
   changing manifest identity, UI crop, URL, rules or HD. Background center-cover
   cropping is recorded; character scaling is uniform, with no subject crop.
4. Validate exact topic/category/kind, dimensions, source ownership and hashes,
   RGBA character exterior, opaque background, and bounded placement before any
   writes. Reject scene/key-prop, unknown ID, opaque character, invalid placement,
   duplicate target, and later invalid source. Verify untouched unregistered
   outputs, byte-stable manifests/native sources and deterministic reexport.
5. Inspect actual dialogue92×112/mobile64×88, roster44² and HUD96×112 clipping.
   Source review alone does not establish face/identifier clearance at play size.
6. Named identity comes from narrative/rules and approved named-character art.
   Current generic battle faces are not identity authority. See
   `hero-identity-contract.md` and `hero-references.json` for the seven mappings
   and actual mismatches. Separate hero presentation variants must retain generic
   troop sources and existing canonical topic counts.
7. Scenes and the two known misclassified subjects remain separate work. Complete
   scene plates may be opaque. No silent128²prop→scene migration is authorized.

Files supplied: `inventory.json` (80outputs/21boards/source-crop-alpha-consumer
hashes), `audit.py`, `audit.log`, this review, and the seven-hero identity contract.
This package audits existing assets; it does not claim new card source completion.
