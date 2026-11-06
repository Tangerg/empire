# Native localized narrative presentation

## Ownership and integration

`StoryScreen` owns only a local beat cursor and the lifetime of its controls. It never reads or mutates a campaign runtime, performs a save, or synthesizes a battle outcome. The application supplies `NarrativeContext` (the explicit `GameText` service, authored metadata, completed battle count, active status) and callbacks:

- `ShowStory(story, identityNameKeys, context, isEnding, onContinue, onFinish, onExit, initialBeatIndex = 0)`.
- `ShowChoice(choice, availableOptionIds, context, onChoose, onExit)`.

Every pre-final primary action advances one local beat without calling the application. Only the last normal beat invokes `onContinue`; only the last ending beat invokes `onFinish`. That callback, not the screen, decides whether to advance/save/rebind or exit. Menu navigation is always `onExit`. Choice IDs are passed unchanged to the caller, in source presentation order, restricted to the canonical available option IDs.

`NarrativeProjection` is a detached read model. `CampaignShell` renders its supplied title, chapter heading, segmented progress, actual backdrop, menu button and scroll body. Its display methods are reusable by staging/results without gaining campaign authority. The native `grayflag-story.json` schema 3 owns story/choice structure, shared `identityNameKeys` for narrative roles and named campaign people, speaker IDs and manually authored semantic beat IDs. It contains message references, not prose. `grayflag-staging.json` schema 3 owns shared `titleKey`, `completionLabelKey`, chapter title references, scene artwork and identity-to-portrait-ID bindings. `portrait-catalog.json` alone owns current portrait resource paths and hashes; a shared injected `PortraitCatalog` resolves them for story, staging and HUD. Level entries no longer repeat chapter titles. `zh_CN.po` alone owns narrative wording and the shared shell labels. No story/choice/scene identifiers are duplicated in production C#.

`StoryDocument.Load(text)` validates all content keys and zero-argument formats, unique story/choice/beat/option identities and mapped speakers before navigation. Literal old fields are rejected, not adapted. The projection resolves messages into detached view models; controls render those values. The application owns the one injected service. There is no localization dependency in Core, no translated prose/key added to campaign state, and no migration for schema-1 presentation documents.

`export-grayflag.ts` no longer regenerates these documents or scrapes TypeScript menu wording. It refreshes explicitly referenced, hash-verified scene artwork and verifies native portrait catalog bytes without copying legacy portraits. Native authoring now advances independently of original browser wording. Menu/staging labels and semantic-refusal presentation also use the catalog. Built-in level/unit names and remaining battle/outcome/progress text are not yet migrated; these slices do not claim complete game internationalization.

## Campaign-relative dates

The original `story.ts` mixes the prologue's world date, `断冠纪第 318 年`, with the campaign's year-zero timeline. The source campaign README pairs years 0–3 with Laiya's ages 18–21; these are relative story years, not a reset of the world calendar. Native PO wording now labels all 34 dated stories `战役第 … 年`, and the prologue retains the world-date anchor in parentheses. Numbers and seasons are unchanged; the failed ending remains `未定`. No calendar conversion, runtime clock or save field is introduced. The original chapter-two outline spans deep winter into the following autumn, so no absolute-year boundary is inferred from its relative labels.

`tests/native/narrative.json` explicitly owns all 35 current expected dates. Projections and actual widgets compare directly with that reviewed native fixture, without a C# date override. Its [migration contract](../tests/native/narrative.md) records the precise intentional departures from the unchanged historical oracle, including formal mission wording and the existing native portrait-source ownership.

## Ending reading contract

The native design intentionally departs from the historical TypeScript controller's
first-beat-only ending. All four existing chapter-three ending beats are readable
in order. Before the final beat, the primary action is `下一段 →` for active,
completed and failed campaign states; it changes only the local cursor.

