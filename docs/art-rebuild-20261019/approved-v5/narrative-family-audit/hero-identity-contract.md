# Seven named heroes: identity and source-generation contract

Audit checkpoint and exact reference hashes are in `hero-references.json`.
The current narrative portraits and all seven current @2x four-frame battle
strips were visually inspected. Generic troop approval establishes style and
class equipment, not the named hero's face, age or gender.

## Per-person decisions

| Rule unit / narrative card | Preserve named identity | Actual current battle reference | Required correction / reusable style |
|---|---|---|---|
| c01.laiya / C01-CHAR-LEIA-01 |18-year-old woman, brown side braid, gray-blue mail, gray banner, left wrist guard; no crown for stage01|BANNER-GUARD is a moustached older male soldier in helmet, spear/banner and pack|Definite face/age mismatch. Independent Laiya hero appearance required; borrow gray banner, material palette and readable body mass, never the moustached face. Rules: foot commander, border-blade and gray-oath|
| c01.roderick / C01-CHAR-RODERICK-01 |Older gray-haired/bearded mentor; heavy gray armor; keep recognizable face|HEAVY-KNIGHT is a younger clean-shaven helmeted foot soldier with mace and rectangular shield|Definite age/weapon/mobility mismatch. `units.ts` explicitly gives mounted movementClass, cavalry tags, knight-lance and knight-charge. A future battle appearance must depict mounted/lance identity, not reinterpret rules to fit this generic sheet. Standing portrait may show the mentor dismounted, with lance/horse cue composed for UI if approved|
| c01.cain / C01-CHAR-CAIN-01 |Adult imperial officer, severe face, black/gold helmet/armor and tall shield; not later reformer stage02|LEGION-SHIELD is helmeted dark/red armored spear-and-rectangular-shield soldier|Role and silhouette compatible; exact officer face/rank/palette still require a named appearance. Rules specify legion-spear: don't propagate the old portrait's sword as sole combat weapon. No proven gender/age mismatch|
| c01.mirelle / C01-CHAR-MIREL-01 |Woman with light hood, dark robe, warm lantern, book/name tags; explicitly referred to as 她 in chapter01|GRAVEKEEPER is a gray-haired, bearded man carrying a shovel and lantern|Definite gender/face mismatch. Independent Mirelle hero appearance required; reuse lantern/material rendering only. Rules support lantern-staff and oath-dispel/heal, not a shovel-warrior identity|
| c01.bran / C01-CHAR-BRAN-01 |Young brown-haired hunter, olive/brown hood, bow, quiver and field kit; retain boyish face, no invented age|RANGER has brown fringe, dark hood, bow/quiver and compact field gear|Compatible silhouette and role. New named portrait can align material/style without copying generic face verbatim. If distinct face is not retained by existing sheet, add a named appearance; don't claim identical identity from shared occupation|
| c01.tasha / C01-CHAR-TASHA-01 |Woman, brown tied-up hair, rust-brown coat, ledger, keys, tools; mercenary organizer/engineer|ENGINEER has brown helmet, short visible hair, hammer/shovel/coil; no clear matching bun/ledger identity|Role/tools compatible, face/hair/leadership identifiers not established. Independent named appearance recommended, preserving rule hammer/satchel and portrait ledger/keys; not a forced generic worker face|
| c01.ivra / C01-CHAR-IVRA-01 |Juvenile red dragon, amber eyes, gray belly plates, pale horns, wings and tail; no rider/forced bond|IVRA-GROWTH is red/gray with pale horns but larger, angular and mature-looking|Species/palette compatible; stage mismatch risk. Rules explicitly say 幼龙. Keep the juvenile01 identity rather than silently replacing it with adult stage03. Native growth topic may host a juvenile appearance; age progression needs explicit story state, absent today|

The old22card slots were not swapped; these are presentation alias mismatches
between named rule units and generic battle topics. Source face/age cues above
are visual observations, while Roderick's mounted/lance and Ivra's juvenile
claims are backed by actual `src/units.ts:29–68`. Narrative female pronouns are
in `01-border-fire.md:21,286,366` and `02-grey-banner-exile.md:175,204`.

## Seven-source production group

Each listed card gets a separate full-canvas native RGBA named-character source.
Retain genuine alpha and the whole authored canvas. No background extraction,
chroma reconstruction, opaque character master, labels, UI frames or faux alpha.
Make an independent authored background if the current UI card needs one; record
both source hashes. Portrait/card composition must be reviewed in dialogue,
mobile dialogue, roster and HUD crops, not just native resolution.

Use the legacy named card and its exact original board cell for identity; use
the current battle strip/native frames for stroke/material/body-mass style and
compatible equipment only. `hero-references.json` supplies both references with
full path, hash, dimensions and alpha. The full original board must not be passed
as an unlabeled single-character reference: the exact crop box is supplied.

No source generation, hero binding, combat redefinition, card substitution or
new canonical topic is performed by this audit. The three definite human
identity mismatches and the juvenile dragon stage require explicit source work.

## Minimal future hero-appearance binding

1. Keep `CANDIDATE_01_UNIT_ART` as the sole unit→art binding owner. Represent each
   binding explicitly as `{topicId, appearanceId?}`; seven named units can select
   reviewed appearance IDs while generic troop entries keep the same base topic.
   Do not add a second hero-name heuristic or global engine content lookup.
2. Store authored appearance geometry/source URLs in a separate presentation
   manifest, keyed by appearanceId and anchored to one existing topicId. This is
   a variant, not another canonical topic: no extra manifest assetCount or new
   game unit. Inherit canonical category/footprint and semantic four-frame order;
   validate full logical/HD geometry and per-frame registration independently.
3. `candidate01UnitPicture` resolves the explicit binding once. Missing referenced
   appearance is an error; never fall back to a wrong generic face. A named
   appearance takes precedence over generic team-color art; team ownership still
   uses existing UI indicators and preserveAuthoredPalette. Any authored hero
   team variant must itself reference that hero appearance, not generic topic.
4. All consumers of unitPicture (BoardView GPU/SVG, icon, campaign unnamed-unit
   fallback) share this resolution. Named character card binding remains existing
   `CANDIDATE_01_CHARACTER_ART`; its source evidence links the same hero identity.
   A transparent portrait and battle actions need identity consistency, not an
   artificial promise that one pose file can serve every action.
5. First validate one complete named set, preferably Laiya, before seven-person
   rollout. Generic BANNER-GUARD remains the same old source/geometry for troop
   c01.banner-guard. Do not enable unfinished four-frame appearances in production.

## Regression boundaries for future hero work

- Exactly same canonical topic IDs/counts and unit rule IDs/fields/terrain/triggers;
  compare all60 mission replay event/outcome hashes, no retuning for art.
- Each explicit hero binding resolves its intended appearance; generic troop
  binding still resolves identical URL/frame geometry/palette and team behavior.
- Reject unknown/duplicate appearance, wrong base topic/category, wrong semantic
  frame order, dimension/HD/pivot mismatch, missing runtime URL, incomplete poses.
- Actual complete BoardView + same hero dialogue/roster/HUD, source hashes fixed;
  four-frame stance/feet and attack identity review, owner indicators visible.
- Roderick wide horse canvas must not silently change logical occupancy/footprint,
  and must be reviewed for occlusion at true scene scale.
- Ivra current renderer does not select age by campaign stage. No automatic
  progression or duplicate canonical topic can be inferred from `GROWTH` label.
