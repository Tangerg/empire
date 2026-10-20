# Council selector audit

Read-only engineering audit, 2026-10-20 UTC. Repository: `/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019`. Binding commit: `895a415`; HEAD advanced from `d4fca5f` to `9c02176` during this audit as the coordinator integrated unrelated work. No repository files were edited; no worktree, art generation, or Git writes were performed. All audit outputs are outside the repository.

## Result

The new council export is correctly bound to its existing canonical topic. The defect is the story's use of that topic for unrelated locations. Eight story presentations select it, and three staging heroes inherit it from their briefings. Seven story selections should stop using it. Keep the chapter-three ending as a deliberate thematic council illustration, with its time-of-day limitation recorded.

There is no honest seven-line selector-only fix using the present catalog. Three existing alternatives fit the setting, but four presentations have no matching scene. The smallest complete no-new-art correction is three remaps plus an explicit missing-specific-art state for those four, carried through the story/staging presentation contract. Preserve the already intentional chapter-level background fallback. Do not introduce a silent fallback in the asset resolver or map unrelated scenes onto newly invented topic IDs.

## Canonical identity and binding ownership

- `docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md:63` identifies `C01-CH05-S26` as 灰誓议会与灰夜 (Gray Oath Council and Gray Night)
- `docs/story-candidates/candidate-01/deep-customized/05-two-peaces.md:115–144` describes the chapter-five council, independent delegates, the council overruling Leia, local defense, and the seven-tower gray-light event
- `docs/story-candidates/candidate-01/deep-customized/09-static-assets.md:76` requests representatives, defense and seven-tower gray light together
- Current source-review evidence: `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/narrative-scenes/ch05-s26/source-review.md`; source snapshot: `audited-source.json` in the same directory
- Formal binding evidence: `docs/art-rebuild-20261019/approved-v5/narrative-scenes/ch05-s26/README.md` and `export-report.json`
- Canonical manifest remains 256×144, kind `scene`, with no sprite frames or anchor. The physical HD entry is 1664×936, density 6.5; this is an explicit source-native crop of 1672×941, not a gameplay scene

One fact has one owner:

1. `packages/story-candidate-01/src/story.ts:44–56` declares semantic scene aliases; each `p(...)` call owns which topic depicts its presentation
2. `packages/story-candidate-01/src/presentation/candidate-01-story.ts:15–16` only resolves a topic to its URL; `candidate-01-assets.ts:194–207` selects the registered physical export
3. `packages/game-ui/src/ui/campaign-game.ts:317–319` renders a story's selected scene
4. `packages/story-candidate-01/src/presentation/index.ts:161` derives each level's briefing ID; `campaign-game.ts:360–374` obtains that briefing and uses the SAME scene for the staging hero
5. Staging's full-screen backdrop is separately the deliberate chapter backdrop (`campaign-game.ts:396`). Choices/results also retain that policy. `candidate-01-story.ts:18–32` owns the chapter-topic mapping

Thus do not add a staging-specific council blacklist, a second level→art map, a URL rewrite, or a renderer condition on `c01-04`/`c01-10`/`c01-13`.

## Per-consumer decision

| Current presentation | Source evidence | Proposed selection | Classification / limitation |
|---|---|---|---|
| `c01/brief-04` (line 117) | Gray Border west woods; prisoners; three tents; stealth and escape | Explicit missing specific art | No unmarked hostile forest camp topic. Gray Camp belongs to the later gray-banner faction; assigning it here conflates identities |
| `c01/aftermath-04` (123) | Outside Black Camp, rescued witnesses, first names recorded | Explicit missing specific art | Same location gap as briefing; no council is taking place |
| `c01/aftermath-08` (171) | Luojia granary; mercenary contract, debts, posted pension list | `C01-SCENE-PUBLIC-RATIONS` | Strongest replacement. Approved source review explicitly places it in the city granary/public-distribution context with ledgers and public noticeboard. The image is a setting plate, not a literal picture of Tasha signing |
| `c01/brief-10` (190) | Gray Border eastern mountain pass; Roderick's pursuing force and escape | Explicit missing specific art | No old-banner pass scene. Old Flag Homecoming is a village/homecoming plate; Twin Hills shows another named battlefield with river/bridge; neither is an exact or neutral pass plate |
| `c01/aftermath-10` (196) | Camp outside the pass; gray-banner members hear exit terms again | `C01-ARCH-GRAY-CAMP` | Justified generic camp reuse. It illustrates the right faction and kind of location; it does not depict the precise pass or pledge |
| `c01/brief-13` (226) | Holy White Tower archive; three record groups; alarm and escape | Explicit missing specific art | No Holy Archives/White Tower narrative plate. Forge Archive Opening depicts the distinct Mountain Forge archive furnace, not a church archive |
| `c01/aftermath-13` (232) | Old hospital outside the holy city; examination of rescued records | `C01-ARCH-FIELD-HOSPITAL` | Justified generic Holy Light hospital setting, not a literal depiction of the old building or record-reading scene |
| `c01/chapter-three-ending` (274) | Luojia morning council, seats, common rules, shared table, freedom to leave | Keep `C01-CH05-S26` | Deliberate thematic fallback. The equal-status representatives and documents fit. The chapter-five/night/gray-light identity does NOT become the canonical identity of this chapter-three morning event |