At the last beat, an active ending invokes `onFinish`. The application advances
and saves the declared canonical outcome, then explicitly rebinds the same
ending with its last `initialBeatIndex`. This retains the text just read rather
than forcing a reread. Its next final action returns to the menu without another
transition or save. A completed/failed ending opened afresh is also readable in
order, and its final action only exits. The single-beat failure ending retains
its immediate final action and declared failed outcome.

Fresh Continue, F9 load and exit/re-entry start at beat zero. The cursor handoff
belongs only to the same active ending's completion, not to campaign persistence.
There are no new save fields, rewards, authored beats or PO wording changes.
The final active label remains the existing completion label, including for the
failure ending; terminal final actions use `返回主菜单`.

## Text, images and controls

The screen mounts exactly one dialogue beat, with the localized speaker label (unknown speaker IDs are authoring errors), portrait when mapped, current and prior active beat dots, source next/continue/finish label, kicker/date/location/summary, and the original three mission labels. All 11 choice documents retain their original option number, label, detail, consequence and save-effect note.

Null story scenes intentionally select the chapter backdrop. A missing declared scene does **not** select an unrelated substitute. A narrator/unmapped portrait stays portrait-free; missing mapped artwork produces a localized, player-safe artwork-failure notice. The precise missing source/resource path remains available through `RenderingDiagnostics`, not in the player label. The verification script checks native portrait PNG bytes against the catalog and remaining scene PNGs against both source/export hashes before loading them in Godot.

Stale local-page buttons, old rebound controls, double submissions and unmounted controls cannot advance or choose. One successful canonical callback disables competing navigation until the caller rebinds. This is intentionally stronger than an unmounted-but-undisposed original DOM root, whose click listener can still run; source `dispose()` is what disables that listener.

Local beat changes retain the same scroll container and its offset. Resizing retains the beat and scroll position subject to ordinary content bounds. Native layout follows the source 760 px breakpoint: progress hides, mission/choice cards stack, actions become one column and portraits shrink. A new caller binding starts at the top. Headless native layout tests cover these contracts; they are not a browser/native pixel comparison or proof of physical pointer/touch usability.

## Verification and limits

Run:

```sh
bash tools/godot/narrative-checks/run.sh --verify-guard
```

The test creates a fresh `/tmp/empire-narrative.*` project and isolates XDG data/config/cache/runtime directories; it does not touch user saves. It copies the actual production narrative classes and the exact production JSON loader, and imports the native catalog portraits and required source-exported scene PNGs. The default gate uses the reviewed `tests/native/narrative.json` and native story-schema-3/staging-schema-3 documents, with no TypeScript runtime. `verify-native.sh` runs the same scene against this same checked-in fixture.

The complete fixture migrated from observations originally produced by `StoryCampaignController`, its adapter/runtime and save helpers preserves:

- 35 documents / 91 ordinary-mode beat render fixtures, plus all five ending beats under active/completed/failed status: 106 native story projections total.
- All 11 source-rendered choice menus.
- 48 transition fixtures, including 15 reviewed ending sequences starting at every declared cursor under active/completed/failed status.
- 11 actual DOM lifetime fixtures, with three unmounted/remounted cases explicitly marked as native safety differences.
- Four supplemental native inputs: real runtime retreat history gives nonzero progress, a valid zero-battle definition, an unknown speaker rejection, and a known speaker with no portrait. The unknown-speaker fallback is an intentional breaking correction; its old display expectation was removed, not silently normalized.

Native checks compare complete story projections against the current native fixture, without date, prose or portrait exclusions, and explicitly assert unknown-speaker rejection. Native widgets exercise every ordinary-mode beat fixture and every ending beat/status fixture. An explicit coverage assertion retains the existing `reinforcement-deadline` widget case. All 91 beat speaker projections use the shared identity owner in an independent check group; group failures are collected so prose failures do not hide identity checks or vice versa. Canonical normal and ending callback sequences use the existing C# `CampaignRuntime` and `CampaignSave`; no battles or victories are fabricated.

