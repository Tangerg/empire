# Native localized narrative presentation

## Ownership and integration

`StoryScreen` owns only a local beat cursor and the lifetime of its controls. It never reads or mutates a campaign runtime, performs a save, or synthesizes a battle outcome. The application supplies `NarrativeContext` (the explicit `GameText` service, authored metadata, completed battle count, active status) and callbacks:

- `ShowStory(story, speakerNameKeys, context, isEnding, onContinue, onFinish, onExit)`.
- `ShowChoice(choice, availableOptionIds, context, onChoose, onExit)`.

Normal primary actions advance one local beat. Only the last normal beat invokes `onContinue`. Ending primary actions invoke `onFinish` immediately. That callback, not the screen, decides whether to advance/save/rebind or exit. Menu navigation is always `onExit`. Choice IDs are passed unchanged to the caller, in source presentation order, restricted to the canonical available option IDs.

`NarrativeProjection` is a detached read model. `CampaignShell` renders its supplied title, chapter heading, segmented progress, actual backdrop, menu button and scroll body. Its display methods are reusable by staging/results without gaining campaign authority. The native `grayflag-story.json` schema 2 owns story/choice structure, speaker IDs and manually authored semantic beat IDs. It contains message references, not prose. `grayflag-staging.json` schema 2 owns shared `titleKey`, `completionLabelKey`, chapter title references and artwork. Level entries no longer repeat chapter titles. `zh_CN.po` alone owns narrative wording and the shared shell labels. No story/choice/scene identifiers are duplicated in production C#.

`StoryDocument.Load(text)` validates all content keys and zero-argument formats, unique story/choice/beat/option identities and mapped speakers before navigation. Literal old fields are rejected, not adapted. The projection resolves messages into detached view models; controls render those values. The application owns the one injected service. There is no localization dependency in Core, no translated prose/key added to campaign state, and no migration for schema-1 presentation documents.

`export-grayflag.ts` no longer regenerates these documents or scrapes TypeScript menu wording. It only refreshes their explicitly referenced, hash-verified artwork. Native authoring now advances independently of original browser wording. Menu/staging labels and semantic-refusal presentation also use the catalog. Built-in level/unit names and remaining battle/outcome/progress text are not yet migrated; these slices do not claim complete game internationalization.

## Campaign-relative dates

The original `story.ts` mixes the prologue's world date, `断冠纪第 318 年`, with the campaign's year-zero timeline. The source campaign README pairs years 0–3 with Laiya's ages 18–21; these are relative story years, not a reset of the world calendar. Native PO wording now labels all 34 dated stories `战役第 … 年`, and the prologue retains the world-date anchor in parentheses. Numbers and seasons are unchanged; the failed ending remains `未定`. No calendar conversion, runtime clock or save field is introduced. The original chapter-two outline spans deep winter into the following autumn, so no absolute-year boundary is inferred from its relative labels.

`NarrativeChecks.NativeStoryDates` explicitly names all 35 expected dates and verifies both projections and actual widgets. Date wording is the intentional native departure from the unchanged historical oracle; all other valid projected fields retain their frozen source comparisons.

## The ending quirk is intentionally preserved

The original `packages/game-ui/src/ui/campaign-game.ts` has two distinct contracts:

1. `nextBeat()` returns immediately for an ending. The four-beat chapter-three ending enters at local beat zero, and normal navigation never exposes its remaining three beats.
2. `finishCampaign()` on an active ending calls the canonical `runtime.advance()`, persists and renders the same ending. It does not exit or increment the local beat. A later finish when status is no longer active exits without another save or transition. The declared outcome may be `completed` **or** `failed`.

`renderStory()` shows the adapter's completion label for any active ending, including the failed-ending fixture; once not active, it says `返回主菜单`. Native presentation preserves that label behavior rather than inventing a victory or silently repairing the source story flow. The two ending documents have five beats in the data. Testing all five as controlled render fixtures is not a claim that a normal playthrough can page through them. Changing this behavior requires an explicit source/design decision, not a renderer fix.