The four missing entries correspond to three absent settings, not four new art requests. They do not warrant inventing `C01-CH01-S04`, `C01-CH02-S10` or `C01-CH03-S13`: none is in the current 404-topic catalog or runtime manifest. A later new asset identity requires the normal content/art decision.

### Quality limitations of existing replacements

`PUBLIC-RATIONS` has a formally approved 1664×936 HD source. `ARCH-GRAY-CAMP` and `ARCH-FIELD-HOSPITAL` are existing legacy 256×144-only scenes with `final-cartoon-fantasy-v1` quality, not newly approved-v5 art. Remapping them improves semantics but does not complete their art rebuild or live visual acceptance. Metadata and exact displayed PNG hashes are in `verified-candidate-records.json`.

## Smallest complete correction proposal

1. In `story.ts`, remap the three supported cases above. Add only the two needed existing-topic aliases for camp/hospital. Keep the council alias canonical and keep the ending's reuse explicit
2. Permit a required `scene: string | null` in `StoryPresentation` and `StoryPresentationView`; type the `p` scene argument from the property. Set the four missing selections to explicit `null` with concise reasons. Null means no specific illustration is available, not an unknown ID, not a fake asset, and not authorization to guess another location
3. For `renderStory`, resolve a string scene normally; for null, call `shell` without a specific scene so the authored chapter backdrop remains. This preserves the existing generic background policy, but is not a claim that the fallback bridge/tower/forest literally depicts the scene
4. For `renderBattleStaging`, resolve the same briefing selection; null means omit the specific hero `<img>`. Keep the normal chapter backdrop, title, subtitle, roster and controls. Add a text-only class that makes the hero one column; current desktop CSS has a mandatory two-column grid (`packages/game-ui/src/styles/campaign.css:475–485`), so simply removing the image would leave bad layout
5. Leave `candidate01StoryArt` strict. Unknown non-null topics must still throw. Do not catch and replace resolver failures. Do not change source masters, source-overrides, export dimensions, manifests, campaign graph, story prose, chapter background mapping or battle logic
6. Add selector and UI tests below; add a current audit note in the normal documentation area. Preserve immutable source-review snapshots as historical evidence rather than rewriting them to imply their original consumer counts were different

If explicit missing-art presentation is outside the implementation scope, the bounded partial patch is only the three supported remaps. It leaves four story and three staging misbindings open and must not be called a complete selector correction. Alternatively, wait for approved art for the three missing settings. Replacing them with convenient but wrong existing images is not a complete correction.

## Catalog comparison and rejected shortcuts

All 41 `kind: scene` records were visually checked at their canonical 256×144 representation. `catalog-scenes-1.png` and `catalog-scenes-2.png` are labeled audit contact sheets; `existing-scene-comparison.png` enlarges likely alternatives. These are analysis composites, not edited assets or live UI screenshots.

Do not trust labels alone. Current `STYLE-BORDER-AUTUMN` pixels show an urban military parade, and `STYLE-CAPITAL-ORDER` pixels show a wooded tent camp. Their labels do not establish that their current image is suitable. The latter's visual resemblance to a forest camp does not make its canonical identity appropriate for Black Camp. This audit does not establish the cause of those pre-existing label/pixel discrepancies and makes no repair to them.

Other rejected substitutions: `SCENE-ARCHIVE-FORGE-OPEN` would conflate Holy Light and Mountain Forge; `SCENE-OLD-FLAG-HOMECOMING` would turn a mountain-pass confrontation into a homecoming; reusing CHAPTER_ART as a specifically selected location would obscure its intentionally generic status.

## Verification actually performed

- Read AGENTS.md and PROJECT_RULES.md; traced all semantic/presentation/physical owners
- AST-based read-only inventory: 35 story presentations, 8 council users, derived staging levels `c01-04`, `c01-10`, `c01-13`
- Proposed council-only allowlist regression was executed against current source and correctly FAILS with 7 unexpected consumers. `audit-current-selectors.cjs` is outside the repository and reads its argument root; `current-selector-audit.json` contains full output. It does not mutate source
- Existing baseline tests: 3 files, 20 tests PASS (13.63s). These cover campaign graph, narrative density/resolver contracts, and basic campaign UI. They do not assert the eight scene semantics, so their green result does not validate the selector reuse
- Command: `npm test -- packages/story-candidate-01/src/campaign.test.ts packages/story-candidate-01/src/presentation/candidate-01-narrative-scenes.test.ts packages/game-ui/src/ui/__tests__/campaign-game.test.ts --no-cache --maxWorkers=1 --minWorkers=1`
- No proposed production change was applied. Therefore no post-fix test pass, typecheck, build, browser verification, or live-game visual acceptance is claimed

See `proposed-regression-tests.md` for the implementation acceptance matrix.
