# Targeted regression plan

## Semantic selection tests (public story API, not source-text regex)

Add to a focused story scene-selector suite (or the existing narrative-scene suite):

1. Explicit table assertions via `candidate01Story(id).scene`:
   - brief-04 → null
   - aftermath-04 → null
   - aftermath-08 → C01-SCENE-PUBLIC-RATIONS
   - brief-10 → null
   - aftermath-10 → C01-ARCH-GRAY-CAMP
   - brief-13 → null
   - aftermath-13 → C01-ARCH-FIELD-HOSPITAL
   - chapter-three-ending → C01-CH05-S26
2. Enumerate story and ending presentations from the shipped campaign graph; resolve through `candidate01Story`. Assert the complete council consumer set is only chapter-three-ending. Do not merely assert the eight selected examples if new consumers could be added accidentally
3. Assert the null scene set is exactly brief-04, aftermath-04, brief-10, brief-13. The explicit list documents missing art and prevents accidental content omissions from silently turning into acceptable fallback
4. For all other graph presentations, verify `candidate01Asset(scene)` has category narrative-static and kind scene and `candidate01StoryArt(scene)` is the catalog URL
5. Through the actual `candidate01CampaignAdapter`, iterate all 16 levels and resolve `adapter.story(adapter.briefingId(level)).scene`. Assert c01-04, c01-10 and c01-13 are null and no staging hero points to council. This protects the real derived binding path, without adding a duplicate production level→scene map
6. Preserve existing unknown-topic throws and generic chapter fallback tests, including chapter 99 using chapter 1. Do not relax source density or export pixel checks

## DOM behavior tests

Extend `packages/game-ui/src/ui/__tests__/campaign-game.test.ts`, reusing its composed content/engine/options and jsdom environment. Construct states from `new CampaignRuntime(adapter.definition).snapshot()` and select the desired valid graph node, then instantiate `StoryCampaignController` with that state.

- At each null story presentation: title/location/mission or dialogue remains present; backdrop images resolve to `adapter.chapterArt(chapter)`; no src contains council; `adapter.storyArt` is not called with null/empty/undefined
- At battle-04, battle-10 and battle-13: `.staging-hero img` is absent; the text-only hero is present, title and start control remain; the full-screen backdrop still equals chapterArt, not council
- At a normal staging node such as battle-01: the hero still uses the briefing's exact scene and the backdrop still uses chapterArt
- At aftermath-08, aftermath-10, aftermath-13: story backdrop src is the expected remapped topic's actual resolver URL
- At chapter-three-ending: story backdrop still uses the physical HD council URL
- Unknown non-null topic still throws; null is the ONLY explicit missing-art state. An invalid topic must not be swallowed by the generic background behavior
- Navigate a missing-art story to its next choice/staging screen and start/leave/reenter the corresponding battle; absence of a hero image must not change campaign progression or persistence

## Visual check

Review desktop and narrow-mobile widths for the four missing story screens, three staging screens, three remapped aftermath screens and retained finale. Confirm no empty two-column staging grid, broken image icon, literal 'null' URL, clipped title, or accidentally removed chapter backdrop. Assess the legacy 256×144 camp/hospital quality separately from semantic correctness; do not mark them rebuilt.

## Verification commands and mutation proof

Run the focused selector suite plus the three baseline files, `npm run typecheck`, and the applicable app build after implementation. The current audit's baseline 20 tests are green, so new regression coverage is necessary.

Deliberately restore one wrong council binding (for example brief-04) in an isolated test mutation and confirm the allowlist/table and derived-staging tests fail, then undo it. Also deliberately let null produce `<img src="null">` or let an unknown topic fall back and confirm the relevant UI/strictness test fails. Do not count a source regex that merely matches the current formatting as behavioral regression coverage.

No tests in this document have been added to the repository or reported as post-fix passes.