## Text, images and controls

The screen mounts exactly one dialogue beat, with the localized speaker label (unknown speaker IDs are authoring errors), portrait when mapped, current and prior active beat dots, source next/continue/finish label, kicker/date/location/summary, and the original three mission labels. All 11 choice documents retain their original option number, label, detail, consequence and save-effect note.

Null story scenes intentionally select the chapter backdrop. A missing declared scene does **not** select an unrelated substitute. A narrator/unmapped portrait stays portrait-free; missing mapped artwork produces a localized, player-safe artwork-failure notice. The precise missing source/resource path remains available through `RenderingDiagnostics`, not in the player label. The verification script checks actual source/export PNG bytes against every referenced SHA-256 before loading them in Godot.

Stale local-page buttons, old rebound controls, double submissions and unmounted controls cannot advance or choose. One successful canonical callback disables competing navigation until the caller rebinds. This is intentionally stronger than an unmounted-but-undisposed original DOM root, whose click listener can still run; source `dispose()` is what disables that listener.

Local beat changes retain the same scroll container and its offset. Resizing retains the beat and scroll position subject to ordinary content bounds. Native layout follows the source 760 px breakpoint: progress hides, mission/choice cards stack, actions become one column and portraits shrink. A new caller binding starts at the top. Headless native layout tests cover these contracts; they are not a browser/native pixel comparison or proof of physical pointer/touch usability.

## Verification and limits

Run:

```sh
bash tools/godot/narrative-checks/run.sh --verify-guard
```

The test creates a fresh `/tmp/empire-narrative.*` project and isolates `XDG_DATA_HOME`; it does not touch user saves. It copies the actual production narrative classes and the exact production JSON loader, and imports only required source-exported narrative PNGs. The default gate uses frozen rendered expectations and native schema-2 fixtures, with no TypeScript runtime.

Frozen observations originally produced by `StoryCampaignController`, its adapter/runtime and save helpers cover:

- 35 documents / 91 ordinary-mode beat render fixtures, plus all five ending beats under active/completed/failed status: 106 source story projections total.
- All 11 source-rendered choice menus.
- 48 original transition fixtures, including the deliberately unreachable nonzero ending cursors that characterize the source quirk.
- 11 actual DOM lifetime fixtures, with three unmounted/remounted cases explicitly marked as native safety differences.
- Four supplemental native inputs: real runtime retreat history gives nonzero progress, a valid zero-battle definition, an unknown speaker rejection, and a known speaker with no portrait. The unknown-speaker fallback is an intentional breaking correction; its old display expectation was removed, not silently normalized.

Native checks compare every valid projected field against frozen DOM outputs except the explicitly asserted native dates above, and explicitly assert the unknown-speaker rejection. Native widgets exercise every ordinary-mode beat fixture and each reachable beat-zero ending/status fixture. Canonical normal and ending callback sequences use the existing C# `CampaignRuntime` and `CampaignSave`; no battles or victories are fabricated. A wrong next-beat label is deliberately introduced only in the temporary project and must fail the source differential check. Source-side ending paging mutation was also rejected by the oracle.

The completed narrative-only checkpoint passed its native widget/projection checks and wrong-navigation-key negative control. That checkpoint covered 452 catalog messages and 415 content references, before the subsequent menu/staging/refusal additions; it is not merged-game acceptance. Parser and authoring checks reject malformed, duplicate, missing, empty, fuzzy and obsolete data. Two independent in-memory catalogs change actual native widget wording without changing authoring identities or state. These test catalogs are not additional shipped languages.

Not claimed: complete built-in-content/battle/outcome i18n; a victorious campaign playthrough; browser CSS effects or pixel-identical typography; physical native GUI acceptance; a newly exported standalone game. The separate foundation packaging check established native PO loading/formatting in a real exported executable; product release validation remains the integration owner's gate.
