# Story scene selection correction

2026-10-20. Bounded presentation correction from the council consumer audit, based on `32e7ffc`. No new art, catalog identities, story prose, campaign graph, game rules, source masters, export geometry or density contracts are introduced.

## Semantic owner and selections

`packages/story-candidate-01/src/story.ts` owns each presentation's specific scene. Staging obtains the same briefing through `candidate01CampaignAdapter`; it has no second level-to-scene map.

| Presentation | Selected specific art | Meaning / limitation |
| --- | --- | --- |
| `c01/brief-04` | `null` | No appropriate Black Camp / Gray Border west-woods plate |
| `c01/aftermath-04` | `null` | Rescued witnesses outside the same missing Black Camp setting |
| `c01/aftermath-08` | `C01-SCENE-PUBLIC-RATIONS` | City granary, public ledgers and noticeboard; a setting plate, not literal contract signing |
| `c01/brief-10` | `null` | No eastern mountain-pass plate for the old-banner confrontation |
| `c01/aftermath-10` | `C01-ARCH-GRAY-CAMP` | Appropriate faction and generic camp; not the exact pass or pledge |
| `c01/brief-13` | `null` | No Holy White Tower archive plate; the Mountain Forge archive is a different place |
| `c01/aftermath-13` | `C01-ARCH-FIELD-HOSPITAL` | Appropriate Holy Light hospital setting; not the exact old building or record-reading event |
| `c01/chapter-three-ending` | `C01-CH05-S26` | Deliberate thematic council reuse only: equal-status representatives and documents fit, but the canonical chapter-five/night/gray-light identity does not become this chapter-three morning event |

`PUBLIC-RATIONS` and the retained council use their existing approved HD exports. At the isolated selector commit, Gray Camp and Field Hospital still used legacy 256×144 art. The subsequent main integration separately rebuilt and registered both scenes at canonical256×144 and native1664×936 (density6.5). Their raw/source/export evidence is under the corresponding narrative-scene records; selector correction itself adds no artwork count. Live-game visual acceptance remains pending.

## Explicit missing-art contract

`StoryPresentation.scene` and `StoryPresentationView.scene` are required `string | null` values. Only authored `null` permits the existing chapter backdrop to stand alone. Unknown non-null topics and empty strings still throw through the strict resolver; there is no catch-and-fallback path.

A missing story illustration leaves the title, location, mission and dialogue intact. Battle staging derives the null value from its briefing, omits the hero image, and uses an explicit one-column `.staging-hero--text-only` layout. Its chapter backdrop, roster, start control, campaign navigation and saved state remain available.

The chapter backgrounds are intentionally generic. They are not reclassified as literal Black Camp, eastern pass, or Holy White Tower illustrations. Source-review snapshots and their historical consumer counts are left unchanged.

## Missing-setting backlog

Four null presentations represent three still-unillustrated settings:

1. Black Camp in the Gray Border west woods, including rescued witnesses outside it (`brief-04`, `aftermath-04`, and derived battle-04 hero)
2. The Gray Border eastern mountain pass during the old-banner pursuit (`brief-10`, and derived battle-10 hero)
3. The Holy White Tower archive in the holy city (`brief-13`, and derived battle-13 hero)

These remain content/art decisions. No placeholder topic IDs are invented. The later gray-banner camp, homecoming village, Twin Hills battlefield, and Mountain Forge archive must not silently stand in for these places.

## Regression coverage and checks

- `candidate-01-story-scenes.test.ts`: 14 public-API tests, including the exact eight decisions, all 35 graph presentations, complete council and null consumer sets, all 16 adapter-derived staging heroes, and strict invalid-topic errors
- `campaign-game.test.ts`: 22 DOM tests total (18 added). Covers prose and chapter backgrounds for all four null presentations, no hero image and computed one-column CSS for all three staging screens, normal staging, three remaps, the HD finale, unknown-topic errors, story→choice/staging, battle start/exit/reentry, saved-state restoration, and aftermath→next briefing
- AST comparison against `32e7ffc`: all non-scene arguments of all 35 story presentations are byte-identical; prose, missions, speakers and dialogue are unchanged
- Existing campaign graph and deterministic progression tests retained
- Existing narrative source/export dimensions, physical density, scene kind and chapter fallback (including chapter 99) checks retained without weakening

Run from the repository root:

```sh
npm test -- packages/story-candidate-01/src/presentation/candidate-01-story-scenes.test.ts packages/story-candidate-01/src/presentation/candidate-01-narrative-scenes.test.ts packages/story-candidate-01/src/campaign.test.ts packages/game-ui/src/ui/__tests__/campaign-game.test.ts --no-cache --maxWorkers=1 --minWorkers=1
npm run typecheck
npm run build --workspace @empire/game-app
```

Final focused run: 4 files / 52 tests passed. Typecheck and game-app build passed. The build retains the existing large-chunk warning. No full-suite or gameplay replay-sweep pass is claimed: changes are presentation selection/rendering only, and the real campaign navigation/progression tests run against the composed engine.

## Mutation proof

All mutations were isolated to this clean task branch, run individually, and restored before final verification:

1. Restoring `brief-04` to the council killed four API guards: exact selector, council allowlist, missing-scene set, and derived staging binding (4 failed / 10 passed)
2. Unconditionally emitting the staging image produced `<img src="null">`; all three no-image staging tests failed (3 failed / 19 intentionally unselected)
3. Making the resolver swallow unknown topics and return a generic scene killed both invalid-topic API cases and both story/staging DOM strictness tests (4 failed / 32 intentionally unselected)

The negative runs demonstrate behavior, not source-format regex matches. Their targeted unselected tests are not reported as successful executions.

## Visual acceptance limit

The local DOM and computed-CSS contracts passed. Review HTML for the eleven affected screens plus normal battle-01 was prepared at desktop and narrow-mobile sizes, but real browser screenshots could not be captured in this execution environment: Chromium's local IPC is sandbox-blocked, the escalation failed before browser startup, and the cloud browser denied `file://` navigation. No workaround was used after the protocol denial.

Desktop/mobile visual acceptance therefore remains open. Verify all four missing story screens, all three no-image staging screens, three remapped aftermaths and retained finale for title/control clipping, broken images and backdrop composition. In particular, assess the two legacy 256×144 camp/hospital plates separately from semantic correctness. Generated review HTML and DOM checks are not live-game screenshot evidence.