Negative controls change only temporary expected prose, an expected speaker name, and the production projection's next-beat key. Each must fail its corresponding check, and the prose/speaker controls also prove the independent group still runs. The historical source-side ending paging mutation was separately rejected by the original oracle; that frozen browser contract is not the current native ending contract.

The completed narrative-only checkpoint passed its native widget/projection checks and wrong-navigation-key negative control. That checkpoint covered 452 catalog messages and 415 content references, before the subsequent menu/staging/refusal additions; it is not merged-game acceptance. Parser and authoring checks reject malformed, duplicate, missing, empty, fuzzy and obsolete data. Two independent in-memory catalogs change actual native widget wording without changing authoring identities or state. These test catalogs are not additional shipped languages.

Not claimed: complete built-in-content/battle/outcome i18n; a victorious campaign playthrough; browser CSS effects or pixel-identical typography; full-application physical pointer/touch acceptance; a newly exported standalone game. The separate foundation packaging check established native PO loading/formatting in a real exported executable; product release validation remains the integration owner's gate.

## Native portrait ownership and revisions

`data/portrait-catalog.json` schema 1 is the single native owner of all eight
named cards: Laiya, Roderick, Cain, Bran, Mirelle, Tasha, Ivra and Torren. Each
identity declares only `resourcePath` and `sha256`. Runtime diagnostic/source
strings are derived from that path as `/godot/…`, never authored a second time.
Existing PNG locations are retained. Staging/narrative schema 3 stores only
identity-to-portrait-ID bindings; tactical HUD schema 2 stores `portraitId` or an
explicit null selecting the bound unit's idle frame. An unknown ID, omitted
choice or missing named-card resource is an error, not a sprite fallback.

The legacy importers validate this catalog and refuse to overwrite its assets.
`export-tactical-hud-art.ts` preserves native portrait bindings and imports only
icons from the original provider. Scene and icon assets retain their existing
source/export contract. The original TypeScript portrait assets and frozen
JavaScript oracle remain unchanged.

All eight portraits now use generated transparent revisions. These are
image-generation edits with redrawn details, not claims of lossless pixel
extraction. `data/portrait-revisions.json` records historical provenance by
portrait ID, with the original source/hash and an honest edit description; it
never duplicates the current resource path/hash owner.

Named HUD portraits use the complete catalog texture and the native
`KeepAspectCentered` presentation, just as authored. The old JavaScript card's
side clipping does not apply to these native images. There is no named-card
atlas, per-character crop or padding, or extra texture cache. Explicit null
portrait bindings still use the canonical unit idle-frame atlas. Controls release
their references when replaced or detached; they do not dispose shared resources.

Laiya and Roderick were inspected at roster/dialogue sizes in the earlier
isolated application review. Mirelle passed native StoryScreen and TacticalHud
visual review on 2026-10-23. On the same date, a temporary native GUI fixture
mounted the actual production controls at their native portrait sizes: all eight
named HUD portraits plus the generic idle frame were reviewed on controlled
light/dark backgrounds. Cain, Bran, Tasha and red juvenile dragon Ivra v2 also
passed light/dark review in the actual StoryScreen with authored speaker beats.
No baked rectangle, conspicuous fringe or clipped extremity was visible; Ivra's
wings, curled tail and claws remained intact. Torren has no current authored
story-speaker binding, so its acceptance covers the HUD only. The fixture was
removed after review; its background overrides are not a shipped theme or a
text-contrast acceptance test. No portrait pixels, offsets or padding were
changed to pass these checks.

Static checks and the full `verify-native.sh` headless aggregate also passed,
including whole-texture identity, shared-resource lifetime and generic-atlas HUD
regressions. A temporary restoration of the old side crop failed the regression
as expected. These automated results, the controlled native GUI review and a
complete campaign/export acceptance are distinct; the last is not claimed here.
